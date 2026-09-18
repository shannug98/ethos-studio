namespace Ethos.Api.Domain.Enums;

public enum WhatsAppNotificationStatus
{
    Pending = 1,
    Sending = 2,
    Sent = 3,
    Failed = 4,
    AmbiguousTimeout = 5,
    NeedsManualReview = 6,
    Skipped = 7
}
