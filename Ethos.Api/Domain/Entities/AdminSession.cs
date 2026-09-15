namespace Ethos.Api.Domain.Entities;

public class AdminSession
{
    public Guid Id { get; set; }

    public Guid AdminDeviceId { get; set; }

    public Guid AdminUserId { get; set; }

    public string SessionTokenHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime? LoggedOutAt { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public AdminDevice AdminDevice { get; set; } = null!;

    public User AdminUser { get; set; } = null!;
}
