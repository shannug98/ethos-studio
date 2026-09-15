namespace Ethos.Api.Contracts.Trainers;

public class TrainerDashboardResponse
{
    public TrainerResponse Trainer { get; set; } = null!;

    public int StudentCount { get; set; }

    public int UpcomingClassCount { get; set; }

    public int UpcomingWorkshopCount { get; set; }

    public decimal AttendancePercentage { get; set; }

    public int FeedbackCount { get; set; }

    public decimal? AverageFeedbackRating { get; set; }

    public int PendingUpgradeRequests { get; set; }

    public int UnreadNotificationCount { get; set; }

    public int TotalWorkshops { get; set; }

    public IReadOnlyList<TrainerWorkshopResponse> UpcomingWorkshops { get; set; } = [];
}
