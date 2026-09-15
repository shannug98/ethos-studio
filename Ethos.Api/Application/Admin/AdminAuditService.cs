using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminAuditService : IAdminAuditService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AdminAuditService> _logger;

    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "pwd", "otp", "token", "accesstoken", "refreshtoken",
        "credential", "devicecredential", "sessiontoken", "secret", "secretkey",
        "hash", "authorization", "bearer", "cookie", "cvv", "cardnumber"
    };

    private static readonly Regex BearerRegex = new(@"Bearer\s+[A-Za-z0-9\-_.]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public AdminAuditService(AppDbContext db, ILogger<AdminAuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public void AddAuditLog(
        Guid adminUserId,
        string actionType,
        string entityType,
        Guid entityId,
        string? reason,
        string category = "OPERATIONS",
        Guid? adminDeviceId = null,
        Guid? adminSessionId = null,
        string? traceId = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? metadataJson = null)
    {
        var sanitizedMetadata = SanitizeJsonString(metadataJson);

        var action = new AdminAction
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            AdminDeviceId = adminDeviceId,
            AdminSessionId = adminSessionId,
            ActionType = actionType.Trim().ToUpperInvariant(),
            Category = string.IsNullOrWhiteSpace(category) ? "OPERATIONS" : category.Trim().ToUpperInvariant(),
            EntityType = entityType.Trim().ToUpperInvariant(),
            EntityId = entityId,
            Success = true,
            OutcomeCode = "SUCCESS",
            Reason = reason?.Trim(),
            TraceId = traceId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            MetadataJson = sanitizedMetadata,
            CreatedAt = DateTime.UtcNow
        };

        _db.AdminActions.Add(action);
    }

    public async Task LogActionAsync(
        Guid adminUserId,
        string actionType,
        string category,
        string entityType,
        Guid entityId,
        bool success = true,
        string? outcomeCode = null,
        string? reason = null,
        Guid? adminDeviceId = null,
        Guid? adminSessionId = null,
        string? traceId = null,
        string? requestId = null,
        string? ipAddress = null,
        string? userAgent = null,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedMetadata = SanitizeObject(metadata);

        var action = new AdminAction
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            AdminDeviceId = adminDeviceId,
            AdminSessionId = adminSessionId,
            ActionType = actionType.Trim().ToUpperInvariant(),
            Category = string.IsNullOrWhiteSpace(category) ? "OPERATIONS" : category.Trim().ToUpperInvariant(),
            EntityType = entityType.Trim().ToUpperInvariant(),
            EntityId = entityId,
            Success = success,
            OutcomeCode = outcomeCode ?? (success ? "SUCCESS" : "FAILURE"),
            Reason = reason?.Trim(),
            TraceId = traceId,
            RequestId = requestId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            MetadataJson = sanitizedMetadata,
            CreatedAt = DateTime.UtcNow
        };

        _db.AdminActions.Add(action);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task LogSecurityEventAsync(
        string eventType,
        string severity,
        string? ipAddress,
        string? userAgent,
        Guid? userId = null,
        Guid? adminDeviceId = null,
        Guid? adminSessionId = null,
        string? traceId = null,
        string? maskedPhone = null,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedDetails = SanitizeObject(details);

        var secEvent = new SecurityEvent
        {
            Id = Guid.NewGuid(),
            EventType = eventType.Trim().ToUpperInvariant(),
            Severity = string.IsNullOrWhiteSpace(severity) ? "INFO" : severity.Trim().ToUpperInvariant(),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            UserId = userId,
            AdminDeviceId = adminDeviceId,
            AdminSessionId = adminSessionId,
            TraceId = traceId,
            MaskedPhone = maskedPhone,
            DetailsJson = sanitizedDetails,
            CreatedAt = DateTime.UtcNow
        };

        _db.SecurityEvents.Add(secEvent);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<AdminAuditLogResponse>> GetAuditLogsAsync(
        int page,
        int pageSize,
        string? category = null,
        string? actionType = null,
        string? entityType = null,
        Guid? entityId = null,
        Guid? adminUserId = null,
        string? traceId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.AdminActions
            .AsNoTracking()
            .AsQueryable();

        if (adminUserId.HasValue)
            query = query.Where(a => a.AdminUserId == adminUserId.Value);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(a => a.Category.ToLower() == category.Trim().ToLower());

        if (!string.IsNullOrWhiteSpace(actionType))
            query = query.Where(a => a.ActionType.ToLower() == actionType.Trim().ToLower());

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType.ToLower() == entityType.Trim().ToLower());

        if (entityId.HasValue)
            query = query.Where(a => a.EntityId == entityId.Value);

        if (!string.IsNullOrWhiteSpace(traceId))
            query = query.Where(a => a.TraceId != null && a.TraceId.ToLower() == traceId.Trim().ToLower());

        if (startDate.HasValue)
            query = query.Where(a => a.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.CreatedAt <= endDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var actions = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var adminUserIds = actions.Select(a => a.AdminUserId).Distinct().ToList();
        var adminUsers = await _db.Users
            .AsNoTracking()
            .Where(u => adminUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var deviceIds = actions.Where(a => a.AdminDeviceId.HasValue).Select(a => a.AdminDeviceId!.Value).Distinct().ToList();
        var devices = await _db.AdminDevices
            .AsNoTracking()
            .Where(d => deviceIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        var items = actions.Select(a => new AdminAuditLogResponse
        {
            Id = a.Id,
            AdminUserId = a.AdminUserId,
            AdminName = adminUsers.TryGetValue(a.AdminUserId, out var u) ? u.FullName : "System Admin",
            AdminCustomerCode = adminUsers.TryGetValue(a.AdminUserId, out u) ? u.CustomerCode : null,
            AdminPhone = adminUsers.TryGetValue(a.AdminUserId, out u) ? u.Phone : "",
            AdminDeviceId = a.AdminDeviceId,
            DeviceName = a.AdminDeviceId.HasValue && devices.TryGetValue(a.AdminDeviceId.Value, out var dev) ? dev.DeviceName : null,
            ActionType = a.ActionType,
            Category = string.IsNullOrWhiteSpace(a.Category) ? "OPERATIONS" : a.Category,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            Success = a.Success,
            OutcomeCode = a.OutcomeCode,
            Reason = a.Reason,
            TraceId = a.TraceId,
            RequestId = a.RequestId,
            IpAddress = a.IpAddress,
            UserAgent = a.UserAgent,
            MetadataJson = a.MetadataJson,
            CreatedAt = a.CreatedAt
        }).ToList();

        return new PagedResult<AdminAuditLogResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<AdminAuditLogResponse?> GetAuditLogByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var action = await _db.AdminActions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (action == null) return null;

        var adminUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == action.AdminUserId, cancellationToken);

        AdminDevice? device = null;
        if (action.AdminDeviceId.HasValue)
        {
            device = await _db.AdminDevices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == action.AdminDeviceId.Value, cancellationToken);
        }

        return new AdminAuditLogResponse
        {
            Id = action.Id,
            AdminUserId = action.AdminUserId,
            AdminName = adminUser?.FullName ?? "System Admin",
            AdminCustomerCode = adminUser?.CustomerCode,
            AdminPhone = adminUser?.Phone ?? "",
            AdminDeviceId = action.AdminDeviceId,
            DeviceName = device?.DeviceName,
            ActionType = action.ActionType,
            Category = string.IsNullOrWhiteSpace(action.Category) ? "OPERATIONS" : action.Category,
            EntityType = action.EntityType,
            EntityId = action.EntityId,
            Success = action.Success,
            OutcomeCode = action.OutcomeCode,
            Reason = action.Reason,
            TraceId = action.TraceId,
            RequestId = action.RequestId,
            IpAddress = action.IpAddress,
            UserAgent = action.UserAgent,
            MetadataJson = action.MetadataJson,
            CreatedAt = action.CreatedAt
        };
    }

    public async Task<PagedResult<AdminSecurityEventResponse>> GetSecurityEventsAsync(
        int page,
        int pageSize,
        string? eventType = null,
        string? severity = null,
        Guid? userId = null,
        string? traceId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.SecurityEvents
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(e => e.EventType.ToLower() == eventType.Trim().ToLower());

        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(e => e.Severity.ToLower() == severity.Trim().ToLower());

        if (userId.HasValue)
            query = query.Where(e => e.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(traceId))
            query = query.Where(e => e.TraceId != null && e.TraceId.ToLower() == traceId.Trim().ToLower());

        if (startDate.HasValue)
            query = query.Where(e => e.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => e.CreatedAt <= endDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var events = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userIds = events.Where(e => e.UserId.HasValue).Select(e => e.UserId!.Value).Distinct().ToList();
        var users = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var devIds = events.Where(e => e.AdminDeviceId.HasValue).Select(e => e.AdminDeviceId!.Value).Distinct().ToList();
        var devs = await _db.AdminDevices
            .AsNoTracking()
            .Where(d => devIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        var items = events.Select(e => new AdminSecurityEventResponse
        {
            Id = e.Id,
            EventType = e.EventType,
            Severity = e.Severity,
            IpAddress = e.IpAddress,
            UserAgent = e.UserAgent,
            UserId = e.UserId,
            AdminName = e.UserId.HasValue && users.TryGetValue(e.UserId.Value, out var u) ? u.FullName : null,
            AdminCustomerCode = e.UserId.HasValue && users.TryGetValue(e.UserId.Value, out u) ? u.CustomerCode : null,
            AdminDeviceId = e.AdminDeviceId,
            DeviceName = e.AdminDeviceId.HasValue && devs.TryGetValue(e.AdminDeviceId.Value, out var d) ? d.DeviceName : null,
            AdminSessionId = e.AdminSessionId,
            TraceId = e.TraceId,
            MaskedPhone = e.MaskedPhone,
            DetailsJson = e.DetailsJson,
            CreatedAt = e.CreatedAt
        }).ToList();

        return new PagedResult<AdminSecurityEventResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<AdminSecurityEventResponse?> GetSecurityEventByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var e = await _db.SecurityEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (e == null) return null;

        User? user = null;
        if (e.UserId.HasValue)
        {
            user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == e.UserId.Value, cancellationToken);
        }

        AdminDevice? dev = null;
        if (e.AdminDeviceId.HasValue)
        {
            dev = await _db.AdminDevices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == e.AdminDeviceId.Value, cancellationToken);
        }

        return new AdminSecurityEventResponse
        {
            Id = e.Id,
            EventType = e.EventType,
            Severity = e.Severity,
            IpAddress = e.IpAddress,
            UserAgent = e.UserAgent,
            UserId = e.UserId,
            AdminName = user?.FullName,
            AdminCustomerCode = user?.CustomerCode,
            AdminDeviceId = e.AdminDeviceId,
            DeviceName = dev?.DeviceName,
            AdminSessionId = e.AdminSessionId,
            TraceId = e.TraceId,
            MaskedPhone = e.MaskedPhone,
            DetailsJson = e.DetailsJson,
            CreatedAt = e.CreatedAt
        };
    }

    // --- Strict Secret Sanitization ---

    private static string? SanitizeObject(object? obj)
    {
        if (obj == null) return null;
        if (obj is string str) return SanitizeJsonString(str);

        try
        {
            var json = JsonSerializer.Serialize(obj);
            return SanitizeJsonString(json);
        }
        catch
        {
            return "{}";
        }
    }

    private static string? SanitizeJsonString(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            var node = JsonNode.Parse(json);
            if (node == null) return null;

            SanitizeNode(node);
            return node.ToJsonString();
        }
        catch
        {
            // If not JSON, apply regex redacting
            return RedactSensitivePatterns(json);
        }
    }

    private static void SanitizeNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var propertiesToMask = new List<string>();
            foreach (var kvp in obj)
            {
                if (SensitiveKeys.Contains(kvp.Key))
                {
                    propertiesToMask.Add(kvp.Key);
                }
                else if (kvp.Value != null)
                {
                    SanitizeNode(kvp.Value);
                }
            }

            foreach (var prop in propertiesToMask)
            {
                obj[prop] = "[REDACTED]";
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item != null)
                {
                    SanitizeNode(item);
                }
            }
        }
    }

    private static string RedactSensitivePatterns(string text)
    {
        var redacted = BearerRegex.Replace(text, "Bearer [REDACTED]");
        return redacted;
    }
}
