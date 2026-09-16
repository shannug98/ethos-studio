namespace Ethos.Api.Contracts.Admin;

public class AdminCheckInTicketRequest
{
    public string? QrToken { get; set; }
    public string? TicketNumber { get; set; }
    public string? Notes { get; set; }
}

public class AdminCheckInTicketResponse
{
    public bool Success { get; set; }
    public string Code { get; set; } = null!; // SUCCESS, INVALID_TOKEN, TICKET_NOT_FOUND, WRONG_WORKSHOP, ALREADY_CHECKED_IN, BOOKING_CANCELLED, PAYMENT_NOT_CONFIRMED, TICKET_CANCELLED, WORKSHOP_NOT_OPEN, UNAUTHORIZED
    public string Message { get; set; } = null!;
    public Guid? WorkshopId { get; set; }
    public Guid? TicketId { get; set; }
    public string? AttendeeName { get; set; }
    public string? TicketNumber { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public string? CheckInMethod { get; set; }
}

public class AdminWorkshopRecentCheckInDto
{
    public Guid TicketId { get; set; }
    public string AttendeeName { get; set; } = null!;
    public string TicketNumber { get; set; } = null!;
    public DateTime CheckedInAt { get; set; }
    public string FormattedTime { get; set; } = null!;
    public string CheckInMethod { get; set; } = "QR";
}

public class AdminWorkshopOverviewResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string WorkshopReference { get; set; } = null!;
    public string DanceStyle { get; set; } = null!;
    public string Level { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime WorkshopDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string FormattedSchedule { get; set; } = null!;
    public string Venue { get; set; } = null!;
    public decimal Price { get; set; }
    public int Capacity { get; set; }
    public int BookedCount { get; set; }
    public int AttendedCount { get; set; }
    public double CapacityPercentage { get; set; }
    public double CheckInPercentage { get; set; }
    public decimal TotalRevenue { get; set; }
    public string TrainerName { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public List<AdminWorkshopRecentCheckInDto> RecentCheckIns { get; set; } = new();
}

public class AdminWorkshopAttendeeDto
{
    public Guid TicketId { get; set; }
    public Guid BookingId { get; set; }
    public string AttendeeName { get; set; } = null!;
    public string AttendeePhoneMasked { get; set; } = null!;
    public string? AttendeeEmailMasked { get; set; }
    public string TicketNumber { get; set; } = null!;
    public string BookingReference { get; set; } = null!;
    public string BookingStatus { get; set; } = null!;
    public string PaymentStatus { get; set; } = null!;
    public bool IsCheckedIn { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public string? FormattedCheckedInAt { get; set; }
    public string? CheckInMethod { get; set; }
    public bool IsGuest { get; set; }
    public string AttendeeType { get; set; } = null!;
}

public class AdminWorkshopFeedbackDto
{
    public Guid Id { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string StudentNameMasked { get; set; } = null!;
    public DateTime SubmittedAt { get; set; }
    public string FormattedDate { get; set; } = null!;
}

public class AdminWorkshopCountsDto
{
    public int PendingReview { get; set; }
    public int Upcoming { get; set; }
    public int Ongoing { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int All { get; set; }
}
