namespace Ethos.Api.Contracts.Trainers;

public class TrainerWorkshopFeedbackResponse
{
    public Guid Id { get; set; }
    public string StudentName { get; set; } = null!;
    public string WorkshopTitle { get; set; } = string.Empty;
    public DateTime? WorkshopDate { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime SubmittedAt { get => CreatedAt; set => CreatedAt = value; }
}
