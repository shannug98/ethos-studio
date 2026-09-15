namespace Ethos.Api.Contracts.Trainers;

public class TrainerWorkshopResponse
{
    public Guid Id { get; set; }

    public string? WorkshopReference { get; set; }

    public Guid TrainerProfileId { get; set; }

    public string TrainerName { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string DanceStyle { get; set; } = null!;

    public string Level { get; set; } = null!;

    public DateTime WorkshopDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public string Venue { get; set; } = null!;

    public decimal? TrainerProposedPrice { get; set; }

    public decimal? AdminApprovedPrice { get; set; }

    public decimal Price { get; set; }

    public int Capacity { get; set; }

    public int BookedCount { get; set; }

    public int EnrolledStudentsCount { get; set; }

    public string Status { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; }
}
