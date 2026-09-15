namespace Ethos.Api.Domain.Entities;

public class WorkshopFeedbackToken
{
    public Guid Id { get; set; }

    public Guid WorkshopBookingId { get; set; }

    /// <summary>
    /// Cryptographic SHA-256 hash or token key of the single-use guest link.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public WorkshopBooking WorkshopBooking { get; set; } = null!;
}
