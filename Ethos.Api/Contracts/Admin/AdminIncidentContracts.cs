namespace Ethos.Api.Contracts.Admin;

public class CreateIncidentRequest
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Severity { get; set; } = "MEDIUM"; // CRITICAL, HIGH, MEDIUM, LOW
    public string AffectedService { get; set; } = "API_GATEWAY";
    public string? TraceId { get; set; }
    public string? EvidenceJson { get; set; }
    public Guid? AssignedAdminId { get; set; }
}

public class CreateIncidentFromTraceRequest
{
    public string TraceId { get; set; } = null!;
    public string? Title { get; set; }
    public string? Severity { get; set; }
}

public class CreateIncidentFromSecurityEventRequest
{
    public Guid SecurityEventId { get; set; }
    public string? Title { get; set; }
    public string? Severity { get; set; }
}

public class UpdateIncidentStatusRequest
{
    public string? NewStatus { get; set; } // Investigating, Identified, Monitoring, Resolved, Cancelled
    public string? TargetStatus { get; set; }
    public string? Message { get; set; }
    public string? RootCause { get; set; } // Required if Resolved
    public string? ResolutionNotes { get; set; } // Required if Resolved
}

public class AddIncidentUpdateDto
{
    public string Message { get; set; } = null!;
    public string? NewStatus { get; set; }
}

public class AssignIncidentRequest
{
    public Guid AssignedAdminId { get; set; }
}

public class IncidentResponse
{
    public Guid Id { get; set; }
    public string IncidentNumber { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string AffectedService { get; set; } = null!;
    public Guid? AssignedAdminId { get; set; }
    public string? AssignedAdminName { get; set; }
    public string? TraceId { get; set; }
    public string? EvidenceJson { get; set; }
    public string? RootCause { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public List<IncidentUpdateDto> Updates { get; set; } = new();
}

public class IncidentUpdateDto
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public string AdminName { get; set; } = null!;
    public string AuthorName => AdminName;
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public string Message { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class IncidentMetricsResponse
{
    public int OpenIncidentsCount { get; set; }
    public int CriticalOutagesCount { get; set; }
    public int CriticalIncidentsCount { get; set; }
    public int TotalIncidentsCount { get; set; }
    public int ResolvedIncidentsCount { get; set; }
    public double MttrMinutes { get; set; }
    public double MeanTimeToResolveHours { get; set; }
    public double MeanTimeToDetectHours { get; set; }
    public Dictionary<string, int> ServiceBreakdown { get; set; } = new();
    public Dictionary<string, int> SeverityBreakdown { get; set; } = new();
}