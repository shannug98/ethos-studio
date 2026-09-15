namespace Ethos.Api.Domain.Entities;

public class ApiRequestLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string TraceId { get; set; } = null!;

    public string? CorrelationId { get; set; }

    public string Method { get; set; } = null!;

    public string Path { get; set; } = null!;

    public int StatusCode { get; set; }

    public long DurationMs { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public Guid? UserId { get; set; }

    public string? Role { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}