using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Payments;

public class PaymentTransactionResponse
{
    public Guid Id { get; set; }

    public PaymentPurpose Purpose { get; set; }

    public Guid ReferenceId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "INR";

    public PaymentStatus Status { get; set; }

    public string? RazorpayOrderId { get; set; }

    public string? RazorpayPaymentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }
}
