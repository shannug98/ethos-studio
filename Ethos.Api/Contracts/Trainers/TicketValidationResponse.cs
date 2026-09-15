using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Trainers;

public class TicketValidationResponse
{
    public bool IsValid { get; set; }
    public string ValidationStatus { get; set; } = string.Empty; // Valid, AlreadyCheckedIn, InvalidWorkshop, CancelledOrRefunded, Expired, NotFound
    public string Message { get; set; } = string.Empty;

    public Guid? TicketId { get; set; }
    public string? TicketNumber { get; set; }
    public string? AttendeeName { get; set; }
    public string? MaskedPhone { get; set; }
    public bool IsPrimaryAttendee { get; set; }
    public TicketStatus? Status { get; set; }

    public DateTime? CheckedInAt { get; set; }
    public bool IsCurrentlyInside { get; set; }

    public Guid? WorkshopBookingId { get; set; }
    public int GroupTotalTickets { get; set; }
    public int GroupCheckedInCount { get; set; }
    public List<GroupTicketSummaryDto> GroupTickets { get; set; } = new();
}

public class GroupTicketSummaryDto
{
    public Guid TicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string AttendeeName { get; set; } = string.Empty;
    public bool IsPrimaryAttendee { get; set; }
    public bool IsCheckedIn { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public TicketStatus Status { get; set; }
    public bool IsEligibleForCheckIn { get; set; }
}
