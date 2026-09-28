namespace Ethos.Api.Application.Feedback;

public class FeedbackEligibilityEvaluationResult
{
    public Guid? WorkshopId { get; set; }
    public int WorkshopsProcessed { get; set; }
    public int TotalBookingsEvaluated { get; set; }
    public int EligibleAttendedCount { get; set; }
    public int EligibleNoShowCount { get; set; }
    public int ExcludedCount { get; set; }
    public int PendingNotEndedCount { get; set; }
    public int TokensGeneratedCount { get; set; }
    public int NotificationsQueuedCount { get; set; }
    public List<string> Logs { get; set; } = new();
}

public interface IFeedbackEligibilityService
{
    Task<FeedbackEligibilityEvaluationResult> EvaluateWorkshopBookingsAsync(
        Guid workshopId,
        DateTime? nowUtc = null,
        CancellationToken cancellationToken = default);

    Task<FeedbackEligibilityEvaluationResult> ProcessCompletedWorkshopsAsync(
        DateTime? nowUtc = null,
        CancellationToken cancellationToken = default);
}
