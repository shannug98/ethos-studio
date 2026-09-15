namespace Ethos.Api.Domain.Entities;

public class TrainerAvailability
{
    public Guid Id { get; set; }

    public Guid TrainerProfileId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public bool IsAvailable { get; set; }

    public TrainerProfile TrainerProfile { get; set; } = null!;
}
