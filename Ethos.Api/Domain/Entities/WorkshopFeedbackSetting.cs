namespace Ethos.Api.Domain.Entities;

public class WorkshopFeedbackSetting
{
    public Guid Id { get; set; }

    public Guid WorkshopId { get; set; }

    public bool IsFeedbackEnabled { get; set; } = true;

    /// <summary>
    /// Explicit timestamp when feedback configuration editing closes and the active version freezes.
    /// Defaults to 15 minutes before the earliest session scheduled end time.
    /// </summary>
    public DateTime? ConfigCutoffUtc { get; set; }

    public bool IsLocked { get; set; } = false;

    public DateTime? LockedAtUtc { get; set; }

    public Guid? ActiveVersionId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Workshop Workshop { get; set; } = null!;

    public FeedbackFormVersion? ActiveVersion { get; set; }

    public ICollection<FeedbackFormVersion> FormVersions { get; set; } = new List<FeedbackFormVersion>();
}
