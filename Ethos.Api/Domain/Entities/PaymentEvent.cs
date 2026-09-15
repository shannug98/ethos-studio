namespace Ethos.Api.Domain.Entities;

public class PaymentEvent
{
    public Guid Id { get; set; }

    public Guid PaymentTransactionId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string? Payload { get; set; }

    public DateTime CreatedAt { get; set; }

    public PaymentTransaction PaymentTransaction { get; set; } = null!;
}
