namespace Ethos.Api.Domain.Enums;

public enum PaymentStatus
{
    Created = 1,
    OrderCreated = 2,
    PaymentPending = 3,
    Paid = 4,
    Failed = 5,
    Cancelled = 6,
    Refunded = 7,
    PartiallyRefunded = 8
}
