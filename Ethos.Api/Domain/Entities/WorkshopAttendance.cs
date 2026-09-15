using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class WorkshopAttendance
{
    public Guid Id { get; set; }
    public Guid WorkshopTicketId { get; set; }
    public Guid WorkshopId { get; set; }
    public Guid CheckedInByUserId { get; set; }

    public DateTime FirstCheckedInAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastCheckedInAt { get; set; }
    public bool IsCurrentlyInside { get; set; } = true;

    public CheckInMethod Method { get; set; }
    public string? Notes { get; set; }
    public string? DeviceIp { get; set; }

    public WorkshopTicket WorkshopTicket { get; set; } = null!;
    public Workshop Workshop { get; set; } = null!;
    public User CheckedInByUser { get; set; } = null!;
}
