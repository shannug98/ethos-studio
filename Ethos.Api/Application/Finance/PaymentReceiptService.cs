using System.Text.Json;
using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Finance;

public class PaymentReceiptService : IPaymentReceiptService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;

    public PaymentReceiptService(
        AppDbContext db,
        IAdminAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<AdminPaymentReceiptResponse?> GetPaymentReceiptAsync(
        Guid transactionId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var payment = await _db.PaymentTransactions
            .AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == transactionId, cancellationToken);

        if (payment == null) return null;

        // 1. Fetch related payment events for refunds and reconciliation
        var events = await _db.PaymentEvents
            .AsNoTracking()
            .Where(e => e.PaymentTransactionId == transactionId)
            .ToListAsync(cancellationToken);

        decimal totalRefundedAmount = 0;
        foreach (var ev in events.Where(e => e.EventType == "EXTERNAL_REFUND_RECORDED"))
        {
            if (!string.IsNullOrWhiteSpace(ev.Payload))
            {
                try
                {
                    using var doc = JsonDocument.Parse(ev.Payload);
                    if (doc.RootElement.TryGetProperty("RefundAmount", out var amtProp))
                    {
                        totalRefundedAmount += amtProp.GetDecimal();
                    }
                }
                catch
                {
                    // Ignore non-standard payloads
                }
            }
        }

        bool isReconciled = events.Any(e => e.EventType == "RECONCILIATION_SYNC" || e.EventType == "CORRECTIVE_RECONCILIATION");
        bool isGatewayVerified = payment.Status == PaymentStatus.Paid && !string.IsNullOrWhiteSpace(payment.RazorpayPaymentId);

        // 2. Resolve human-readable purpose, item name, details, and description
        string itemName;
        string? itemDetails = null;
        string itemDescription;

        switch (payment.Purpose)
        {
            case PaymentPurpose.PackagePurchase:
                var pkg = await _db.Packages
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == payment.ReferenceId, cancellationToken);

                if (pkg != null)
                {
                    itemName = pkg.Name;
                    itemDetails = $"{pkg.ClassLimit} Classes • {pkg.DurationDays} Days Validity";
                    itemDescription = $"Dance Package Purchase — {pkg.Name} ({pkg.ClassLimit} classes)";
                }
                else
                {
                    itemName = "Dance Class Package";
                    itemDetails = "Student Class Credits";
                    itemDescription = "Dance Package Purchase";
                }
                break;

            case PaymentPurpose.WorkshopBooking:
                // Primary lookup: direct workshop
                var directWs = await _db.Workshops
                    .AsNoTracking()
                    .FirstOrDefaultAsync(w => w.Id == payment.ReferenceId, cancellationToken);

                // Fallback lookup: reference ID is WorkshopBooking.Id
                WorkshopBooking? booking = null;
                if (directWs == null)
                {
                    booking = await _db.WorkshopBookings
                        .AsNoTracking()
                        .Include(b => b.Workshop)
                        .FirstOrDefaultAsync(b => b.Id == payment.ReferenceId, cancellationToken);
                }

                var ws = directWs ?? booking?.Workshop;
                if (ws != null)
                {
                    itemName = ws.Title;
                    var dateStr = ws.WorkshopDate.ToString("yyyy-MM-dd");
                    var timeStr = $"{ws.StartTime:hh\\:mm} - {ws.EndTime:hh\\:mm}";
                    var bookingRefStr = booking != null ? $" • Ref: BK-{booking.Id.ToString()[..8].ToUpper()}" : "";
                    itemDetails = $"{ws.DanceStyle} (Level: {ws.Level}) • Date: {dateStr} ({timeStr}){bookingRefStr}";
                    itemDescription = $"Workshop Registration — {ws.Title} ({ws.DanceStyle})";
                }
                else
                {
                    itemName = "Workshop Registration";
                    itemDetails = "Special Masterclass Session";
                    itemDescription = "Workshop Registration";
                }
                break;

            case PaymentPurpose.TrainerApplication:
                var app = await _db.TrainerApplications
                    .AsNoTracking()
                    .Include(a => a.TrainerProfile)
                        .ThenInclude(p => p.CurrentTier)
                    .FirstOrDefaultAsync(a => a.Id == payment.ReferenceId, cancellationToken);

                var appTier = app?.TrainerProfile?.CurrentTier?.Name ?? "Standard";
                itemName = $"Trainer Onboarding Application ({appTier} Tier)";
                itemDetails = "Trainer Verification & Profile Accreditation";
                itemDescription = $"Trainer Tier Registration — {appTier} Trainer";
                break;

            case PaymentPurpose.TrainerTierUpgrade:
                var upgrade = await _db.TrainerUpgradeRequests
                    .AsNoTracking()
                    .Include(u => u.RequestedTier)
                    .FirstOrDefaultAsync(u => u.Id == payment.ReferenceId, cancellationToken);

                var targetTier = upgrade?.RequestedTier?.Name ?? "Gold";
                itemName = $"Trainer Tier Upgrade ({targetTier} Tier)";
                itemDetails = $"Professional Certification & Upgrade to {targetTier}";
                itemDescription = $"Trainer Tier Upgrade — {targetTier} Trainer";
                break;

            case PaymentPurpose.TrainerPass:
                itemName = "Trainer Workshop Pass";
                itemDetails = "Trainer Professional Access";
                itemDescription = "Trainer Studio Pass";
                break;

            default:
                itemName = payment.Purpose.ToString();
                itemDetails = "Studio Service";
                itemDescription = payment.Purpose.ToString();
                break;
        }

        // 3. Customer Code & Details
        string customerCode = "CUST-" + payment.UserId.ToString()[..6].ToUpper();
        if (!string.IsNullOrWhiteSpace(payment.User?.CustomerCode))
        {
            customerCode = payment.User.CustomerCode;
        }

        // 4. Financial Status and Ledger Calculation
        string statusStr;
        string displayStatus;

        if (payment.Status == PaymentStatus.Paid)
        {
            if (totalRefundedAmount >= payment.Amount && payment.Amount > 0)
            {
                statusStr = "REFUNDED";
                displayStatus = "Fully Refunded";
            }
            else if (totalRefundedAmount > 0)
            {
                statusStr = "PARTIALLY_REFUNDED";
                displayStatus = "Partially Refunded";
            }
            else
            {
                statusStr = "PAID";
                displayStatus = "Payment Successful";
            }
        }
        else if (payment.Status == PaymentStatus.Refunded)
        {
            statusStr = "REFUNDED";
            displayStatus = "Fully Refunded";
        }
        else if (payment.Status == PaymentStatus.Failed)
        {
            statusStr = "FAILED";
            displayStatus = "Payment Failed";
        }
        else if (payment.Status == PaymentStatus.Cancelled)
        {
            statusStr = "CANCELLED";
            displayStatus = "Order Cancelled";
        }
        else
        {
            statusStr = "PENDING";
            displayStatus = "Payment Pending";
        }

        decimal netAmountPaid = (payment.Status == PaymentStatus.Paid || payment.Status == PaymentStatus.PartiallyRefunded)
            ? Math.Max(0, payment.Amount - totalRefundedAmount)
            : 0;
        decimal remainingRefundableAmount = payment.Status == PaymentStatus.Paid
            ? Math.Max(0, payment.Amount - totalRefundedAmount)
            : 0;

        // 5. Unique Stable Receipt Number
        var receiptNumber = $"ETH-RCP-{payment.CreatedAt:yyyyMM}-{payment.Id.ToString()[..8].ToUpper()}";

        // 6. Audit Trail
        _auditService.AddAuditLog(
            adminUserId,
            "PAYMENT_RECEIPT_GENERATED",
            "PaymentTransaction",
            payment.Id,
            $"Generated commercial payment receipt {receiptNumber} for amount {payment.Amount:F2} INR.");

        await _db.SaveChangesAsync(cancellationToken);

        return new AdminPaymentReceiptResponse
        {
            ReceiptNumber = receiptNumber,
            ReceiptDate = payment.PaidAt ?? payment.CreatedAt,
            Studio = new AdminReceiptStudioDetails(),
            Customer = new AdminReceiptCustomerDetails
            {
                UserId = payment.UserId,
                FullName = payment.User?.FullName ?? "Valued Customer",
                Phone = payment.User?.Phone ?? "—",
                Email = payment.User?.Email,
                CustomerCode = customerCode
            },
            Transaction = new AdminReceiptTransactionDetails
            {
                TransactionId = payment.Id,
                Purpose = (int)payment.Purpose,
                PurposeName = payment.Purpose.ToString(),
                ReferenceId = payment.ReferenceId.ToString(),
                ItemName = itemName,
                ItemDetails = itemDetails,
                ItemDescription = itemDescription,
                Quantity = 1,
                UnitAmount = payment.Amount,
                RazorpayOrderId = payment.RazorpayOrderId,
                RazorpayPaymentId = payment.RazorpayPaymentId,
                CreatedAt = payment.CreatedAt,
                PaidAt = payment.PaidAt,
                PaymentMethod = !string.IsNullOrWhiteSpace(payment.RazorpayPaymentId) ? "Online / Razorpay" : "Online Checkout",
                IsGatewayVerified = isGatewayVerified,
                IsReconciled = isReconciled,
                DisplayStatus = displayStatus
            },
            Subtotal = payment.Amount,
            TotalRefundedAmount = totalRefundedAmount,
            NetAmountPaid = netAmountPaid,
            RemainingRefundableAmount = remainingRefundableAmount,
            TotalAmount = payment.Amount,
            Currency = payment.Currency,
            Status = statusStr,
            DocumentType = "Official Commercial Payment Receipt (Non-GST)",
            Disclaimer = "Ethos Dance Studio is not registered under GST. This document serves as an official commercial payment receipt for studio services rendered.",
            SystemGeneratedNotice = "This is a computer-generated commercial receipt and does not require a physical signature."
        };
    }
}