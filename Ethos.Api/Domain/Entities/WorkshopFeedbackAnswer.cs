namespace Ethos.Api.Domain.Entities;

public class WorkshopFeedbackAnswer
{
    public Guid Id { get; set; }

    public Guid WorkshopFeedbackId { get; set; }

    public Guid FeedbackQuestionId { get; set; }

    public int? NumericValue { get; set; }

    public string? TextValue { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public WorkshopFeedback WorkshopFeedback { get; set; } = null!;

    public FeedbackQuestion FeedbackQuestion { get; set; } = null!;
}
