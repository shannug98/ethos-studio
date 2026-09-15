using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Workshops;

public class WorkshopTicketResponse
{
    public Guid Id { get; set; }

    public string TicketNumber { get; set; } = string.Empty;

    public Guid WorkshopBookingId { get; set; }

    public Guid WorkshopId { get; set; }

    public string WorkshopTitle { get; set; } = string.Empty;

    public DateTime WorkshopDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public string Venue { get; set; } = string.Empty;

    public string AttendeeName { get; set; } = string.Empty;

    public string? AttendeePhone { get; set; }

    public string? AttendeeEmail { get; set; }

    public bool IsPrimaryAttendee { get; set; }

    public TicketStatus Status { get; set; }

    public DateTime IssuedAt { get; set; }

    public DateTime? CheckedInAt { get; set; }

    public bool IsCheckedIn => CheckedInAt.HasValue;

    public bool IsDetailsLocked => CheckedInAt.HasValue || AttendeeDetailsLockedAt.HasValue;

    public DateTime? AttendeeDetailsLockedAt { get; set; }

    /// <summary>
    /// Cryptographic QR Token string. ONLY exposed to authorized ticket buyer/attendee in pass modal/receipt.
    /// NEVER returned on public or trainer roster endpoints.
    /// </summary>
    public string? QrToken { get; set; }
}
