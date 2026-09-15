namespace Ethos.Api.Contracts.Feedback;

public class StudentFeedbackResponse
{
    public Guid FeedbackId { get; set; }

    public string Type { get; set; } = string.Empty; // "CLASS" or "WORKSHOP"

    public Guid ItemId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? TrainerName { get; set; }

    public DateTime EventDate { get; set; }

    public int OverallRating { get; set; }

    public Dictionary<string, int> Criteria { get; set; } = new();

    public string? LikedAspects { get; set; }

    public string? Improvements { get; set; }

    public string? Recommendation { get; set; }

    public DateTime SubmittedAt { get; set; }
}
