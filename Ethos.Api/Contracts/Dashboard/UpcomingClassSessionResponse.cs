using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Dashboard;

public class UpcomingClassSessionResponse
{
    public Guid SessionId { get; set; }

    public Guid DanceClassId { get; set; }

    public string DanceClassName { get; set; } = string.Empty;

    public string DanceStyle { get; set; } = string.Empty;

    public DateTime SessionDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public string? StudioRoom { get; set; }

    public ClassSessionStatus Status { get; set; }
}
