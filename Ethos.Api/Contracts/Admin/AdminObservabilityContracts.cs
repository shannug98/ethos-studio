using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Contracts.Admin;

public class ApiRequestLogResponse
{
    public Guid Id { get; set; }
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
    public DateTime CreatedAt { get; set; }
}

public class TraceDeepDiveResponse
{
    public string TraceId { get; set; } = null!;
    public ApiRequestLogResponse? Request { get; set; }
    public List<AdminAuditLogResponse> AdminActions { get; set; } = new();
    public List<AdminSecurityEventResponse> SecurityEvents { get; set; } = new();
    public List<PaymentEventSummaryDto> PaymentEvents { get; set; } = new();
    public ExceptionSummaryDto? ExceptionDetails { get; set; }
}

public class PaymentEventSummaryDto
{
    public Guid Id { get; set; }
    public Guid PaymentTransactionId { get; set; }
    public string EventType { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = null!;
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExceptionSummaryDto
{
    public string Type { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string Path { get; set; } = null!;
    public string Method { get; set; } = null!;
    public DateTime Timestamp { get; set; }
}

public class ObservabilityMetricsResponse
{
    public long TotalRequests { get; set; }
    public double RequestsPerMinute { get; set; }
    public double ErrorRatePercentage { get; set; }
    public double AverageDurationMs { get; set; }
    public double P50Ms { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public List<EndpointLatencyDto> SlowestEndpoints { get; set; } = new();
    public List<EndpointFailureDto> FailingEndpoints { get; set; } = new();
}

public class EndpointLatencyDto
{
    public string Path { get; set; } = null!;
    public string Method { get; set; } = null!;
    public double AverageDurationMs { get; set; }
    public int RequestCount { get; set; }
}

public class EndpointFailureDto
{
    public string Path { get; set; } = null!;
    public string Method { get; set; } = null!;
    public int ErrorCount { get; set; }
    public double ErrorRatePercentage { get; set; }
}

public class DeepHealthCheckResponse
{
    public string Status { get; set; } = "Healthy";
    public SubsystemHealthDto Database { get; set; } = new();
    public SubsystemHealthDto Storage { get; set; } = new();
    public SubsystemHealthDto Authentication { get; set; } = new();
    public SubsystemHealthDto Payments { get; set; } = new();
    public SubsystemHealthDto Messaging { get; set; } = new();
}

public class SubsystemHealthDto
{
    public string Name { get; set; } = null!;
    public string Status { get; set; } = "Healthy";
    public long LatencyMs { get; set; }
    public string? Details { get; set; }
}

public class UserTechnicalTimelineResponse
{
    public Guid UserId { get; set; }
    public string? Phone { get; set; }
    public string? Role { get; set; }
    public List<UserTimelineItemDto> Timeline { get; set; } = new();
}

public class UserTimelineItemDto
{
    public string Type { get; set; } = null!; // REQUEST, SECURITY, AUDIT
    public string Action { get; set; } = null!;
    public int? StatusCode { get; set; }
    public string? TraceId { get; set; }
    public string? IpAddress { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}