namespace Ethos.Api.Contracts.Classes;

public class ClassScheduleResponse
{
    public Guid Id { get; set; }

    public Guid DanceClassId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public string? StudioRoom { get; set; }

    public int Capacity { get; set; }
}
