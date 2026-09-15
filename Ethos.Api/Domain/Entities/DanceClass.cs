namespace Ethos.Api.Domain.Entities;

public class DanceClass
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string DanceStyle { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsArchived { get; set; } = false;

    public DateTime? ArchivedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<ClassSchedule> Schedules { get; set; }
        = new List<ClassSchedule>();

    public ICollection<ClassEnrollment> Enrollments { get; set; }
        = new List<ClassEnrollment>();
}
