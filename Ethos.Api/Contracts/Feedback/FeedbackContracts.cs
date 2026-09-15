namespace Ethos.Api.Contracts.Feedback;

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
    public bool AlreadySubmitted { get; init; }
    public bool IsEligible { get; init; }
    public string? IneligibilityReason { get; init; }
}

public sealed class SubmitGuestWorkshopFeedbackRequest
{
    public string Token { get; init; } = string.Empty;
    public int Rating { get; init; }
    public string? Comment { get; init; }
}

public sealed class GenerateFeedbackTokenResponse
{
    public Guid BookingId { get; init; }
    public string Token { get; init; } = string.Empty;
    public string FeedbackUrl { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}
