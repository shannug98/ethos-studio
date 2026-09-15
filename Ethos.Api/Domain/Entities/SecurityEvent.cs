namespace Ethos.Api.Domain.Entities;

public class SecurityEvent
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = null!;

    public string Severity { get; set; } = "INFO";

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public Guid? UserId { get; set; }

    public Guid? AdminDeviceId { get; set; }

    public Guid? AdminSessionId { get; set; }

    public string? TraceId { get; set; }

    public string? MaskedPhone { get; set; }

    public string? DetailsJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }

    public AdminDevice? AdminDevice { get; set; }

    public AdminSession? AdminSession { get; set; }
}
