using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; }

    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public NotificationChannel Channel { get; set; }
    
    public string? EventKey { get; set; }

    public string? ActionUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<NotificationRecipient> Recipients { get; set; }
        = new List<NotificationRecipient>();
}
