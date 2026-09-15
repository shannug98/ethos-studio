using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Attendance;

public class AttendanceRecordResponse
{
    public Guid Id { get; set; }

    public Guid ClassSessionId { get; set; }

    public DateTime SessionDate { get; set; }

    public string DanceClassName { get; set; } = string.Empty;

    public AttendanceStatus Status { get; set; }

    public DateTime MarkedAt { get; set; }

    public string? Notes { get; set; }
}
