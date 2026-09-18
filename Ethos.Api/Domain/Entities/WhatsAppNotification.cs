using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class WhatsAppNotification
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public Guid? WorkshopTicketId { get; set; }

    public WhatsAppNotificationType NotificationType { get; set; }

    public string RecipientPhone { get; set; } = null!;

    public string IdempotencyKey { get; set; } = null!;

    public WhatsAppNotificationStatus Status { get; set; } = WhatsAppNotificationStatus.Pending;

    public int Attempts { get; set; }

    public string? ProviderMessageId { get; set; }

    public string? ProviderRequestId { get; set; }

    public string? LastError { get; set; }

    public DateTime? LeaseExpiresAt { get; set; }

    public string? LockedByWorkerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    public DateTime? NextAttemptAt { get; set; }

    public WorkshopBooking WorkshopBooking { get; set; } = null!;

    public WorkshopTicket? WorkshopTicket { get; set; }
}
