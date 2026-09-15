namespace Ethos.Api.Contracts.Trainers;

public class PerformanceMetricResponse
{
    public int Students { get; set; }
    public int Workshops { get; set; }
    public int Sessions { get; set; }
    public int Feedback { get; set; }
    public decimal? Rating { get; set; }
    public decimal AttendancePercentage { get; set; }
}

public class PerformancePeriodResponse : PerformanceMetricResponse
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}

public class TrainerRatingBreakdownResponse
{
    public int FiveStars { get; set; }
    public int FourStars { get; set; }
    public int ThreeStars { get; set; }
    public int TwoStars { get; set; }
    public int OneStar { get; set; }
}

public class TrainerPerformanceResponse
{
    public Guid Id { get; set; }

    public DateTime SnapshotDate { get; set; }

    public PerformanceMetricResponse Overall { get; set; } = new();

    public PerformancePeriodResponse Monthly { get; set; } = new();

    public TrainerRatingBreakdownResponse RatingBreakdown { get; set; } = new();

    // Top-level aliases for backwards compatibility with existing consumers
    public int SessionsConducted
    {
        get => Overall.Sessions;
        set => Overall.Sessions = value;
    }

    public int UniqueStudents
    {
        get => Overall.Students;
        set => Overall.Students = value;
    }

    public int AttendanceCount { get; set; }

    public decimal AttendancePercentage
    {
        get => Overall.AttendancePercentage;
        set => Overall.AttendancePercentage = value;
    }

    public int FeedbackCount
    {
        get => Overall.Feedback;
        set => Overall.Feedback = value;
    }

    public decimal? AverageFeedbackRating
    {
        get => Overall.Rating;
        set => Overall.Rating = value;
    }

    public int WorkshopsConducted
    {
        get => Overall.Workshops;
        set => Overall.Workshops = value;
    }

    public string? Notes { get; set; }

    // Frontend 7D Aliases
    public decimal OverallRating
    {
        get => Overall.Rating ?? 0;
        set => Overall.Rating = value;
    }

    public int TotalStudentsTaught
    {
        get => Overall.Students;
        set => Overall.Students = value;
    }

    public int TotalWorkshopsCompleted
    {
        get => Overall.Workshops;
        set => Overall.Workshops = value;
    }

    public decimal AttendanceRate
    {
        get => Overall.AttendancePercentage;
        set => Overall.AttendancePercentage = value;
    }

    public decimal StudentRetentionRate { get; set; }

    public int TotalFeedbacksReceived
    {
        get => Overall.Feedback;
        set => Overall.Feedback = value;
    }
}
