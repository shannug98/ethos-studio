using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class WorkshopTicket
{
    public Guid Id { get; set; }

    public string TicketNumber { get; set; } = null!;

    // Cryptographic hash (SHA-256) of the raw QR token
    public string QrTokenHash { get; set; } = null!;

    public Guid WorkshopBookingId { get; set; }
    public Guid WorkshopId { get; set; }
    public Guid UserId { get; set; }
    public Guid PaymentTransactionId { get; set; }

    public string AttendeeName { get; set; } = null!;
    public string? AttendeePhone { get; set; }
    public string? AttendeeEmail { get; set; }

    public bool IsPrimaryAttendee { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.Issued;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CheckedInAt { get; set; }
    public Guid? CheckedInByUserId { get; set; }

    public CheckInMethod? CheckInMethod { get; set; }

    public bool EmailSent { get; set; }
    public bool WhatsAppSent { get; set; }

    public int ResendCount { get; set; }
    public DateTime? LastResentAt { get; set; }

    public DateTime? AttendeeDetailsLockedAt { get; set; }

    public WorkshopBooking WorkshopBooking { get; set; } = null!;
    public Workshop Workshop { get; set; } = null!;
    public User User { get; set; } = null!;
    public PaymentTransaction PaymentTransaction { get; set; } = null!;
    public WorkshopAttendance? Attendance { get; set; }
}
