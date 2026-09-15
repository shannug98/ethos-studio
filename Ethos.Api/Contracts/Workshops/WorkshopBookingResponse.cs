using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Workshops;

public class WorkshopBookingResponse
{
    public Guid Id { get; set; }

    public Guid WorkshopId { get; set; }

    public string WorkshopTitle { get; set; } = string.Empty;

    public DateTime WorkshopDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public string Venue { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    public decimal Price { get; set; }

    public decimal TotalPrice { get; set; }

    public string? BookingReference { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? CustomerEmail { get; set; }

    public WorkshopBookingStatus Status { get; set; }

    public DateTime BookedAt { get; set; }

    public List<WorkshopTicketResponse> Tickets { get; set; } = new();
}
