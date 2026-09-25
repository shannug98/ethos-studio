using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class WorkshopBookingSession
{
    public Guid Id { get; set; }

    public Guid WorkshopBookingId { get; set; }

    public Guid WorkshopSessionId { get; set; }

    public Guid? WorkshopTicketId { get; set; }

    public WorkshopBookingSessionStatus Status { get; set; } = WorkshopBookingSessionStatus.Booked;

    public Guid? OriginalSessionId { get; set; }

    public DateTime? ReplacedAt { get; set; }

    public Guid? ReplacedByAdminId { get; set; }

    public bool CutoffOverrideUsed { get; set; }

    public string? OverrideReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public WorkshopBooking WorkshopBooking { get; set; } = null!;

    public WorkshopSession WorkshopSession { get; set; } = null!;

    public WorkshopTicket? WorkshopTicket { get; set; }
}
