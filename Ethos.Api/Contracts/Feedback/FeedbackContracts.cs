using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Feedback;

public sealed class FeedbackQuestionDto
{
    public Guid Id { get; init; }
    public string QuestionKey { get; init; } = string.Empty;
    public string PromptText { get; init; } = string.Empty;
    public FeedbackQuestionType QuestionType { get; init; }
    public FeedbackAudienceType TargetAudience { get; init; }
    public string? OptionsJson { get; init; }
    public IReadOnlyList<string> Choices { get; init; } = Array.Empty<string>();
    public bool IsRequired { get; init; }
    public int SortOrder { get; init; }
}

public sealed class FeedbackAnswerSubmissionDto
{
    public Guid QuestionId { get; init; }
    public int? NumericValue { get; init; }
    public string? TextValue { get; init; }
}

public sealed class GuestWorkshopFeedbackDetailsResponse
{
    public Guid BookingId { get; init; }
    public string BookingReference { get; init; } = string.Empty;
    public string WorkshopTitle { get; init; } = string.Empty;
    public string TrainerName { get; init; } = string.Empty;
    public DateTime WorkshopDate { get; init; }
    public TimeSpan? StartTime { get; init; }
    public TimeSpan? EndTime { get; init; }
    public string? DanceStyle { get; init; }
    public string? Venue { get; init; }
    public FeedbackAudienceType AudienceType { get; init; } = FeedbackAudienceType.Attended;
    public int VersionNumber { get; init; } = 1;
    public Guid? FormVersionId { get; init; }
    public IReadOnlyList<FeedbackQuestionDto> Questions { get; init; } = Array.Empty<FeedbackQuestionDto>();
    public bool AlreadySubmitted { get; init; }
    public bool IsEligible { get; init; }
    public string? IneligibilityReason { get; init; }
}

public sealed class SubmitGuestWorkshopFeedbackRequest
{
    public string Token { get; init; } = string.Empty;
    public int? Rating { get; init; }
    public string? Comment { get; init; }
    public bool? WouldRecommend { get; init; }
    public bool? WouldAttendTrainerAgain { get; init; }
    public IReadOnlyList<FeedbackAnswerSubmissionDto>? Answers { get; init; }
}

public sealed class GenerateFeedbackTokenResponse
{
    public Guid BookingId { get; init; }
    public string Token { get; init; } = string.Empty;
    public string FeedbackUrl { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}

public sealed class AdminTrainerFeedbackDto
{
    public Guid FeedbackId { get; init; }
    public Guid WorkshopId { get; init; }
    public string WorkshopTitle { get; init; } = string.Empty;
    public DateTime WorkshopDate { get; init; }
    public int Rating { get; init; }
    public string? Comment { get; init; }
    public string BookingReference { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; }
}

public sealed class TrainerAggregateFeedbackDto
{
    public decimal AverageRating { get; init; }
    public int TotalReviews { get; init; }
    public int FiveStars { get; init; }
    public int FourStars { get; init; }
    public int ThreeStars { get; init; }
    public int TwoStars { get; init; }
    public int OneStar { get; init; }
}
