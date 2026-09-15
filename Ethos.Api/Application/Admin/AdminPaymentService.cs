using System.Text.Json;
using Ethos.Api.Application.Finance;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminPaymentService : IAdminPaymentService
{
    private readonly AppDbContext _db;
    private readonly IPaymentRefundService _refundService;
    private readonly IPaymentReconciliationService _reconciliationService;
    private readonly IPaymentReceiptService _receiptService;
    private readonly ITrainerPayoutService _payoutService;
    private readonly IAdminAuditService _auditService;

    public AdminPaymentService(
        AppDbContext db,
        IPaymentRefundService refundService,
        IPaymentReconciliationService reconciliationService,
        IPaymentReceiptService receiptService,
        ITrainerPayoutService payoutService,
        IAdminAuditService auditService)
    {
        _db = db;
        _refundService = refundService;
        _reconciliationService = reconciliationService;
        _receiptService = receiptService;
        _payoutService = payoutService;
        _auditService = auditService;
    }

    public async Task<PagedResult<AdminPaymentTransactionResponse>> GetPaymentsAsync(
        int page,
        int pageSize,
        DateTime? startDate,
        DateTime? endDate,
        Guid? userId,
        PaymentPurpose? purpose,
        PaymentStatus? status,
        string? search,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.PaymentTransactions
            .AsNoTracking()
            .Include(p => p.User)
            .AsQueryable();

        if (userId.HasValue)
            query = query.Where(p => p.UserId == userId.Value);

        if (purpose.HasValue)
            query = query.Where(p => p.Purpose == purpose.Value);

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (startDate.HasValue)
            query = query.Where(p => p.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(p => p.CreatedAt <= endDate.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p =>
                (p.RazorpayOrderId != null && p.RazorpayOrderId.ToLower().Contains(s)) ||
                (p.RazorpayPaymentId != null && p.RazorpayPaymentId.ToLower().Contains(s)) ||
                p.User.FullName.ToLower().Contains(s) ||
                (p.User.CustomerCode != null && p.User.CustomerCode.ToLower().Contains(s)) ||
                p.User.Phone.Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var transactions = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var enrichedItems = await EnrichTransactionsAsync(transactions, cancellationToken);

        return new PagedResult<AdminPaymentTransactionResponse>
        {
            Items = enrichedItems,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<AdminPaymentTransactionResponse?> GetPaymentByIdAsync(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var p = await _db.PaymentTransactions
            .AsNoTracking()
            .Include(pt => pt.User)
            .FirstOrDefaultAsync(pt => pt.Id == transactionId, cancellationToken);

        if (p == null) return null;

        var enriched = await EnrichTransactionsAsync(new[] { p }, cancellationToken);
        return enriched.FirstOrDefault();
    }

    private async Task<List<AdminPaymentTransactionResponse>> EnrichTransactionsAsync(
        IReadOnlyList<PaymentTransaction> transactions,
        CancellationToken cancellationToken)
    {
        if (transactions.Count == 0) return new List<AdminPaymentTransactionResponse>();

        var txIds = transactions.Select(t => t.Id).ToList();

        // 1. Batch fetch all payment events for these transactions
        var relevantEvents = await _db.PaymentEvents
            .AsNoTracking()
            .Where(e => txIds.Contains(e.PaymentTransactionId))
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        // Map total refunds per transaction
        var refundMap = new Dictionary<Guid, decimal>();
        var discrepancyMap = new Dictionary<Guid, bool>();
        var latestNotesMap = new Dictionary<Guid, string>();
        var resolvedMap = new Dictionary<Guid, bool>();

        foreach (var ev in relevantEvents)
        {
            if (ev.EventType == "EXTERNAL_REFUND_RECORDED" && !string.IsNullOrWhiteSpace(ev.Payload))
            {
                try
                {
                    using var doc = JsonDocument.Parse(ev.Payload);
                    if (doc.RootElement.TryGetProperty("RefundAmount", out var amtProp))
                    {
                        refundMap[ev.PaymentTransactionId] = refundMap.GetValueOrDefault(ev.PaymentTransactionId) + amtProp.GetDecimal();
                    }
                }
                catch {}
            }
            else if (ev.EventType == "CUSTOMER_DISCREPANCY_FLAGGED")
            {
                discrepancyMap[ev.PaymentTransactionId] = true;
            }
            else if (ev.EventType == "ADMIN_MANUAL_FULFILLMENT_RESOLVED" || ev.EventType == "ADMIN_ISSUE_CLOSED_NOT_RECEIVED")
            {
                resolvedMap[ev.PaymentTransactionId] = true;
            }

            if ((ev.EventType == "ADMIN_NOTE_RECORDED" || ev.EventType == "ADMIN_MANUAL_FULFILLMENT_RESOLVED" || ev.EventType == "CUSTOMER_DISCREPANCY_FLAGGED") &&
                !string.IsNullOrWhiteSpace(ev.Payload))
            {
                try
                {
                    using var doc = JsonDocument.Parse(ev.Payload);
                    if (doc.RootElement.TryGetProperty("AdminReason", out var rProp))
                    {
                        latestNotesMap[ev.PaymentTransactionId] = rProp.GetString() ?? "";
                    }
                    else if (doc.RootElement.TryGetProperty("Reason", out var r2Prop))
                    {
                        latestNotesMap[ev.PaymentTransactionId] = r2Prop.GetString() ?? "";
                    }
                }
                catch {}
            }
        }

        // Set of explicitly reconciled transactions
        var reconciledTxIds = relevantEvents
            .Where(e => e.EventType == "RECONCILIATION_SYNC" || e.EventType == "CORRECTIVE_RECONCILIATION" || e.EventType == "ADMIN_MANUAL_FULFILLMENT_RESOLVED")
            .Select(e => e.PaymentTransactionId)
            .ToHashSet();

        // 2. Batch fetch fulfillment states
        var fulfilledPackageTxIds = await _db.StudentPackages
            .AsNoTracking()
            .Where(sp => sp.PaymentTransactionId.HasValue && txIds.Contains(sp.PaymentTransactionId.Value))
            .Select(sp => sp.PaymentTransactionId!.Value)
            .ToHashSetAsync(cancellationToken);

        var confirmedBookingTxIds = await _db.WorkshopBookings
            .AsNoTracking()
            .Where(wb => wb.PaymentTransactionId.HasValue && txIds.Contains(wb.PaymentTransactionId.Value) && wb.Status == WorkshopBookingStatus.Confirmed)
            .Select(wb => wb.PaymentTransactionId!.Value)
            .ToHashSetAsync(cancellationToken);

        // 3. Collect ReferenceIds by Purpose for batch resolution
        var pkgRefIds = transactions.Where(t => t.Purpose == PaymentPurpose.PackagePurchase).Select(t => t.ReferenceId).Distinct().ToList();
        var wsRefIds = transactions.Where(t => t.Purpose == PaymentPurpose.WorkshopBooking).Select(t => t.ReferenceId).Distinct().ToList();
        var appRefIds = transactions.Where(t => t.Purpose == PaymentPurpose.TrainerApplication).Select(t => t.ReferenceId).Distinct().ToList();
        var upRefIds = transactions.Where(t => t.Purpose == PaymentPurpose.TrainerTierUpgrade).Select(t => t.ReferenceId).Distinct().ToList();

        // Query Packages
        var packagesMap = pkgRefIds.Count > 0
            ? await _db.Packages.AsNoTracking()
                .Where(p => pkgRefIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken)
            : new Dictionary<Guid, Package>();

        // Query Workshops (primary) and WorkshopBookings (fallback)
        var workshopsMap = wsRefIds.Count > 0
            ? await _db.Workshops.AsNoTracking()
                .Where(w => wsRefIds.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id, cancellationToken)
            : new Dictionary<Guid, Workshop>();

        var missingWsRefIds = wsRefIds.Where(id => !workshopsMap.ContainsKey(id)).ToList();
        var bookingsMap = missingWsRefIds.Count > 0
            ? await _db.WorkshopBookings.AsNoTracking()
                .Include(b => b.Workshop)
                .Where(b => missingWsRefIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, cancellationToken)
            : new Dictionary<Guid, WorkshopBooking>();

        // Query Trainer Applications
        var trainerAppsMap = appRefIds.Count > 0
            ? await _db.TrainerApplications.AsNoTracking()
                .Include(a => a.TrainerProfile)
                .ThenInclude(p => p.CurrentTier)
                .Where(a => appRefIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken)
            : new Dictionary<Guid, TrainerApplication>();

        // Query Trainer Upgrade Requests
        var trainerUpgradesMap = upRefIds.Count > 0
            ? await _db.TrainerUpgradeRequests.AsNoTracking()
                .Include(u => u.RequestedTier)
                .Where(u => upRefIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancellationToken)
            : new Dictionary<Guid, TrainerUpgradeRequest>();

        // 4. Assemble enriched response DTOs
        var results = new List<AdminPaymentTransactionResponse>();

        foreach (var p in transactions)
        {
            string itemName;
            string? itemDetails = null;
            string displayPurpose;

            switch (p.Purpose)
            {
                case PaymentPurpose.PackagePurchase:
                    displayPurpose = "Dance Package Purchase";
                    if (packagesMap.TryGetValue(p.ReferenceId, out var pkg))
                    {
                        itemName = pkg.Name;
                        itemDetails = $"{pkg.ClassLimit} Classes • {pkg.DurationDays} Days Validity";
                    }
                    else
                    {
                        itemName = "Package Purchase";
                        itemDetails = "Dance Class Package";
                    }
                    break;

                case PaymentPurpose.WorkshopBooking:
                    displayPurpose = "Workshop Registration";
                    if (workshopsMap.TryGetValue(p.ReferenceId, out var ws))
                    {
                        itemName = ws.Title;
                        itemDetails = $"{ws.DanceStyle} • Level: {ws.Level}";
                    }
                    else if (bookingsMap.TryGetValue(p.ReferenceId, out var wb) && wb.Workshop != null)
                    {
                        itemName = wb.Workshop.Title;
                        itemDetails = $"{wb.Workshop.DanceStyle} • Level: {wb.Workshop.Level}";
                    }
                    else
                    {
                        itemName = "Workshop Registration";
                        itemDetails = "Studio Workshop Session";
                    }
                    break;

                case PaymentPurpose.TrainerApplication:
                    displayPurpose = "Trainer Onboarding Fee";
                    if (trainerAppsMap.TryGetValue(p.ReferenceId, out var app))
                    {
                        var tierName = app.TrainerProfile?.CurrentTier?.Name ?? "Standard";
                        itemName = $"Trainer Onboarding ({tierName} Tier)";
                        itemDetails = $"Application Ref: {app.Id.ToString()[..8].ToUpper()}";
                    }
                    else
                    {
                        itemName = "Trainer Onboarding Fee";
                        itemDetails = "Instructor Application";
                    }
                    break;

                case PaymentPurpose.TrainerTierUpgrade:
                    displayPurpose = "Trainer Tier Upgrade";
                    if (trainerUpgradesMap.TryGetValue(p.ReferenceId, out var up))
                    {
                        var targetTier = up.RequestedTier?.Name ?? "Advanced Tier";
                        itemName = $"Upgrade to {targetTier}";
                        itemDetails = $"Tier Upgrade Request: {up.Id.ToString()[..8].ToUpper()}";
                    }
                    else
                    {
                        itemName = "Trainer Tier Upgrade";
                        itemDetails = "Tier Advancement Fee";
                    }
                    break;

                default:
                    displayPurpose = "Studio Payment";
                    itemName = "Unclassified Payment";
                    itemDetails = $"Ref: {p.ReferenceId.ToString()[..8]}";
                    break;
            }

            // Display Status mapping
            string displayStatus = p.Status switch
            {
                PaymentStatus.Created => "Created",
                PaymentStatus.OrderCreated => "Checkout Abandoned",
                PaymentStatus.PaymentPending => "Payment Pending",
                PaymentStatus.Paid => "Payment Successful",
                PaymentStatus.Failed => "Payment Failed",
                PaymentStatus.Cancelled => "Order Cancelled",
                PaymentStatus.Refunded => "Fully Refunded",
                PaymentStatus.PartiallyRefunded => "Partially Refunded",
                _ => p.Status.ToString()
            };

            // Discrepancy flag
            bool hasDiscrepancy = discrepancyMap.GetValueOrDefault(p.Id) && !resolvedMap.GetValueOrDefault(p.Id);
            if (hasDiscrepancy && p.Status != PaymentStatus.Paid)
            {
                displayStatus = "Customer Reports Deduction";
            }

            // Fulfillment Status
            string fulfillmentStatus;
            if (p.Status == PaymentStatus.Refunded)
            {
                fulfillmentStatus = "Refunded";
            }
            else if (p.Status == PaymentStatus.Paid)
            {
                switch (p.Purpose)
                {
                    case PaymentPurpose.PackagePurchase:
                        fulfillmentStatus = fulfilledPackageTxIds.Contains(p.Id) ? "Package Active" : "Pending Fulfillment";
                        break;
                    case PaymentPurpose.WorkshopBooking:
                        fulfillmentStatus = confirmedBookingTxIds.Contains(p.Id) ? "Booking Confirmed" : "Pending Fulfillment";
                        break;
                    case PaymentPurpose.TrainerTierUpgrade:
                        if (trainerUpgradesMap.TryGetValue(p.ReferenceId, out var upReq) && upReq.Status == TrainerUpgradeRequestStatus.PaymentVerified)
                            fulfillmentStatus = "Tier Verified";
                        else
                            fulfillmentStatus = "Pending Verification";
                        break;
                    case PaymentPurpose.TrainerApplication:
                        if (trainerAppsMap.TryGetValue(p.ReferenceId, out var appReq) && appReq.Status == TrainerApplicationStatus.PaymentVerified)
                            fulfillmentStatus = "Application Verified";
                        else
                            fulfillmentStatus = "Pending Verification";
                        break;
                    default:
                        fulfillmentStatus = "Completed";
                        break;
                }
            }
            else if (p.Status == PaymentStatus.PaymentPending)
            {
                fulfillmentStatus = "Awaiting Confirmation";
            }
            else
            {
                fulfillmentStatus = "Not Completed";
            }

            // Customer Impact
            string customerImpact;
            if (p.Status == PaymentStatus.Paid)
            {
                customerImpact = "Payment Received & Confirmed";
            }
            else if (p.Status == PaymentStatus.Refunded)
            {
                customerImpact = "Amount Refunded to Customer";
            }
            else if (p.Status == PaymentStatus.PartiallyRefunded)
            {
                customerImpact = "Partial Amount Refunded";
            }
            else if (hasDiscrepancy)
            {
                customerImpact = "Customer Claims Debited (Action Required)";
            }
            else if (p.Status == PaymentStatus.Failed)
            {
                customerImpact = "No Studio Funds Captured";
            }
            else
            {
                customerImpact = "Checkout Incomplete / Abandoned";
            }

            // Action Needed
            string actionNeeded;
            if (hasDiscrepancy)
            {
                actionNeeded = "Review Discrepancy";
            }
            else if (p.Status == PaymentStatus.Paid && fulfillmentStatus.Contains("Pending"))
            {
                actionNeeded = "Complete Fulfillment";
            }
            else if (p.Status == PaymentStatus.Failed)
            {
                actionNeeded = "Review Issue";
            }
            else if (p.Status == PaymentStatus.PaymentPending)
            {
                actionNeeded = "Check Status";
            }
            else if (p.Status == PaymentStatus.Paid)
            {
                actionNeeded = "Issue Receipt / Refund";
            }
            else
            {
                actionNeeded = "None";
            }

            // Gateway Verification vs Reconciliation
            bool isGatewayVerified = !string.IsNullOrWhiteSpace(p.RazorpayPaymentId) &&
                                     (p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.PartiallyRefunded);

            bool isReconciled = reconciledTxIds.Contains(p.Id) ||
                                (isGatewayVerified && !string.IsNullOrWhiteSpace(p.RazorpayOrderId));

            // Refund amounts calculation
            decimal refundedAmount = refundMap.GetValueOrDefault(p.Id);
            if (p.Status == PaymentStatus.Refunded && refundedAmount == 0)
            {
                refundedAmount = p.Amount;
            }
            decimal remainingRefundable = Math.Max(0m, p.Amount - refundedAmount);

            // Customer code resolution
            string? customerCode = p.User?.CustomerCode;
            if (string.IsNullOrWhiteSpace(customerCode) && p.UserId != Guid.Empty)
            {
                customerCode = "CUST-" + p.UserId.ToString()[..6].ToUpper();
            }

            results.Add(new AdminPaymentTransactionResponse
            {
                Id = p.Id,
                UserId = p.UserId,
                UserName = p.User?.FullName ?? "Unknown User",
                UserPhone = p.User?.Phone ?? "",
                UserCustomerCode = customerCode,
                Purpose = (int)p.Purpose,
                PurposeName = p.Purpose.ToString(),
                ReferenceId = p.ReferenceId.ToString(),
                ItemName = itemName,
                ItemDetails = itemDetails,
                DisplayPurpose = displayPurpose,
                Amount = p.Amount,
                Currency = p.Currency,
                Status = (int)p.Status,
                StatusName = p.Status.ToString(),
                DisplayStatus = displayStatus,
                RazorpayOrderId = p.RazorpayOrderId,
                RazorpayPaymentId = p.RazorpayPaymentId,
                IsGatewayVerified = isGatewayVerified,
                IsReconciled = isReconciled,
                TotalRefundedAmount = refundedAmount,
                RemainingRefundableAmount = remainingRefundable,
                FulfillmentStatus = fulfillmentStatus,
                CustomerImpact = customerImpact,
                ActionNeeded = actionNeeded,
                HasCustomerReportedDiscrepancy = hasDiscrepancy,
                LatestAdminNote = latestNotesMap.GetValueOrDefault(p.Id),
                CreatedAt = p.CreatedAt,
                PaidAt = p.PaidAt
            });
        }

        return results;
    }

    public async Task<AdminRevenueResponse> GetRevenueAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var paidTx = _db.PaymentTransactions
            .Where(p => p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.PartiallyRefunded);

        var grossSuccessfulRevenue = await paidTx.SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var currentMonthGross = await paidTx.Where(p => p.PaidAt >= startOfMonth).SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var packageRevenue = await paidTx.Where(p => p.Purpose == PaymentPurpose.PackagePurchase).SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var workshopRevenue = await paidTx.Where(p => p.Purpose == PaymentPurpose.WorkshopBooking).SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var appRevenue = await paidTx.Where(p => p.Purpose == PaymentPurpose.TrainerApplication).SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var upgradeRevenue = await paidTx.Where(p => p.Purpose == PaymentPurpose.TrainerTierUpgrade).SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var successfulCount = await paidTx.CountAsync(cancellationToken);
        var failedCount = await _db.PaymentTransactions.CountAsync(p => p.Status == PaymentStatus.Failed, cancellationToken);
        var pendingCount = await _db.PaymentTransactions.CountAsync(p => p.Status == PaymentStatus.Created || p.Status == PaymentStatus.OrderCreated || p.Status == PaymentStatus.PaymentPending, cancellationToken);

        // Authoritative total refunds recorded from PaymentEvents
        var refundEvents = await _db.PaymentEvents
            .AsNoTracking()
            .Where(e => e.EventType == "EXTERNAL_REFUND_RECORDED")
            .ToListAsync(cancellationToken);

        decimal totalRefunds = 0m;
        foreach (var ev in refundEvents)
        {
            if (!string.IsNullOrWhiteSpace(ev.Payload))
            {
                try
                {
                    using var doc = JsonDocument.Parse(ev.Payload);
                    if (doc.RootElement.TryGetProperty("RefundAmount", out var amtProp))
                    {
                        totalRefunds += amtProp.GetDecimal();
                    }
                }
                catch {}
            }
        }

        var legacyRefundedAmount = await _db.PaymentTransactions
            .Where(p => p.Status == PaymentStatus.Refunded)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        if (totalRefunds == 0 && legacyRefundedAmount > 0)
        {
            totalRefunds = legacyRefundedAmount;
        }

        var netRevenue = Math.Max(0m, grossSuccessfulRevenue - totalRefunds);

        // Operational counts
        var discrepancyTxIds = await _db.PaymentEvents
            .AsNoTracking()
            .Where(e => e.EventType == "CUSTOMER_DISCREPANCY_FLAGGED")
            .Select(e => e.PaymentTransactionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var resolvedTxIds = await _db.PaymentEvents
            .AsNoTracking()
            .Where(e => e.EventType == "ADMIN_MANUAL_FULFILLMENT_RESOLVED" || e.EventType == "ADMIN_ISSUE_CLOSED_NOT_RECEIVED")
            .Select(e => e.PaymentTransactionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeDiscrepancies = discrepancyTxIds.Except(resolvedTxIds).Count();
        var pendingGateway = await _db.PaymentTransactions
            .CountAsync(p => p.Status == PaymentStatus.PaymentPending, cancellationToken);

        var trainerPayouts = await _payoutService.GetTrainerPayoutsAsync(cancellationToken);
        var pendingPayoutsCount = trainerPayouts.Payouts.Count(p => p.Status != "PROCESSED" && p.TrainerPayoutAmount > 0);
        var pendingPayoutsAmount = trainerPayouts.Payouts
            .Where(p => p.Status != "PROCESSED" && p.TrainerPayoutAmount > 0)
            .Sum(p => p.TrainerPayoutAmount);

        return new AdminRevenueResponse
        {
            TotalSuccessfulRevenue = netRevenue,
            GrossSuccessfulRevenue = grossSuccessfulRevenue,
            TotalRefundedRevenue = totalRefunds,
            CurrentMonthRevenue = currentMonthGross,
            PackageRevenue = packageRevenue,
            WorkshopRevenue = workshopRevenue,
            TrainerApplicationRevenue = appRevenue,
            TrainerUpgradeRevenue = upgradeRevenue,
            SuccessfulTransactionsCount = successfulCount,
            FailedTransactionsCount = failedCount,
            PendingTransactionsCount = pendingCount,
            PaymentsNeedingAttentionCount = activeDiscrepancies + pendingGateway,
            CustomerIssuesCount = activeDiscrepancies,
            TrainerPayoutsPendingCount = pendingPayoutsCount,
            TrainerPayoutsPendingAmount = pendingPayoutsAmount,
            RefundsIssuedCount = refundEvents.Count
        };
    }

    public async Task<AdminResolvePaymentIssueResponse> ResolvePaymentIssueAsync(
        Guid transactionId,
        Guid adminUserId,
        AdminResolvePaymentIssueRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AdminReason))
            throw new ArgumentException("Administrative reason is strictly mandatory.");

        var payment = await _db.PaymentTransactions
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == transactionId, cancellationToken);

        if (payment == null)
            throw new ArgumentException("Payment transaction was not found.");

        var previousStatus = payment.Status.ToString();
        var now = DateTime.UtcNow;
        bool fulfillmentExecuted = false;
        string fulfillmentResult = "No fulfillment action taken";

        if (request.Action == "CONFIRM_AND_FULFILL")
        {
            PaymentStateMachine.AssertAdminResolution(payment.Status, PaymentStatus.Paid);

            if (string.IsNullOrWhiteSpace(request.GatewayPaymentId) && string.IsNullOrWhiteSpace(payment.RazorpayPaymentId))
            {
                throw new InvalidOperationException("Gateway payment ID or bank UTR reference is required to confirm payment.");
            }

            payment.Status = PaymentStatus.Paid;
            if (!string.IsNullOrWhiteSpace(request.GatewayPaymentId))
            {
                payment.RazorpayPaymentId = request.GatewayPaymentId.Trim();
            }
            if (!payment.PaidAt.HasValue)
            {
                payment.PaidAt = now;
            }
            payment.UpdatedAt = now;

            // Perform idempotent fulfillment based on purpose
            switch (payment.Purpose)
            {
                case PaymentPurpose.PackagePurchase:
                    fulfillmentResult = await FulfillPackagePurchaseAsync(payment, payment.UserId, cancellationToken);
                    fulfillmentExecuted = true;
                    break;

                case PaymentPurpose.WorkshopBooking:
                    fulfillmentResult = await FulfillWorkshopBookingAsync(payment, payment.UserId, cancellationToken);
                    fulfillmentExecuted = true;
                    break;

                case PaymentPurpose.TrainerTierUpgrade:
                    fulfillmentResult = await FulfillTrainerTierUpgradeAsync(payment, payment.UserId, cancellationToken);
                    fulfillmentExecuted = true;
                    break;

                case PaymentPurpose.TrainerApplication:
                    fulfillmentResult = await FulfillTrainerApplicationAsync(payment, payment.UserId, cancellationToken);
                    fulfillmentExecuted = true;
                    break;

                default:
                    fulfillmentResult = "Payment confirmed for studio services.";
                    break;
            }

            var eventPayload = JsonSerializer.Serialize(new
            {
                Action = request.Action,
                IssueType = request.IssueType,
                PreviousStatus = previousStatus,
                NewStatus = "Paid",
                request.AdminReason,
                request.AdminNotes,
                request.GatewayPaymentId,
                FulfillmentResult = fulfillmentResult,
                ResolvedByAdminId = adminUserId,
                ResolvedAt = now
            });

            _db.PaymentEvents.Add(new PaymentEvent
            {
                Id = Guid.NewGuid(),
                PaymentTransactionId = payment.Id,
                EventType = "ADMIN_MANUAL_FULFILLMENT_RESOLVED",
                Payload = eventPayload,
                CreatedAt = now
            });

            _auditService.AddAuditLog(
                adminUserId,
                "PAYMENT_ISSUE_RESOLVED",
                "PaymentTransaction",
                payment.Id,
                $"Admin resolved payment {payment.Id} ({payment.Purpose}) to Paid. Action: {request.Action}. Reason: {request.AdminReason}. Fulfillment: {fulfillmentResult}");
        }
        else if (request.Action == "MARK_NOT_RECEIVED")
        {
            var eventPayload = JsonSerializer.Serialize(new
            {
                Action = request.Action,
                IssueType = request.IssueType,
                PreviousStatus = previousStatus,
                request.AdminReason,
                request.AdminNotes,
                ResolvedByAdminId = adminUserId,
                ResolvedAt = now
            });

            _db.PaymentEvents.Add(new PaymentEvent
            {
                Id = Guid.NewGuid(),
                PaymentTransactionId = payment.Id,
                EventType = "ADMIN_ISSUE_CLOSED_NOT_RECEIVED",
                Payload = eventPayload,
                CreatedAt = now
            });

            _auditService.AddAuditLog(
                adminUserId,
                "PAYMENT_ISSUE_CLOSED",
                "PaymentTransaction",
                payment.Id,
                $"Admin closed payment issue {payment.Id} as funds not received. Reason: {request.AdminReason}");
        }
        else if (request.Action == "FLAG_DISCREPANCY")
        {
            var eventPayload = JsonSerializer.Serialize(new
            {
                Action = request.Action,
                IssueType = request.IssueType,
                request.AdminReason,
                request.AdminNotes,
                request.GatewayPaymentId,
                FlaggedByAdminId = adminUserId,
                FlaggedAt = now
            });

            _db.PaymentEvents.Add(new PaymentEvent
            {
                Id = Guid.NewGuid(),
                PaymentTransactionId = payment.Id,
                EventType = "CUSTOMER_DISCREPANCY_FLAGGED",
                Payload = eventPayload,
                CreatedAt = now
            });

            _auditService.AddAuditLog(
                adminUserId,
                "PAYMENT_DISCREPANCY_FLAGGED",
                "PaymentTransaction",
                payment.Id,
                $"Flagged customer payment discrepancy for {payment.Id}. Reason: {request.AdminReason}");
        }
        else // RECORD_NOTE
        {
            var eventPayload = JsonSerializer.Serialize(new
            {
                Action = "RECORD_NOTE",
                request.AdminReason,
                request.AdminNotes,
                RecordedByAdminId = adminUserId,
                RecordedAt = now
            });

            _db.PaymentEvents.Add(new PaymentEvent
            {
                Id = Guid.NewGuid(),
                PaymentTransactionId = payment.Id,
                EventType = "ADMIN_NOTE_RECORDED",
                Payload = eventPayload,
                CreatedAt = now
            });

            _auditService.AddAuditLog(
                adminUserId,
                "PAYMENT_NOTE_ADDED",
                "PaymentTransaction",
                payment.Id,
                $"Recorded administrative note on payment {payment.Id}: {request.AdminReason}");
        }

        await _db.SaveChangesAsync(cancellationToken);

        var enrichedList = await EnrichTransactionsAsync(new[] { payment }, cancellationToken);
        var enriched = enrichedList.First();

        string customerMsg = GenerateCustomerMessageTemplate(enriched, request.Action);

        return new AdminResolvePaymentIssueResponse
        {
            TransactionId = payment.Id,
            PreviousStatus = previousStatus,
            NewStatus = payment.Status.ToString(),
            FulfillmentResult = fulfillmentResult,
            FulfillmentExecuted = fulfillmentExecuted,
            Message = $"Payment issue processed ({request.Action}).",
            ResolvedAt = now,
            CustomerMessageTemplate = customerMsg,
            Transaction = enriched
        };
    }

    private async Task<string> FulfillPackagePurchaseAsync(PaymentTransaction transaction, Guid userId, CancellationToken cancellationToken)
    {
        var alreadyFulfilled = await _db.StudentPackages
            .AnyAsync(sp => sp.PaymentTransactionId == transaction.Id, cancellationToken);

        if (alreadyFulfilled)
        {
            return "Package was already granted previously; duplicate creation skipped.";
        }

        var studentProfile = await _db.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.UserId == userId, cancellationToken);

        if (studentProfile == null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            studentProfile = new StudentProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EmergencyContactName = user?.FullName,
                EmergencyContactPhone = user?.Phone,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.StudentProfiles.Add(studentProfile);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var package = await _db.Packages
            .FirstOrDefaultAsync(p => p.Id == transaction.ReferenceId, cancellationToken);

        if (package == null)
        {
            throw new InvalidOperationException("The purchased package no longer exists in database.");
        }

        var now = DateTime.UtcNow;
        var activePackage = await _db.StudentPackages
            .Where(sp => sp.StudentProfileId == studentProfile.Id && sp.Status == StudentPackageStatus.Active && sp.ExpiryDate > now)
            .OrderByDescending(sp => sp.ExpiryDate)
            .FirstOrDefaultAsync(cancellationToken);

        var startDate = activePackage != null ? activePackage.ExpiryDate : now;
        var expiryDate = startDate.AddDays(package.DurationDays);

        var studentPackage = new StudentPackage
        {
            Id = Guid.NewGuid(),
            StudentProfileId = studentProfile.Id,
            PackageId = package.Id,
            PaymentTransactionId = transaction.Id,
            StartDate = startDate,
            ExpiryDate = expiryDate,
            Status = activePackage != null ? StudentPackageStatus.Pending : StudentPackageStatus.Active,
            ClassesAllowed = package.ClassLimit,
            ClassesUsed = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.StudentPackages.Add(studentPackage);
        return $"Successfully activated '{package.Name}' with {package.ClassLimit} classes valid for {package.DurationDays} days.";
    }

    private async Task<string> FulfillWorkshopBookingAsync(PaymentTransaction transaction, Guid userId, CancellationToken cancellationToken)
    {
        var studentProfile = await _db.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.UserId == userId, cancellationToken);

        if (studentProfile == null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            studentProfile = new StudentProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EmergencyContactName = user?.FullName,
                EmergencyContactPhone = user?.Phone,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.StudentProfiles.Add(studentProfile);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var targetWorkshopId = transaction.ReferenceId;
        var existingBooking = await _db.WorkshopBookings
            .FirstOrDefaultAsync(b => b.Id == transaction.ReferenceId || (b.WorkshopId == targetWorkshopId && b.StudentProfileId == studentProfile.Id), cancellationToken);

        if (existingBooking == null)
        {
            existingBooking = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = targetWorkshopId,
                StudentProfileId = studentProfile.Id,
                PaymentTransactionId = transaction.Id,
                Status = WorkshopBookingStatus.Confirmed,
                BookedAt = DateTime.UtcNow
            };
            _db.WorkshopBookings.Add(existingBooking);
            return "Workshop booking created and confirmed.";
        }

        if (existingBooking.Status == WorkshopBookingStatus.Confirmed && existingBooking.PaymentTransactionId == transaction.Id)
        {
            return "Workshop booking was already confirmed; duplicate confirmation skipped.";
        }

        existingBooking.Status = WorkshopBookingStatus.Confirmed;
        existingBooking.PaymentTransactionId = transaction.Id;
        existingBooking.CancelledAt = null;
        return "Workshop booking status updated to Confirmed.";
    }

    private async Task<string> FulfillTrainerTierUpgradeAsync(PaymentTransaction transaction, Guid userId, CancellationToken cancellationToken)
    {
        var upgradeReq = await _db.TrainerUpgradeRequests
            .Include(u => u.TrainerProfile)
            .Include(u => u.RequestedTier)
            .FirstOrDefaultAsync(u => u.Id == transaction.ReferenceId, cancellationToken);

        if (upgradeReq == null)
        {
            return "Trainer tier upgrade record was not found.";
        }

        upgradeReq.Status = TrainerUpgradeRequestStatus.PaymentVerified;
        return $"Trainer tier upgrade to '{upgradeReq.RequestedTier?.Name}' verified.";
    }

    private async Task<string> FulfillTrainerApplicationAsync(PaymentTransaction transaction, Guid userId, CancellationToken cancellationToken)
    {
        var app = await _db.TrainerApplications
            .FirstOrDefaultAsync(a => a.Id == transaction.ReferenceId, cancellationToken);

        if (app == null)
        {
            return "Trainer application was not found.";
        }

        app.PaymentTransactionId = transaction.Id;
        app.Status = TrainerApplicationStatus.PaymentVerified;
        app.PaymentVerifiedAt = DateTime.UtcNow;
        app.UpdatedAt = DateTime.UtcNow;
        return "Trainer application onboarding payment verified.";
    }

    public async Task<List<AdminPaymentTimelineEvent>> GetPaymentTimelineAsync(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var events = await _db.PaymentEvents
            .AsNoTracking()
            .Where(e => e.PaymentTransactionId == transactionId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        var timeline = new List<AdminPaymentTimelineEvent>();

        foreach (var ev in events)
        {
            string title;
            string description;
            string severity = "INFO";
            string? actor = null;

            switch (ev.EventType)
            {
                case "OrderCreated":
                case "PublicPackageOrderCreated":
                    title = "Payment Order Created";
                    description = "Customer initiated checkout session and generated gateway order.";
                    severity = "INFO";
                    break;

                case "PaymentVerified":
                case "PublicPackagePaymentVerified":
                    title = "Gateway Payment Verified & Captured";
                    description = "Payment signature verified and funds captured by Razorpay payment gateway.";
                    severity = "SUCCESS";
                    break;

                case "SignatureVerificationFailed":
                case "PublicPaymentSignatureVerificationFailed":
                    title = "Gateway Verification Failed";
                    description = "Payment verification declined or signature mismatch reported by gateway.";
                    severity = "DANGER";
                    break;

                case "EXTERNAL_REFUND_RECORDED":
                    title = "External Refund Recorded";
                    description = "Administrator recorded external gateway refund.";
                    severity = "WARNING";
                    actor = "Studio Administrator";
                    break;

                case "ADMIN_MANUAL_FULFILLMENT_RESOLVED":
                    title = "Manual Payment & Fulfillment Resolved";
                    description = "Administrator confirmed payment evidence and executed purchase fulfillment.";
                    severity = "SUCCESS";
                    actor = "Studio Administrator";
                    break;

                case "CUSTOMER_DISCREPANCY_FLAGGED":
                    title = "Customer Discrepancy Flagged";
                    description = "Marked for review: customer reported funds debited from bank account.";
                    severity = "WARNING";
                    actor = "Studio Administrator";
                    break;

                case "ADMIN_ISSUE_CLOSED_NOT_RECEIVED":
                    title = "Issue Closed — No Funds Received";
                    description = "Investigation completed: no evidence of studio fund capture found.";
                    severity = "INFO";
                    actor = "Studio Administrator";
                    break;

                case "ADMIN_NOTE_RECORDED":
                    title = "Administrative Audit Note";
                    description = "Administrative note appended to payment audit record.";
                    severity = "INFO";
                    actor = "Studio Administrator";
                    break;

                case "RECONCILIATION_SYNC":
                case "CORRECTIVE_RECONCILIATION":
                    title = "Ledger Reconciled";
                    description = "Payment record synchronized with gateway ledger.";
                    severity = "INFO";
                    actor = "Studio Administrator";
                    break;

                default:
                    title = ev.EventType;
                    description = ev.Payload ?? "Payment event logged.";
                    severity = "INFO";
                    break;
            }

            if (!string.IsNullOrWhiteSpace(ev.Payload))
            {
                try
                {
                    using var doc = JsonDocument.Parse(ev.Payload);
                    if (doc.RootElement.TryGetProperty("AdminReason", out var rProp))
                    {
                        description += $" Reason: {rProp.GetString()}";
                    }
                    else if (doc.RootElement.TryGetProperty("Reason", out var r2Prop))
                    {
                        description += $" Reason: {r2Prop.GetString()}";
                    }
                }
                catch {}
            }

            timeline.Add(new AdminPaymentTimelineEvent
            {
                Id = ev.Id,
                EventType = ev.EventType,
                EventTitle = title,
                Description = description,
                Timestamp = ev.CreatedAt,
                Actor = actor,
                Payload = ev.Payload,
                Severity = severity
            });
        }

        return timeline;
    }

    private static string GenerateCustomerMessageTemplate(AdminPaymentTransactionResponse tx, string action)
    {
        string customerName = string.IsNullOrWhiteSpace(tx.UserName) ? "Valued Customer" : tx.UserName;
        string itemName = tx.ItemName;
        string refId = tx.ReferenceId.Length >= 8 ? tx.ReferenceId[..8].ToUpper() : tx.ReferenceId;

        if (action == "CONFIRM_AND_FULFILL")
        {
            return $"Hello {customerName},\n\nWe have successfully confirmed your payment for {itemName} (Ref: {refId}) and completed your registration manually. Your pass/booking is now fully active in your Ethos Dance Studio account.\n\nThank you for dancing with us!\n- Ethos Dance Studio Team";
        }
        if (action == "MARK_NOT_RECEIVED")
        {
            return $"Hello {customerName},\n\nRegarding your payment attempt for {itemName} (Ref: {refId}): We checked our payment gateway records and no funds were captured. If the amount was deducted from your account, banks typically reverse unsuccessful debits within 3-5 business days. Please feel free to share your bank statement reference if you need further verification.\n\nWarm regards,\n- Ethos Dance Studio Team";
        }
        return $"Hello {customerName},\n\nYour payment for {itemName} (Ref: {refId}) is currently under review by our studio team. We are checking the gateway confirmation and will update you shortly.\n\n- Ethos Dance Studio Team";
    }

    public Task<AdminRefundResponse> RecordExternalRefundAsync(
        Guid transactionId,
        Guid adminUserId,
        AdminRecordRefundRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        return _refundService.RecordExternalRefundAsync(transactionId, adminUserId, request, idempotencyKey, cancellationToken);
    }


    public Task<AdminPaymentReceiptResponse?> GetPaymentReceiptAsync(
        Guid transactionId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        return _receiptService.GetPaymentReceiptAsync(transactionId, adminUserId, cancellationToken);
    }

    public Task<AdminTrainerPayoutResponse> GetTrainerPayoutsAsync(CancellationToken cancellationToken)
    {
        return _payoutService.GetTrainerPayoutsAsync(cancellationToken);
    }

    public Task ProcessTrainerPayoutAsync(
        Guid trainerId,
        Guid adminUserId,
        AdminProcessTrainerPayoutRequest request,
        CancellationToken cancellationToken)
    {
        return _payoutService.ProcessTrainerPayoutAsync(trainerId, adminUserId, request, cancellationToken);
    }
}
