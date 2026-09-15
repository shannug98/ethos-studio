using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class PaymentTransaction
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public PaymentPurpose Purpose { get; set; }

    public Guid ReferenceId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "INR";

    public PaymentStatus Status { get; set; }

    public string? RazorpayOrderId { get; set; }

    public string? RazorpayPaymentId { get; set; }

    public string? RazorpaySignature { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;

    public ICollection<PaymentEvent> Events { get; set; }
        = new List<PaymentEvent>();
}
