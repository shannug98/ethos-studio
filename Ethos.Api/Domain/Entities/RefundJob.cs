using System;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;

namespace Ethos.Api.Domain.Entities;

public class RefundJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkshopId { get; set; }

    public Guid BookingId { get; set; }

    public Guid PaymentTransactionId { get; set; }

    public Guid? PaymentRefundId { get; set; }

    public long AmountPaise { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid InitiatedByAdminId { get; set; }

    public RefundStatus Status { get; set; } = RefundStatus.Requested;

    public int RetryCount { get; set; } = 0;

    public DateTime? NextRetryUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAtUtc { get; set; }

    public string? LastError { get; set; }

    // Navigation properties
    public Workshop? Workshop { get; set; }

    public WorkshopBooking? Booking { get; set; }

    public PaymentTransaction? PaymentTransaction { get; set; }

    public PaymentRefund? PaymentRefund { get; set; }
}
