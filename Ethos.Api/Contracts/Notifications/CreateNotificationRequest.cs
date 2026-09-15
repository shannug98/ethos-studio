using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Notifications;

public class CreateNotificationRequest
{
    public Guid UserId { get; set; }

    public NotificationType Type { get; set; } = NotificationType.System;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    public string? ActionUrl { get; set; }

    public string? EventKey { get; set; }

    public bool SendExternal { get; set; } = false;

    public string? RecipientEmail { get; set; }

    public string? RecipientPhone { get; set; }
}
