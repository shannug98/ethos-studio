using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Notifications;

public class NotificationResponse
{
    public Guid Id { get; set; }

    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public NotificationChannel Channel { get; set; }

    public string? ActionUrl { get; set; }

    public string? EventKey { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
