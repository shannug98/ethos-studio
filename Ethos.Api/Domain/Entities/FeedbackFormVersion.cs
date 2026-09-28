namespace Ethos.Api.Domain.Entities;

public class FeedbackFormVersion
{
    public Guid Id { get; set; }

    public Guid WorkshopFeedbackSettingId { get; set; }

    public int VersionNumber { get; set; } = 1;

    public bool IsFrozen { get; set; } = false;

    public DateTime? FrozenAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public WorkshopFeedbackSetting FeedbackSetting { get; set; } = null!;

    public ICollection<FeedbackQuestion> Questions { get; set; } = new List<FeedbackQuestion>();

    public ICollection<WorkshopFeedback> Feedbacks { get; set; } = new List<WorkshopFeedback>();
}
