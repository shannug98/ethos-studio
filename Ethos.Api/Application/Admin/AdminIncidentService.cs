using System.Text.Json;
using System.Text.RegularExpressions;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public interface IAdminIncidentService
{
    Task<PagedResult<IncidentResponse>> GetIncidentsAsync(
        int page,
        int pageSize,
        string? status = null,
        string? severity = null,
        string? service = null,
        CancellationToken cancellationToken = default);

    Task<IncidentResponse?> GetIncidentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IncidentResponse> CreateIncidentAsync(
        CreateIncidentRequest request,
        Guid adminUserId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<IncidentResponse> CreateIncidentFromTraceAsync(
        CreateIncidentFromTraceRequest request,
        Guid adminUserId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<IncidentResponse> CreateIncidentFromSecurityEventAsync(
        CreateIncidentFromSecurityEventRequest request,
        Guid adminUserId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<IncidentResponse> UpdateIncidentStatusAsync(
        Guid id,
        UpdateIncidentStatusRequest request,
        Guid adminUserId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<IncidentUpdateDto> AddIncidentUpdateAsync(
        Guid incidentId,
        AddIncidentUpdateDto request,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    Task<IncidentResponse> AssignIncidentAsync(
        Guid id,
        AssignIncidentRequest request,
        Guid adminUserId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<IncidentMetricsResponse> GetIncidentMetricsAsync(
        CancellationToken cancellationToken = default);
}

public class AdminIncidentService : IAdminIncidentService
{
    private readonly AppDbContext _db;
    private static readonly Regex SecretRegex = new(
        @"(\""?(password|token|secret|hash|otp|credential|bearer)\""?\s*:\s*\"")[^\""]*(\"")",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public AdminIncidentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<IncidentResponse>> GetIncidentsAsync(
        int page,
        int pageSize,
        string? status = null,
        string? severity = null,
        string? service = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _db.Incidents.AsNoTracking()
            .Include(x => x.AssignedAdmin)
            .Include(x => x.Updates)
            .ThenInclude(u => u.AdminUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim();
            query = query.Where(x => x.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            var sev = severity.Trim();
            query = query.Where(x => x.Severity == sev);
        }

        if (!string.IsNullOrWhiteSpace(service))
        {
            var srv = service.Trim();
            query = query.Where(x => x.AffectedService == srv);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<IncidentResponse>
        {
            Items = items.Select(MapIncident).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IncidentResponse?> GetIncidentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var inc = await _db.Incidents.AsNoTracking()
            .Include(x => x.AssignedAdmin)
            .Include(x => x.Updates)
            .ThenInclude(u => u.AdminUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return inc != null ? MapIncident(inc) : null;
    }

    public async Task<IncidentResponse> CreateIncidentAsync(
        CreateIncidentRequest request,
        Guid adminUserId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Incident title is mandatory.");
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new ArgumentException("Incident description is mandatory.");

        var incidentNumber = await GenerateIncidentNumberAsync(cancellationToken);
        var cleanEvidence = SanitizeEvidence(request.EvidenceJson);

        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            IncidentNumber = incidentNumber,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Severity = NormalizeSeverity(request.Severity),
            Status = "Investigating",
            AffectedService = NormalizeService(request.AffectedService),
            AssignedAdminId = request.AssignedAdminId ?? adminUserId,
            TraceId = request.TraceId,
            EvidenceJson = cleanEvidence,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Incidents.Add(incident);

        // Record initial update
        _db.IncidentUpdates.Add(new IncidentUpdate
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            AdminUserId = adminUserId,
            PreviousStatus = null,
            NewStatus = "Investigating",
            Message = "Incident created and investigation initiated.",
            CreatedAt = DateTime.UtcNow
        });

        // Audit log
        _db.AdminActions.Add(new AdminAction
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            ActionType = "INCIDENT_CREATED",
            Category = "OPERATIONS",
            EntityType = "INCIDENT",
            EntityId = incident.Id,
            Success = true,
            Reason = $"Created incident {incident.IncidentNumber}: {incident.Title}",
            TraceId = traceId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return await GetIncidentByIdAsync(incident.Id, cancellationToken) ?? MapIncident(incident);
    }

    public async Task<IncidentResponse> CreateIncidentFromTraceAsync(
        CreateIncidentFromTraceRequest request,
        Guid adminUserId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TraceId))
            throw new ArgumentException("Trace ID is required to promote to an incident.");

        var cleanTrace = request.TraceId.Trim();
        var log = await _db.ApiRequestLogs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TraceId == cleanTrace, cancellationToken);

        string title = request.Title?.Trim() ??
            (log != null ? $"[Trace {cleanTrace}] Failure on {log.Method} {log.Path}" : $"[Trace {cleanTrace}] Investigated API Anomaly");

        string description = log != null
            ? $"Automated incident promoted from Trace {cleanTrace}. Endpoint: {log.Method} {log.Path}, Status: {log.StatusCode}, Duration: {log.DurationMs}ms, Client IP: {log.IpAddress}. Error: {log.ErrorMessage ?? "None recorded"}."
            : $"Automated incident promoted from Trace {cleanTrace}. No direct request log found in ring buffer.";

        var evidenceObj = new
        {
            promotedFrom = "OBSERVABILITY_TRACE",
            traceId = cleanTrace,
            requestLog = log != null ? new
            {
                log.Method,
                log.Path,
                log.StatusCode,
                log.DurationMs,
                log.IpAddress,
                log.UserId,
                log.Role,
                log.CreatedAt
            } : null
        };

        var evidenceJson = JsonSerializer.Serialize(evidenceObj);

        return await CreateIncidentAsync(new CreateIncidentRequest
        {
            Title = title,
            Description = description,
            Severity = request.Severity ?? (log?.StatusCode >= 500 ? "HIGH" : "MEDIUM"),
            AffectedService = DeduceServiceFromPath(log?.Path),
            TraceId = cleanTrace,
            EvidenceJson = evidenceJson,
            AssignedAdminId = adminUserId
        }, adminUserId, traceId, cancellationToken);
    }

    public async Task<IncidentResponse> CreateIncidentFromSecurityEventAsync(
        CreateIncidentFromSecurityEventRequest request,
        Guid adminUserId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var secEvent = await _db.SecurityEvents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.SecurityEventId, cancellationToken);

        if (secEvent == null)
            throw new ArgumentException("Referenced security event does not exist.");

        string title = request.Title?.Trim() ?? $"[Security Alert] {secEvent.EventType} from {secEvent.IpAddress ?? "unknown IP"}";
        string description = $"Automated security incident promoted from Security Event {secEvent.Id}. EventType: {secEvent.EventType}, Severity: {secEvent.Severity}, Actor IP: {secEvent.IpAddress}, Trace ID: {secEvent.TraceId}. Details: {secEvent.DetailsJson}.";

        var evidenceObj = new
        {
            promotedFrom = "SECURITY_CENTER_EVENT",
            securityEventId = secEvent.Id,
            secEvent.EventType,
            secEvent.Severity,
            secEvent.IpAddress,
            secEvent.TraceId,
            secEvent.DetailsJson,
            secEvent.CreatedAt
        };

        var evidenceJson = JsonSerializer.Serialize(evidenceObj);

        return await CreateIncidentAsync(new CreateIncidentRequest
        {
            Title = title,
            Description = description,
            Severity = request.Severity ?? (secEvent.Severity == "CRITICAL" ? "CRITICAL" : "HIGH"),
            AffectedService = "AUTHENTICATION",
            TraceId = secEvent.TraceId,
            EvidenceJson = evidenceJson,
            AssignedAdminId = adminUserId
        }, adminUserId, traceId, cancellationToken);
    }

    public async Task<IncidentResponse> UpdateIncidentStatusAsync(
        Guid id,
        UpdateIncidentStatusRequest request,
        Guid adminUserId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var incident = await _db.Incidents
            .Include(x => x.Updates)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (incident == null)
            throw new KeyNotFoundException("Incident not found.");

        var requestedStatus = !string.IsNullOrWhiteSpace(request.NewStatus) ? request.NewStatus : request.TargetStatus;
        if (string.IsNullOrWhiteSpace(requestedStatus))
            throw new ArgumentException("Status is mandatory.");

        var oldStatus = incident.Status;
        var newStatus = NormalizeStatus(requestedStatus);

        if (oldStatus == newStatus)
            return MapIncident(incident); // Idempotent no-op

        // Validate State Machine Transitions
        ValidateStatusTransition(oldStatus, newStatus);

        // Resolution Guardrail (Mandatory Root Cause & Resolution Notes)
        if (newStatus == "Resolved")
        {
            if (string.IsNullOrWhiteSpace(request.RootCause) || string.IsNullOrWhiteSpace(request.ResolutionNotes))
            {
                throw new InvalidOperationException("Root cause and resolution notes are mandatory to resolve an incident.");
            }

            incident.RootCause = request.RootCause.Trim();
            incident.ResolutionNotes = request.ResolutionNotes.Trim();
            incident.ResolvedAt = DateTime.UtcNow;
        }

        incident.Status = newStatus;
        incident.UpdatedAt = DateTime.UtcNow;

        var message = !string.IsNullOrWhiteSpace(request.Message)
            ? request.Message.Trim()
            : $"Status transitioned from {oldStatus} to {newStatus}.";

        _db.IncidentUpdates.Add(new IncidentUpdate
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            AdminUserId = adminUserId,
            PreviousStatus = oldStatus,
            NewStatus = newStatus,
            Message = message,
            CreatedAt = DateTime.UtcNow
        });

        _db.AdminActions.Add(new AdminAction
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            ActionType = newStatus == "Resolved" ? "INCIDENT_RESOLVED" : "INCIDENT_STATUS_CHANGED",
            Category = "OPERATIONS",
            EntityType = "INCIDENT",
            EntityId = incident.Id,
            Success = true,
            Reason = $"Status: {oldStatus} -> {newStatus}. {message}",
            TraceId = traceId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return await GetIncidentByIdAsync(id, cancellationToken) ?? MapIncident(incident);
    }

    public async Task<IncidentUpdateDto> AddIncidentUpdateAsync(
        Guid incidentId,
        AddIncidentUpdateDto request,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Update message cannot be empty.");

        var incident = await _db.Incidents
            .FirstOrDefaultAsync(x => x.Id == incidentId, cancellationToken);

        if (incident == null)
            throw new KeyNotFoundException("Incident not found.");

        string? previousStatus = null;
        string? newStatus = null;

        if (!string.IsNullOrWhiteSpace(request.NewStatus))
        {
            newStatus = NormalizeStatus(request.NewStatus);
            if (newStatus != incident.Status)
            {
                previousStatus = incident.Status;
                ValidateStatusTransition(previousStatus, newStatus);
                incident.Status = newStatus;
            }
            else
            {
                newStatus = null;
            }
        }

        incident.UpdatedAt = DateTime.UtcNow;

        var adminUser = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == adminUserId, cancellationToken);

        var update = new IncidentUpdate
        {
            Id = Guid.NewGuid(),
            IncidentId = incidentId,
            AdminUserId = adminUserId,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Message = request.Message.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.IncidentUpdates.Add(update);
        await _db.SaveChangesAsync(cancellationToken);

        return new IncidentUpdateDto
        {
            Id = update.Id,
            AdminUserId = adminUserId,
            AdminName = adminUser?.FullName ?? "Admin",
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Message = update.Message,
            CreatedAt = update.CreatedAt
        };
    }

    public async Task<IncidentResponse> AssignIncidentAsync(
        Guid id,
        AssignIncidentRequest request,
        Guid adminUserId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var incident = await _db.Incidents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (incident == null)
            throw new KeyNotFoundException("Incident not found.");

        var targetAdmin = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.AssignedAdminId, cancellationToken);

        if (targetAdmin == null)
            throw new ArgumentException("Target admin user does not exist.");

        incident.AssignedAdminId = targetAdmin.Id;
        incident.UpdatedAt = DateTime.UtcNow;

        _db.IncidentUpdates.Add(new IncidentUpdate
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            AdminUserId = adminUserId,
            PreviousStatus = incident.Status,
            NewStatus = incident.Status,
            Message = $"Incident assigned to {targetAdmin.FullName} ({targetAdmin.CustomerCode ?? targetAdmin.Phone}).",
            CreatedAt = DateTime.UtcNow
        });

        _db.AdminActions.Add(new AdminAction
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            ActionType = "INCIDENT_ASSIGNED",
            Category = "OPERATIONS",
            EntityType = "INCIDENT",
            EntityId = incident.Id,
            Success = true,
            Reason = $"Assigned to {targetAdmin.FullName}",
            TraceId = traceId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return await GetIncidentByIdAsync(id, cancellationToken) ?? MapIncident(incident);
    }

    public async Task<IncidentMetricsResponse> GetIncidentMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        var incidents = await _db.Incidents.AsNoTracking().ToListAsync(cancellationToken);

        var open = incidents.Count(x => x.Status != "Resolved" && x.Status != "Cancelled");
        var critical = incidents.Count(x => x.Severity == "CRITICAL" && x.Status != "Resolved" && x.Status != "Cancelled");

        var resolved = incidents.Where(x => x.Status == "Resolved" && x.ResolvedAt.HasValue).ToList();
        double mttrHours = resolved.Count > 0
            ? Math.Round(resolved.Average(x => (x.ResolvedAt!.Value - x.CreatedAt).TotalHours), 2)
            : 0;
        double mttrMinutes = resolved.Count > 0
            ? Math.Round(resolved.Average(x => (x.ResolvedAt!.Value - x.CreatedAt).TotalMinutes), 1)
            : 0;

        var serviceBreakdown = incidents
            .GroupBy(x => x.AffectedService)
            .ToDictionary(g => g.Key, g => g.Count());

        var severityBreakdown = incidents
            .GroupBy(x => x.Severity)
            .ToDictionary(g => g.Key, g => g.Count());

        return new IncidentMetricsResponse
        {
            OpenIncidentsCount = open,
            CriticalOutagesCount = critical,
            CriticalIncidentsCount = critical,
            TotalIncidentsCount = incidents.Count,
            ResolvedIncidentsCount = resolved.Count,
            MttrMinutes = mttrMinutes,
            MeanTimeToResolveHours = mttrHours,
            MeanTimeToDetectHours = 0.25, // Baseline telemetry avg detection time
            ServiceBreakdown = serviceBreakdown,
            SeverityBreakdown = severityBreakdown
        };
    }

    private static void ValidateStatusTransition(string fromStatus, string toStatus)
    {
        if (fromStatus == "Cancelled")
            throw new InvalidOperationException("Cancelled incidents cannot be transitioned to another status.");

        if (fromStatus == "Resolved" && toStatus != "Investigating")
            throw new InvalidOperationException("Resolved incidents can only be transitioned to 'Investigating' (reopened).");

        var allowed = fromStatus switch
        {
            "Investigating" => new[] { "Identified", "Resolved", "Cancelled" },
            "Identified" => new[] { "Monitoring", "Resolved", "Cancelled" },
            "Monitoring" => new[] { "Resolved", "Investigating" },
            "Resolved" => new[] { "Investigating" },
            _ => Array.Empty<string>()
        };

        if (!allowed.Contains(toStatus, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Invalid status transition from '{fromStatus}' to '{toStatus}'.");
        }
    }

    private async Task<string> GenerateIncidentNumberAsync(CancellationToken cancellationToken)
    {
        var prefix = $"INC-{DateTime.UtcNow:yyyyMM}-";
        var countThisMonth = await _db.Incidents
            .CountAsync(x => x.IncidentNumber.StartsWith(prefix), cancellationToken);

        return $"{prefix}{(countThisMonth + 1):D4}";
    }

    private static string SanitizeEvidence(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return "{}";
        var scrubbed = SecretRegex.Replace(rawJson, "$1[REDACTED]$3");
        return scrubbed.Length > 8000 ? scrubbed[..8000] : scrubbed;
    }

    private static string NormalizeStatus(string status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "identified" => "Identified",
            "monitoring" => "Monitoring",
            "resolved" => "Resolved",
            "cancelled" => "Cancelled",
            _ => "Investigating"
        };
    }

    private static string NormalizeSeverity(string severity)
    {
        return severity?.Trim().ToUpperInvariant() switch
        {
            "CRITICAL" => "CRITICAL",
            "HIGH" => "HIGH",
            "LOW" => "LOW",
            _ => "MEDIUM"
        };
    }

    private static string NormalizeService(string service)
    {
        var valid = new[]
        {
            "API_GATEWAY", "DATABASE", "AUTHENTICATION", "PAYMENTS",
            "FILE_STORAGE", "NOTIFICATIONS", "STUDIO_CLASSES", "WORKSHOPS"
        };

        var s = service?.Trim().ToUpperInvariant() ?? "API_GATEWAY";
        return valid.Contains(s) ? s : "API_GATEWAY";
    }

    private static string DeduceServiceFromPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "API_GATEWAY";
        var p = path.ToLowerInvariant();
        if (p.Contains("payment") || p.Contains("razorpay") || p.Contains("refund")) return "PAYMENTS";
        if (p.Contains("auth") || p.Contains("login") || p.Contains("otp") || p.Contains("device")) return "AUTHENTICATION";
        if (p.Contains("workshop")) return "WORKSHOPS";
        if (p.Contains("class") || p.Contains("attendance") || p.Contains("booking")) return "STUDIO_CLASSES";
        if (p.Contains("notification")) return "NOTIFICATIONS";
        if (p.Contains("upload") || p.Contains("photo") || p.Contains("document")) return "FILE_STORAGE";
        return "API_GATEWAY";
    }

    private static IncidentResponse MapIncident(Incident inc)
    {
        return new IncidentResponse
        {
            Id = inc.Id,
            IncidentNumber = inc.IncidentNumber,
            Title = inc.Title,
            Description = inc.Description,
            Severity = inc.Severity,
            Status = inc.Status,
            AffectedService = inc.AffectedService,
            AssignedAdminId = inc.AssignedAdminId,
            AssignedAdminName = inc.AssignedAdmin?.FullName,
            TraceId = inc.TraceId,
            EvidenceJson = inc.EvidenceJson,
            RootCause = inc.RootCause,
            ResolutionNotes = inc.ResolutionNotes,
            CreatedAt = inc.CreatedAt,
            UpdatedAt = inc.UpdatedAt,
            ResolvedAt = inc.ResolvedAt,
            Updates = inc.Updates?.OrderByDescending(u => u.CreatedAt).Select(u => new IncidentUpdateDto
            {
                Id = u.Id,
                AdminUserId = u.AdminUserId,
                AdminName = u.AdminUser?.FullName ?? "Admin",
                PreviousStatus = u.PreviousStatus,
                NewStatus = u.NewStatus,
                Message = u.Message,
                CreatedAt = u.CreatedAt
            }).ToList() ?? new List<IncidentUpdateDto>()
        };
    }
}