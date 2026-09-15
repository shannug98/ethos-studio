using System.Text.Json;
using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Finance;

public class PaymentReconciliationService : IPaymentReconciliationService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;

    public PaymentReconciliationService(
        AppDbContext db,
        IAdminAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<AdminPaymentTransactionResponse> ReconcilePaymentAsync(
        Guid transactionId,
        Guid adminUserId,
        AdminReconcilePaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Reconciliation reason is required.");

        var payment = await _db.PaymentTransactions
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == transactionId, cancellationToken);

        if (payment == null)
            throw new ArgumentException("Payment transaction not found.");

        var targetStatus = (PaymentStatus)request.TargetStatus;

        // Enforce state machine transitions
        PaymentStateMachine.AssertTransition(payment.Status, targetStatus);

        // If target is Paid, require evidence (GatewayPaymentId or detailed reason)
        if (targetStatus == PaymentStatus.Paid)
        {
            if (string.IsNullOrWhiteSpace(request.GatewayPaymentId) && string.IsNullOrWhiteSpace(payment.RazorpayPaymentId))
            {
                throw new InvalidOperationException("Cannot reconcile to PAID without verified gateway payment ID evidence.");
            }
        }

        var previousStatus = payment.Status.ToString();

        payment.Status = targetStatus;
        if (!string.IsNullOrWhiteSpace(request.GatewayPaymentId))
        {
            payment.RazorpayPaymentId = request.GatewayPaymentId.Trim();
        }

        if (targetStatus == PaymentStatus.Paid && !payment.PaidAt.HasValue)
        {
            payment.PaidAt = DateTime.UtcNow;
        }

        payment.UpdatedAt = DateTime.UtcNow;

        var syncPayload = JsonSerializer.Serialize(new
        {
            PreviousStatus = previousStatus,
            TargetStatus = targetStatus.ToString(),
            request.Reason,
            request.GatewayPaymentId,
            request.GatewayPayload,
            request.Notes,
            ReconciledByAdminId = adminUserId,
            ReconciledAt = DateTime.UtcNow
        });

        var syncEvent = new PaymentEvent
        {
            Id = Guid.NewGuid(),
            PaymentTransactionId = payment.Id,
            EventType = "RECONCILIATION_SYNC",
            Payload = syncPayload,
            CreatedAt = DateTime.UtcNow
        };

        _db.PaymentEvents.Add(syncEvent);

        _auditService.AddAuditLog(
            adminUserId,
            "PAYMENT_RECONCILED",
            "PaymentTransaction",
            payment.Id,
            $"Reconciled payment {payment.Id} from {previousStatus} to {targetStatus}. Reason: {request.Reason}");

        await _db.SaveChangesAsync(cancellationToken);

        string displayPurpose = payment.Purpose switch
        {
            PaymentPurpose.PackagePurchase => "Dance Package Purchase",
            PaymentPurpose.WorkshopBooking => "Workshop Registration",
            PaymentPurpose.TrainerApplication => "Trainer Onboarding Fee",
            PaymentPurpose.TrainerTierUpgrade => "Trainer Tier Upgrade",
            _ => "Studio Payment"
        };

        string displayStatus = payment.Status switch
        {
            PaymentStatus.Paid => "Payment Successful",
            PaymentStatus.Failed => "Payment Failed",
            PaymentStatus.Cancelled => "Order Cancelled",
            PaymentStatus.Refunded => "Fully Refunded",
            PaymentStatus.PartiallyRefunded => "Partially Refunded",
            _ => payment.Status.ToString()
        };

        return new AdminPaymentTransactionResponse
        {
            Id = payment.Id,
            UserId = payment.UserId,
            UserName = payment.User?.FullName ?? "Unknown User",
            UserPhone = payment.User?.Phone ?? "",
            UserCustomerCode = payment.User?.CustomerCode ?? ("CUST-" + payment.UserId.ToString()[..6].ToUpper()),
            Purpose = (int)payment.Purpose,
            PurposeName = payment.Purpose.ToString(),
            ReferenceId = payment.ReferenceId.ToString(),
            ItemName = displayPurpose,
            ItemDetails = $"Ref: {payment.ReferenceId.ToString()[..8]}",
            DisplayPurpose = displayPurpose,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = (int)payment.Status,
            StatusName = payment.Status.ToString(),
            DisplayStatus = displayStatus,
            RazorpayOrderId = payment.RazorpayOrderId,
            RazorpayPaymentId = payment.RazorpayPaymentId,
            IsGatewayVerified = !string.IsNullOrWhiteSpace(payment.RazorpayPaymentId),
            IsReconciled = true,
            TotalRefundedAmount = 0m,
            RemainingRefundableAmount = payment.Amount,
            CreatedAt = payment.CreatedAt,
            PaidAt = payment.PaidAt
        };
    }
}