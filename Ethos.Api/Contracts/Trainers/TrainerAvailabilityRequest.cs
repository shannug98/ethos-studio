namespace Ethos.Api.Contracts.Trainers;

public class TrainerAvailabilityRequest
{
    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public bool IsAvailable { get; set; }
}
