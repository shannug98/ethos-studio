using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class FeedbackQuestion
{
    public Guid Id { get; set; }

    public Guid FeedbackFormVersionId { get; set; }

    public string QuestionKey { get; set; } = string.Empty;

    public string PromptText { get; set; } = string.Empty;

    public FeedbackQuestionType QuestionType { get; set; } = FeedbackQuestionType.Rating1To5;

    public FeedbackAudienceType TargetAudience { get; set; } = FeedbackAudienceType.Both;

    /// <summary>
    /// Optional JSON array of choices for SingleChoice questions (e.g. No-Show reasons).
    /// </summary>
    public string? OptionsJson { get; set; }

    public bool IsRequired { get; set; } = true;

    public int SortOrder { get; set; } = 0;

    public FeedbackFormVersion FormVersion { get; set; } = null!;

    public ICollection<WorkshopFeedbackAnswer> Answers { get; set; } = new List<WorkshopFeedbackAnswer>();
}
