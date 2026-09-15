namespace Ethos.Api.Contracts.Admin;

public class AdminWorkshopTicketResponse
{
    public Guid TicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public Guid WorkshopBookingId { get; set; }
    public string AttendeeName { get; set; } = string.Empty;
    public string? AttendeePhone { get; set; }
    public string? AttendeeEmail { get; set; }
    public bool IsPrimaryAttendee { get; set; }
    public bool IsCheckedIn { get; set; }
    public bool IsCurrentlyInside { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public string? CheckInMethod { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
}
