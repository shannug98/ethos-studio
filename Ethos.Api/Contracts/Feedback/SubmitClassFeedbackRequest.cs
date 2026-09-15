using System.ComponentModel.DataAnnotations;

namespace Ethos.Api.Contracts.Feedback;

public class SubmitClassFeedbackRequest
{
    [Range(1, 5)]
    public int OverallRating { get; set; }

    [Range(1, 5)]
    public int TeachingQuality { get; set; }

    [Range(1, 5)]
    public int ExplanationClarity { get; set; }

    [Range(1, 5)]
    public int TrainerEngagement { get; set; }

    [Range(1, 5)]
    public int ClassPace { get; set; }

    [Range(1, 5)]
    public int ChoreographyContent { get; set; }

    [Range(1, 5)]
    public int DifficultyLevel { get; set; }

    [Range(1, 5)]
    public int ClassExperience { get; set; }

    [StringLength(2000)]
    public string? LikedAspects { get; set; }

    [StringLength(2000)]
    public string? Improvements { get; set; }

    [Required]
    public string WouldAttendAgain { get; set; } = "Yes";
}
