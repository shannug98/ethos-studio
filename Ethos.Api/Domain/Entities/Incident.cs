namespace Ethos.Api.Domain.Entities;

public class Incident
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string IncidentNumber { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Severity { get; set; } = "MEDIUM"; // CRITICAL, HIGH, MEDIUM, LOW

    public string Status { get; set; } = "Investigating"; // Investigating, Identified, Monitoring, Resolved, Cancelled

    public string AffectedService { get; set; } = "API_GATEWAY";

    public Guid? AssignedAdminId { get; set; }

    public string? TraceId { get; set; }

    public string? EvidenceJson { get; set; }

    public string? RootCause { get; set; }

    public string? ResolutionNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }

    public User? AssignedAdmin { get; set; }

    public ICollection<IncidentUpdate> Updates { get; set; } = new List<IncidentUpdate>();
}