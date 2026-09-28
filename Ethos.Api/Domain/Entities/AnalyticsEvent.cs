using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class AnalyticsEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public AnalyticsEventType EventType { get; set; }

    public string EventName { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public string VisitorId { get; set; } = string.Empty;

    public string SessionId { get; set; } = string.Empty;

    public Guid? WorkshopId { get; set; }

    public string? Path { get; set; }

    public string? Referrer { get; set; }

    public string? MetadataJson { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public Guid? UserId { get; set; }

    // Optional navigation property for Workshop (soft reference)
    public Workshop? Workshop { get; set; }

    // Optional navigation property for User (soft reference)
    public User? User { get; set; }
}
