namespace Ethos.Api.Domain.Entities;

public class NotificationRecipient
{
    public Guid Id { get; set; }

    public Guid NotificationId { get; set; }

    public Guid UserId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }
    
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Notification Notification { get; set; } = null!;

    public User User { get; set; } = null!;
}
