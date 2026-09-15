namespace Ethos.Api.Domain.Enums;

public enum TrainerApplicationStatus
{
    Draft = 1,
    Submitted = 2,
    PaymentPending = 3,
    PaymentVerified = 4,
    UnderReview = 5,
    ChangesRequested = 6,
    Approved = 7,
    Rejected = 8,
    Cancelled = 9
}
