using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class WorkshopFeedbackToken
{
    public Guid Id { get; set; }

    public Guid WorkshopBookingId { get; set; }

    /// <summary>
    /// Cryptographic SHA-256 hash or token key of the single-use guest link.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    public FeedbackAudienceType AudienceType { get; set; } = FeedbackAudienceType.Attended;

    public Guid? FeedbackFormVersionId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public WorkshopBooking WorkshopBooking { get; set; } = null!;

    public FeedbackFormVersion? FeedbackFormVersion { get; set; }

    public WorkshopFeedback? WorkshopFeedback { get; set; }
}
