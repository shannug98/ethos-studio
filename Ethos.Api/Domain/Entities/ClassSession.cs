using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class ClassSession
{
    public Guid Id { get; set; }

    public Guid ClassScheduleId { get; set; }

    public DateTime SessionDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public ClassSessionStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ClassSchedule ClassSchedule { get; set; } = null!;
}
