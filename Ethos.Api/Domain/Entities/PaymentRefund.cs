using System;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;

namespace Ethos.Api.Domain.Entities;

public class PaymentRefund
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    public Guid BookingId { get; set; }

    public long AmountPaise { get; set; }

    public string Currency { get; set; } = "INR";

    public RefundStatus Status { get; set; } = RefundStatus.Requested;

    public string? RazorpayRefundId { get; set; }

    public string? RazorpayPaymentId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid InitiatedByAdminId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessingStartedAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public DateTime? LastAttemptAtUtc { get; set; }

    public int AttemptCount { get; set; } = 0;

    public string? FailureReason { get; set; }

    // Navigation properties
    public PaymentTransaction? PaymentTransaction { get; set; }

    public WorkshopBooking? Booking { get; set; }
}
