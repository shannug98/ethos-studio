namespace Ethos.Api.Domain.Entities;

public class ClassFeedback
{
    public Guid Id { get; set; }

    public Guid ClassEnrollmentId { get; set; }

    public Guid DanceClassId { get; set; }

    public Guid StudentProfileId { get; set; }

    public int OverallRating { get; set; }

    public int TeachingQuality { get; set; }

    public int ExplanationClarity { get; set; }

    public int TrainerEngagement { get; set; }

    public int ClassPace { get; set; }

    public int ChoreographyContent { get; set; }

    public int DifficultyLevel { get; set; }

    public int ClassExperience { get; set; }

    public string? LikedAspects { get; set; }

    public string? Improvements { get; set; }

    public string WouldAttendAgain { get; set; } = "Yes";

    public DateTime SubmittedAt { get; set; }

    public ClassEnrollment ClassEnrollment { get; set; } = null!;

    public DanceClass DanceClass { get; set; } = null!;

    public StudentProfile StudentProfile { get; set; } = null!;
}
