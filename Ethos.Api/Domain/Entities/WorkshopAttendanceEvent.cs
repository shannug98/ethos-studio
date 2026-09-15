using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class WorkshopAttendanceEvent
{
    public Guid Id { get; set; }

    public Guid WorkshopTicketId { get; set; }
    public Guid WorkshopId { get; set; }
    public Guid PerformedByUserId { get; set; }

    public AttendanceEventType EventType { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public CheckInMethod Method { get; set; }
    public string? Notes { get; set; }

    public WorkshopTicket WorkshopTicket { get; set; } = null!;
    public Workshop Workshop { get; set; } = null!;
    public User PerformedByUser { get; set; } = null!;
}
