namespace Ethos.Api.Contracts.Trainers;

public class TrainerWorkshopRequest
{
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string DanceStyle { get; set; } = null!;

    public string Level { get; set; } = null!;

    public DateTime WorkshopDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public string Venue { get; set; } = null!;

    public decimal ProposedPrice { get; set; }

    public int Capacity { get; set; }

    public string? ImageUrl { get; set; }
}
