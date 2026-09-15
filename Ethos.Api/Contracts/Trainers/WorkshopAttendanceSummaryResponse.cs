namespace Ethos.Api.Contracts.Trainers;

public class WorkshopAttendanceSummaryResponse
{
    public Guid WorkshopId { get; set; }
    public string WorkshopTitle { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int TotalTicketsIssued { get; set; }
    public int TotalCheckedIn { get; set; }
    public int CurrentlyInside { get; set; }
    public decimal AttendancePercentage { get; set; }
    public bool AllowReEntry { get; set; }
    public List<WorkshopAttendeeListItemDto> Attendees { get; set; } = new();
}

public class WorkshopAttendeeListItemDto
{
    public Guid TicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public Guid WorkshopBookingId { get; set; }
    public string AttendeeName { get; set; } = string.Empty;
    public string? MaskedPhone { get; set; }
    public bool IsPrimaryAttendee { get; set; }
    public bool IsCheckedIn { get; set; }
    public bool IsCurrentlyInside { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public string? CheckInMethod { get; set; }
    public string Status { get; set; } = string.Empty;
}
