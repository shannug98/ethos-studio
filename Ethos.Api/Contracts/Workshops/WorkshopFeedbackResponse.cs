namespace Ethos.Api.Contracts.Workshops;

public class WorkshopFeedbackResponse
{
    public Guid Id { get; set; }

    public Guid WorkshopId { get; set; }

    public string WorkshopTitle { get; set; } = string.Empty;

    public int Rating { get; set; }

    public int? TeachingRating { get; set; }

    public int? EnergyRating { get; set; }

    public int? ContentRating { get; set; }

    public string? Comment { get; set; }

    public bool WouldRecommend { get; set; }

    public DateTime SubmittedAt { get; set; }
}
