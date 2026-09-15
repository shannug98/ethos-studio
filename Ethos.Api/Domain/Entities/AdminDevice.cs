using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class AdminDevice
{
    public Guid Id { get; set; }

    public Guid AdminUserId { get; set; }

    public string DeviceCredentialHash { get; set; } = null!;

    public string DeviceName { get; set; } = null!;

    public AdminDeviceStatus Status { get; set; } = AdminDeviceStatus.Active;

    public DateTime RegisteredAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    public string? LastSeenIp { get; set; }

    public string? UserAgent { get; set; }

    public string? FingerprintTelemetry { get; set; }

    public DateTime? RevokedAt { get; set; }

    // Navigation
    public User AdminUser { get; set; } = null!;

    public ICollection<AdminSession> Sessions { get; set; } = new List<AdminSession>();
}
