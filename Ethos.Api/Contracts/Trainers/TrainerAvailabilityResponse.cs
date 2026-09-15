namespace Ethos.Api.Contracts.Trainers;

public class TrainerAvailabilityResponse
{
    public Guid Id { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public bool IsAvailable { get; set; }
}
