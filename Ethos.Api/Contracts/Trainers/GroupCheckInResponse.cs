namespace Ethos.Api.Contracts.Trainers;

public class GroupCheckInResponse
{
    public Guid BookingId { get; set; }
    public int TotalTickets { get; set; }
    public int SuccessfullyCheckedIn { get; set; }
    public int AlreadyCheckedIn { get; set; }
    public int Failed { get; set; }
    public List<GroupCheckInItemResult> Results { get; set; } = new();
}

public class GroupCheckInItemResult
{
    public Guid TicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string AttendeeName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}
