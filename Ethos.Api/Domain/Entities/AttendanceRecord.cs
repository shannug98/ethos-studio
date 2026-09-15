using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class AttendanceRecord
{
    public Guid Id { get; set; }

    public Guid ClassSessionId { get; set; }

    public Guid StudentProfileId { get; set; }

    public AttendanceStatus Status { get; set; }

    public DateTime MarkedAt { get; set; }

    public Guid? MarkedByUserId { get; set; }

    public string? Notes { get; set; }

    public ClassSession ClassSession { get; set; } = null!;

    public StudentProfile StudentProfile { get; set; } = null!;
}
