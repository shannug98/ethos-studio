namespace Ethos.Api.Domain.Entities;

public class TrainerPerformanceSnapshot
{
    public Guid Id { get; set; }

    public Guid TrainerProfileId { get; set; }

    public DateTime SnapshotDate { get; set; }

    public int SessionsConducted { get; set; }

    public int UniqueStudents { get; set; }

    public int AttendanceCount { get; set; }

    public decimal AttendancePercentage { get; set; }

    public int FeedbackCount { get; set; }

    public decimal? AverageFeedbackRating { get; set; }

    public int WorkshopsConducted { get; set; }

    public string? Notes { get; set; }

    public TrainerProfile TrainerProfile { get; set; } = null!;
}
