using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Workshops;

public class WorkshopResponse
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string DanceStyle { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public DateTime WorkshopDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public string Venue { get; set; } = string.Empty;

    public string? TrainerName { get; set; }

    public decimal StartingPrice { get; set; }

    public decimal CurrentPrice { get; set; }

    public decimal StudentPrice { get; set; }

    public decimal Price { get; set; }

    public int Capacity { get; set; }

    public int BookedSeats { get; set; }

    public int RemainingSeats { get; set; }

    public bool IsFull { get; set; }

    public bool IsStudentEligible { get; set; }

    public int BookingsCount { get; set; }

    public WorkshopStatus Status { get; set; }

    public string? ImageUrl { get; set; }
}
