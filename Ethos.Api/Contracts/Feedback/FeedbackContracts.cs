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

public sealed class AdminWorkshopFeedbackConfigResponse
{
    public Guid WorkshopId { get; init; }
    public string WorkshopTitle { get; init; } = string.Empty;
    public bool IsFeedbackEnabled { get; init; } = true;
    public DateTime? ConfigCutoffUtc { get; init; }
    public bool IsLocked { get; init; }
    public DateTime? LockedAtUtc { get; init; }
    public Guid? ActiveVersionId { get; init; }
    public int ActiveVersionNumber { get; init; } = 1;
    public bool ActiveVersionIsFrozen { get; init; }
    public IReadOnlyList<FeedbackQuestionDto> ActiveQuestions { get; init; } = Array.Empty<FeedbackQuestionDto>();
    public IReadOnlyList<AdminFeedbackFormVersionDto> Versions { get; init; } = Array.Empty<AdminFeedbackFormVersionDto>();
    public AdminFeedbackMetricsDto Metrics { get; init; } = new();
    public AdminFeedbackAutomationDto Automation { get; init; } = new();
    public IReadOnlyList<AdminFeedbackRecipientDto> Recipients { get; init; } = Array.Empty<AdminFeedbackRecipientDto>();
}

public sealed class AdminFeedbackFormVersionDto
{
    public Guid Id { get; init; }
    public int VersionNumber { get; init; }
    public bool IsFrozen { get; init; }
    public DateTime? FrozenAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public int QuestionCount { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<FeedbackQuestionDto> Questions { get; init; } = Array.Empty<FeedbackQuestionDto>();
}

public sealed class AdminFeedbackMetricsDto
{
    public int TotalBookings { get; init; }
    public int EligibleAttendedCount { get; init; }
    public int EligibleNoShowCount { get; init; }
    public int SubmittedCount { get; init; }
    public decimal ResponseRate { get; init; }
    public decimal AverageRating { get; init; }
    public int FiveStars { get; init; }
    public int FourStars { get; init; }
    public int ThreeStars { get; init; }
    public int TwoStars { get; init; }
    public int OneStar { get; init; }
}

public sealed class AdminFeedbackAutomationDto
{
    public string AttendedTemplateName { get; init; } = "ethos_feedback_attended";
    public string NoShowTemplateName { get; init; } = "ethos_feedback_noshow";
    public string TriggerCondition { get; init; } = "Workshop Concludes";
    public int PostEventDelayMinutes { get; init; } = 120;
    public bool IsAutomationActive { get; init; } = true;
    public int QueuedNotificationsCount { get; init; }
    public int SentNotificationsCount { get; init; }
    public int FailedNotificationsCount { get; init; }
}

public sealed class AdminFeedbackRecipientDto
{
    public Guid BookingId { get; init; }
    public string BookingReference { get; init; } = string.Empty;
    public string AttendeeName { get; init; } = string.Empty;
    public string? AttendeePhone { get; init; }
    public string AudienceType { get; init; } = "Attended"; // "Attended" | "NoShow"
    public string DeliveryStatus { get; init; } = "NotQueued"; // "NotQueued" | "Queued" | "Sent" | "Failed" | "Submitted"
    public DateTime? SentAtUtc { get; init; }
    public DateTime? SubmittedAtUtc { get; init; }
    public int? SubmittedRating { get; init; }
    public string? FeedbackUrl { get; init; }
}

public sealed class AdminSaveFeedbackVersionRequest
{
    public IReadOnlyList<AdminFeedbackQuestionInputDto> Questions { get; init; } = Array.Empty<AdminFeedbackQuestionInputDto>();
}

public sealed class AdminFeedbackQuestionInputDto
{
    public Guid? Id { get; init; }
    public string QuestionKey { get; init; } = string.Empty;
    public string PromptText { get; init; } = string.Empty;
    public FeedbackQuestionType QuestionType { get; init; } = FeedbackQuestionType.Rating1To5;
    public FeedbackAudienceType TargetAudience { get; init; } = FeedbackAudienceType.Both;
    public string? OptionsJson { get; init; }
    public IReadOnlyList<string>? Choices { get; init; }
    public bool IsRequired { get; init; } = true;
    public int SortOrder { get; init; }
}

public sealed class AdminUpdateFeedbackSettingRequest
{
    public bool IsFeedbackEnabled { get; init; } = true;
    public DateTime? ConfigCutoffUtc { get; init; }
}

public sealed class AdminResendFeedbackRequest
{
    public string Audience { get; init; } = "All"; // "All" | "Attended" | "NoShow"
    public IReadOnlyList<Guid>? RecipientBookingIds { get; init; }
}

public sealed class AdminResendFeedbackResponse
{
    public int QueuedCount { get; init; }
    public int SkippedCount { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class AdminWorkshopFeedbackAnalyticsResponse
{
    public Guid WorkshopId { get; init; }
    public string WorkshopTitle { get; init; } = string.Empty;
    public AdminFeedbackMetricsDto Metrics { get; init; } = new();
    public IReadOnlyList<AdminQuestionAnalyticsDto> QuestionAnalytics { get; init; } = Array.Empty<AdminQuestionAnalyticsDto>();
    public IReadOnlyList<AdminWorkshopFeedbackSubmissionDetailDto> Submissions { get; init; } = Array.Empty<AdminWorkshopFeedbackSubmissionDetailDto>();
}

public sealed class AdminQuestionAnalyticsDto
{
    public Guid QuestionId { get; init; }
    public string QuestionKey { get; init; } = string.Empty;
    public string PromptText { get; init; } = string.Empty;
    public FeedbackQuestionType QuestionType { get; init; }
    public FeedbackAudienceType TargetAudience { get; init; }
    public int TotalAnswers { get; init; }
    public decimal? AverageRating { get; init; }
    public Dictionary<string, int>? ChoiceCounts { get; init; }
    public IReadOnlyList<string>? TextAnswers { get; init; }
}

public sealed class AdminWorkshopFeedbackSubmissionDetailDto
{
    public Guid FeedbackId { get; init; }
    public Guid? BookingId { get; init; }
    public string BookingReference { get; init; } = string.Empty;
    public string StudentNameMasked { get; init; } = string.Empty;
    public int Rating { get; init; }
    public string? Comment { get; init; }
    public bool? WouldRecommend { get; init; }
    public bool? WouldAttendTrainerAgain { get; init; }
    public string AudienceType { get; init; } = "Attended";
    public DateTime SubmittedAt { get; init; }
    public string FormattedDate { get; init; } = string.Empty;
    public IReadOnlyList<AdminFeedbackAnswerDetailDto> Answers { get; init; } = Array.Empty<AdminFeedbackAnswerDetailDto>();
}

public sealed class AdminFeedbackAnswerDetailDto
{
    public Guid QuestionId { get; init; }
    public string PromptText { get; init; } = string.Empty;
    public FeedbackQuestionType QuestionType { get; init; }
    public int? NumericValue { get; init; }
    public string? TextValue { get; init; }
}

