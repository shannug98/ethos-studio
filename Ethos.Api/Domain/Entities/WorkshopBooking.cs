using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class WorkshopBooking
{
    public Guid Id { get; set; }

    public Guid WorkshopId { get; set; }

    public Guid StudentProfileId { get; set; }

    public Guid? PaymentTransactionId { get; set; }

    public int Quantity { get; set; } = 1;

    public decimal TotalPrice { get; set; }

    public string? PriceBreakdownJson { get; set; }

    public string? GuestName { get; set; }

    public string? GuestPhone { get; set; }

    public string? GuestEmail { get; set; }

    public WorkshopBookingStatus Status { get; set; }

    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString();

    public DateTime? ReservationExpiresAt { get; set; }

    public DateTime BookedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public Workshop Workshop { get; set; } = null!;

    public StudentProfile StudentProfile { get; set; } = null!;

    public ICollection<WorkshopTicket> Tickets { get; set; }
        = new List<WorkshopTicket>();
}
