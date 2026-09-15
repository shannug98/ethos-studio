namespace Ethos.Api.Domain.Entities;

public class ClassSchedule
{
    public Guid Id { get; set; }

    public Guid DanceClassId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public string? StudioRoom { get; set; }

    public int Capacity { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DanceClass DanceClass { get; set; } = null!;

    public ICollection<ClassSession> Sessions { get; set; }
        = new List<ClassSession>();
}
