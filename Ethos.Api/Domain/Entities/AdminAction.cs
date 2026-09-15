namespace Ethos.Api.Domain.Entities;

public class AdminAction
{
    public Guid Id { get; set; }

    public Guid AdminUserId { get; set; }

    public Guid? AdminDeviceId { get; set; }

    public Guid? AdminSessionId { get; set; }

    public string ActionType { get; set; } = null!;

    public string Category { get; set; } = "SYSTEM";

    public string EntityType { get; set; } = null!;

    public Guid EntityId { get; set; }

    public bool Success { get; set; } = true;

    public string? OutcomeCode { get; set; }

    public string? Reason { get; set; }

    public string? TraceId { get; set; }

    public string? RequestId { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? MetadataJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public User? AdminUser { get; set; }

    public AdminDevice? AdminDevice { get; set; }

    public AdminSession? AdminSession { get; set; }
}
