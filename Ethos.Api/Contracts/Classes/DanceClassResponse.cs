namespace Ethos.Api.Contracts.Classes;

public class DanceClassResponse
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

    public int TotalEnrollments { get; set; }

    public int TotalFeedbacks { get; set; }

    public int ActiveSchedulesCount { get; set; }

    public List<ClassScheduleResponse> Schedules { get; set; } = [];
}
