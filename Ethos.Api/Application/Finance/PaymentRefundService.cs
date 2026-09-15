using System.Text.Json;
using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Finance;

public class PaymentRefundService : IPaymentRefundService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;

    public PaymentRefundService(
        AppDbContext db,
        IAdminAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<AdminRefundResponse> RecordExternalRefundAsync(
        Guid transactionId,
        Guid adminUserId,
        AdminRecordRefundRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request.RefundAmount <= 0)
            throw new ArgumentException("Refund amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Refund reason is required.");

        if (string.IsNullOrWhiteSpace(request.GatewayRefundId))
            throw new ArgumentException("Gateway refund ID/reference is required as proof of external refund.");

        var payment = await _db.PaymentTransactions
            .FirstOrDefaultAsync(p => p.Id == transactionId, cancellationToken);

        if (payment == null)
            throw new ArgumentException("Payment transaction not found.");

        if (payment.Status != PaymentStatus.Paid && payment.Status != PaymentStatus.PartiallyRefunded)
        {
            throw new InvalidOperationException($"Cannot refund payment with status '{payment.Status}'. Only Paid or PartiallyRefunded transactions are refundable.");
        }

        // Calculate previously refunded amounts from PaymentEvents
        var priorRefundEvents = await _db.PaymentEvents
            .AsNoTracking()
            .Where(e => e.PaymentTransactionId == transactionId && e.EventType == "EXTERNAL_REFUND_RECORDED")
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        decimal previouslyRefunded = 0m;
        foreach (var ev in priorRefundEvents)
        {
            if (!string.IsNullOrWhiteSpace(ev.Payload))
            {
                try
                {
                    using var doc = JsonDocument.Parse(ev.Payload);
                    if (doc.RootElement.TryGetProperty("RefundAmount", out var amtProp))
                    {
                        previouslyRefunded += amtProp.GetDecimal();
                    }

                    // Idempotency check on GatewayRefundId or IdempotencyKey
                    if (doc.RootElement.TryGetProperty("GatewayRefundId", out var gwIdProp) &&
                        gwIdProp.GetString() == request.GatewayRefundId)
                    {
                        // Duplicate request with same gateway reference - return idempotent response
                        var rem = payment.Amount - previouslyRefunded;
                        return new AdminRefundResponse
                        {
                            TransactionId = payment.Id,
                            OriginalAmount = payment.Amount,
                            PreviouslyRefunded = previouslyRefunded - amtProp.GetDecimal(),
                            RefundedAmount = amtProp.GetDecimal(),
                            RemainingRefundable = rem,
                            Status = payment.Status.ToString(),
                            GatewayRefundId = request.GatewayRefundId,
                            RefundedAt = ev.CreatedAt,
                            Reason = request.Reason
                        };
                    }
                }
                catch
                {
                    // Ignore JSON parse errors for non-standard payloads
                }
            }
        }

        var remainingRefundable = payment.Amount - previouslyRefunded;

        if (request.RefundAmount > remainingRefundable)
        {
            throw new InvalidOperationException($"Refund amount ({request.RefundAmount:F2} INR) exceeds remaining refundable balance ({remainingRefundable:F2} INR).");
        }

        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var newRemaining = remainingRefundable - request.RefundAmount;
        var targetStatus = newRemaining <= 0 ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;

        PaymentStateMachine.AssertTransition(payment.Status, targetStatus);
        payment.Status = targetStatus;
        payment.UpdatedAt = DateTime.UtcNow;

        var refundPayload = JsonSerializer.Serialize(new
        {
            request.RefundAmount,
            request.GatewayRefundId,
            request.Reason,
            request.Notes,
            OriginalAmount = payment.Amount,
            PreviouslyRefunded = previouslyRefunded,
            RemainingRefundable = newRemaining,
            IdempotencyKey = idempotencyKey,
            RecordedByAdminId = adminUserId,
            RecordedAt = DateTime.UtcNow
        });

        var refundEvent = new PaymentEvent
        {
            Id = Guid.NewGuid(),
            PaymentTransactionId = payment.Id,
            EventType = "EXTERNAL_REFUND_RECORDED",
            Payload = refundPayload,
            CreatedAt = DateTime.UtcNow
        };

        _db.PaymentEvents.Add(refundEvent);

        _auditService.AddAuditLog(
            adminUserId,
            "PAYMENT_REFUNDED",
            "PaymentTransaction",
            payment.Id,
            $"Recorded external refund of {request.RefundAmount:F2} INR. Gateway Ref: {request.GatewayRefundId}. Reason: {request.Reason}. Remaining: {newRemaining:F2} INR.");

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new AdminRefundResponse
        {
            TransactionId = payment.Id,
            OriginalAmount = payment.Amount,
            PreviouslyRefunded = previouslyRefunded,
            RefundedAmount = request.RefundAmount,
            RemainingRefundable = newRemaining,
            Status = payment.Status.ToString(),
            GatewayRefundId = request.GatewayRefundId,
            RefundedAt = refundEvent.CreatedAt,
            Reason = request.Reason
        };
    }
}