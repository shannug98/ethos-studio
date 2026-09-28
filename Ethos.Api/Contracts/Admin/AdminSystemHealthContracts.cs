using System;
using System.Collections.Generic;

namespace Ethos.Api.Contracts.Admin;

public class SystemHealthResponse
{
    public string OverallStatus { get; set; } = "Operational"; // Operational, Degraded, Error, Not Configured, Standby
    public int TotalComponents { get; set; } = 10;
    public int OperationalCount { get; set; }
    public int DegradedCount { get; set; }
    public int ErrorCount { get; set; }
    public int NotConfiguredCount { get; set; }
    public int StandbyCount { get; set; }
    public DateTime LastCheckedUtc { get; set; } = DateTime.UtcNow;
    public string FormattedLastChecked { get; set; } = string.Empty;
    public List<SystemHealthComponentDto> Components { get; set; } = new();
}

public class SystemHealthComponentDto
{
    public string Key { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Category { get; set; } = null!; // Core, Database, Gateway, Storage, Messaging, Background Worker, Security
    public string Status { get; set; } = "Operational"; // Operational, Degraded, Error, Not Configured, Standby
    public long LatencyMs { get; set; }
    public DateTime LastCheckedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSuccessfulCheckUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> AffectedSystems { get; set; } = new();
    public Dictionary<string, string> Diagnostics { get; set; } = new();
    public string? TraceId { get; set; }
    public string? ActionUrl { get; set; }
    public string? ActionLabel { get; set; }
}
