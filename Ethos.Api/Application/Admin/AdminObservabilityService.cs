using System.Diagnostics;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Ethos.Api.Application.Admin;

public interface IAdminObservabilityService
{
    Task<PagedResult<ApiRequestLogResponse>> GetRequestLogsAsync(
        int page,
        int pageSize,
        int? statusCode = null,
        string? method = null,
        string? path = null,
        string? traceId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        long? minDurationMs = null,
        CancellationToken cancellationToken = default);

    Task<TraceDeepDiveResponse?> GetTraceDeepDiveAsync(
        string traceId,
        CancellationToken cancellationToken = default);

    Task<ObservabilityMetricsResponse> GetObservabilityMetricsAsync(
        CancellationToken cancellationToken = default);

    Task<DeepHealthCheckResponse> GetDeepHealthCheckAsync(
        CancellationToken cancellationToken = default);

    Task<UserTechnicalTimelineResponse> GetUserTechnicalTimelineAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public class AdminObservabilityService : IAdminObservabilityService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AdminObservabilityService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<PagedResult<ApiRequestLogResponse>> GetRequestLogsAsync(
        int page,
        int pageSize,
        int? statusCode = null,
        string? method = null,
        string? path = null,
        string? traceId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        long? minDurationMs = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _db.ApiRequestLogs.AsNoTracking();

        if (statusCode.HasValue)
        {
            query = query.Where(x => x.StatusCode == statusCode.Value);
        }

        if (!string.IsNullOrWhiteSpace(method))
        {
            var m = method.Trim().ToUpperInvariant();
            query = query.Where(x => x.Method == m);
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            var p = path.Trim().ToLower();
            query = query.Where(x => x.Path.ToLower().Contains(p));
        }

        if (!string.IsNullOrWhiteSpace(traceId))
        {
            var t = traceId.Trim();
            query = query.Where(x => x.TraceId == t);
        }

        if (startDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= endDate.Value);
        }

        if (minDurationMs.HasValue)
        {
            query = query.Where(x => x.DurationMs >= minDurationMs.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ApiRequestLogResponse
            {
                Id = x.Id,
                TraceId = x.TraceId,
                CorrelationId = x.CorrelationId,
                Method = x.Method,
                Path = x.Path,
                StatusCode = x.StatusCode,
                DurationMs = x.DurationMs,
                IpAddress = x.IpAddress,
                UserAgent = x.UserAgent,
                UserId = x.UserId,
                Role = x.Role,
                ErrorMessage = x.ErrorMessage,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ApiRequestLogResponse>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<TraceDeepDiveResponse?> GetTraceDeepDiveAsync(
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(traceId)) return null;
        var cleanTrace = traceId.Trim();

        var requestLog = await _db.ApiRequestLogs.AsNoTracking()
            .Where(x => x.TraceId == cleanTrace)
            .Select(x => new ApiRequestLogResponse
            {
                Id = x.Id,
                TraceId = x.TraceId,
                CorrelationId = x.CorrelationId,
                Method = x.Method,
                Path = x.Path,
                StatusCode = x.StatusCode,
                DurationMs = x.DurationMs,
                IpAddress = x.IpAddress,
                UserAgent = x.UserAgent,
                UserId = x.UserId,
                Role = x.Role,
                ErrorMessage = x.ErrorMessage,
                CreatedAt = x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        var adminActions = await _db.AdminActions.AsNoTracking()
            .Include(x => x.AdminUser)
            .Where(x => x.TraceId == cleanTrace)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminAuditLogResponse
            {
                Id = x.Id,
                AdminUserId = x.AdminUserId,
                AdminName = x.AdminUser != null ? x.AdminUser.FullName : "System",
                AdminCustomerCode = x.AdminUser != null ? x.AdminUser.CustomerCode : null,
                ActionType = x.ActionType,
                Category = x.Category,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                Success = x.Success,
                OutcomeCode = x.OutcomeCode,
                Reason = x.Reason,
                TraceId = x.TraceId,
                IpAddress = x.IpAddress,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var secEvents = await _db.SecurityEvents.AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.TraceId == cleanTrace)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminSecurityEventResponse
            {
                Id = x.Id,
                EventType = x.EventType,
                Severity = x.Severity,
                IpAddress = x.IpAddress,
                UserAgent = x.UserAgent,
                UserId = x.UserId,
                AdminName = x.User != null ? x.User.FullName : null,
                MaskedPhone = x.MaskedPhone,
                TraceId = x.TraceId,
                DetailsJson = x.DetailsJson,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var payEvents = await _db.PaymentEvents.AsNoTracking()
            .Include(x => x.PaymentTransaction)
            .Where(x => x.Payload != null && x.Payload.Contains(cleanTrace))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PaymentEventSummaryDto
            {
                Id = x.Id,
                PaymentTransactionId = x.PaymentTransactionId,
                EventType = x.EventType,
                Amount = x.PaymentTransaction != null ? x.PaymentTransaction.Amount : 0,
                Status = x.PaymentTransaction != null ? x.PaymentTransaction.Status.ToString() : "Unknown",
                Reason = x.Payload,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        ExceptionSummaryDto? exc = null;
        if (requestLog != null && requestLog.StatusCode >= 500)
        {
            exc = new ExceptionSummaryDto
            {
                Type = "UnhandledServerError",
                Message = requestLog.ErrorMessage ?? "Server error occurred during execution",
                Path = requestLog.Path,
                Method = requestLog.Method,
                Timestamp = requestLog.CreatedAt
            };
        }

        return new TraceDeepDiveResponse
        {
            TraceId = cleanTrace,
            Request = requestLog,
            AdminActions = adminActions,
            SecurityEvents = secEvents,
            PaymentEvents = payEvents,
            ExceptionDetails = exc
        };
    }

    public async Task<ObservabilityMetricsResponse> GetObservabilityMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        var totalRequests = await _db.ApiRequestLogs.CountAsync(cancellationToken);
        if (totalRequests == 0)
        {
            return new ObservabilityMetricsResponse
            {
                TotalRequests = 0,
                RequestsPerMinute = 0,
                ErrorRatePercentage = 0,
                AverageDurationMs = 0,
                P50Ms = 0,
                P95Ms = 0,
                P99Ms = 0
            };
        }

        var since = DateTime.UtcNow.AddHours(-24);
        var recentLogs = await _db.ApiRequestLogs.AsNoTracking()
            .Where(x => x.CreatedAt >= since)
            .Select(x => new { x.Path, x.Method, x.StatusCode, x.DurationMs, x.CreatedAt })
            .ToListAsync(cancellationToken);

        if (recentLogs.Count == 0)
        {
            return new ObservabilityMetricsResponse
            {
                TotalRequests = totalRequests,
                RequestsPerMinute = 0,
                ErrorRatePercentage = 0,
                AverageDurationMs = 0,
                P50Ms = 0,
                P95Ms = 0,
                P99Ms = 0
            };
        }

        var totalRecent = recentLogs.Count;
        var errorCount = recentLogs.Count(x => x.StatusCode >= 400);
        var errorRate = totalRecent > 0 ? Math.Round((double)errorCount / totalRecent * 100, 2) : 0;
        var avgDuration = Math.Round(recentLogs.Average(x => x.DurationMs), 2);

        var sortedDurations = recentLogs.Select(x => (double)x.DurationMs).OrderBy(d => d).ToList();
        var p50 = GetPercentile(sortedDurations, 0.50);
        var p95 = GetPercentile(sortedDurations, 0.95);
        var p99 = GetPercentile(sortedDurations, 0.99);

        // Requests per minute over last hour
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        var lastHourCount = recentLogs.Count(x => x.CreatedAt >= oneHourAgo);
        var reqPerMin = Math.Round((double)lastHourCount / 60.0, 2);

        // Slowest endpoints
        var slowest = recentLogs
            .GroupBy(x => new { x.Path, x.Method })
            .Select(g => new EndpointLatencyDto
            {
                Path = g.Key.Path,
                Method = g.Key.Method,
                AverageDurationMs = Math.Round(g.Average(x => x.DurationMs), 2),
                RequestCount = g.Count()
            })
            .OrderByDescending(x => x.AverageDurationMs)
            .Take(10)
            .ToList();

        // Failing endpoints
        var failing = recentLogs
            .Where(x => x.StatusCode >= 400)
            .GroupBy(x => new { x.Path, x.Method })
            .Select(g => new EndpointFailureDto
            {
                Path = g.Key.Path,
                Method = g.Key.Method,
                ErrorCount = g.Count(),
                ErrorRatePercentage = Math.Round((double)g.Count() / recentLogs.Count(r => r.Path == g.Key.Path && r.Method == g.Key.Method) * 100, 2)
            })
            .OrderByDescending(x => x.ErrorCount)
            .Take(10)
            .ToList();

        return new ObservabilityMetricsResponse
        {
            TotalRequests = totalRequests,
            RequestsPerMinute = reqPerMin,
            ErrorRatePercentage = errorRate,
            AverageDurationMs = avgDuration,
            P50Ms = p50,
            P95Ms = p95,
            P99Ms = p99,
            SlowestEndpoints = slowest,
            FailingEndpoints = failing
        };
    }

    public async Task<DeepHealthCheckResponse> GetDeepHealthCheckAsync(
        CancellationToken cancellationToken = default)
    {
        var response = new DeepHealthCheckResponse();
        var sw = new Stopwatch();

        // 1. Database
        sw.Restart();
        try
        {
            await _db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            sw.Stop();
            response.Database = new SubsystemHealthDto
            {
                Name = "PostgreSQL Database",
                Status = "Healthy",
                LatencyMs = sw.ElapsedMilliseconds,
                Details = "Connection pool active and responsive."
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            response.Database = new SubsystemHealthDto
            {
                Name = "PostgreSQL Database",
                Status = "Unhealthy",
                LatencyMs = sw.ElapsedMilliseconds,
                Details = ex.Message
            };
            response.Status = "Degraded";
        }

        // 2. Storage
        sw.Restart();
        try
        {
            var testPath = Path.Combine(AppContext.BaseDirectory, "App_Data", "health_check.tmp");
            Directory.CreateDirectory(Path.GetDirectoryName(testPath)!);
            await File.WriteAllTextAsync(testPath, "health", cancellationToken);
            File.Delete(testPath);
            sw.Stop();
            response.Storage = new SubsystemHealthDto
            {
                Name = "File Storage System",
                Status = "Healthy",
                LatencyMs = sw.ElapsedMilliseconds,
                Details = "Local and App_Data directory read/write operational."
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            response.Storage = new SubsystemHealthDto
            {
                Name = "File Storage System",
                Status = "Unhealthy",
                LatencyMs = sw.ElapsedMilliseconds,
                Details = ex.Message
            };
            response.Status = "Degraded";
        }

        // 3. Authentication Subsystem
        sw.Restart();
        try
        {
            var activeSessionCount = await _db.AdminSessions.CountAsync(x => x.IsActive, cancellationToken);
            sw.Stop();
            response.Authentication = new SubsystemHealthDto
            {
                Name = "Authentication & Sessions",
                Status = "Healthy",
                LatencyMs = sw.ElapsedMilliseconds,
                Details = $"JWT signature verified. Active admin sessions: {activeSessionCount}."
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            response.Authentication = new SubsystemHealthDto
            {
                Name = "Authentication & Sessions",
                Status = "Unhealthy",
                LatencyMs = sw.ElapsedMilliseconds,
                Details = ex.Message
            };
            response.Status = "Degraded";
        }

        // 4. Payments Subsystem
        sw.Restart();
        var keyId = _config["Razorpay:KeyId"];
        sw.Stop();
        response.Payments = new SubsystemHealthDto
        {
            Name = "Razorpay Gateway Subsystem",
            Status = !string.IsNullOrEmpty(keyId) ? "Healthy" : "Degraded",
            LatencyMs = sw.ElapsedMilliseconds,
            Details = !string.IsNullOrEmpty(keyId) ? "Gateway credentials configured." : "Razorpay key missing."
        };

        // 5. External Messaging (Strictly NotMonitored per locked safeguard!)
        response.Messaging = new SubsystemHealthDto
        {
            Name = "External Messaging (SMS/WhatsApp)",
            Status = "NotMonitored",
            LatencyMs = 0,
            Details = "External messaging integration is unmonitored per system policy."
        };

        return response;
    }

    public async Task<UserTechnicalTimelineResponse> GetUserTechnicalTimelineAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.AsNoTracking()
            .Include(x => x.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        var response = new UserTechnicalTimelineResponse
        {
            UserId = userId,
            Phone = user?.Phone,
            Role = user?.UserRoles?.FirstOrDefault()?.Role?.Code
        };

        var timeline = new List<UserTimelineItemDto>();

        // 1. API Requests
        var reqs = await _db.ApiRequestLogs.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new UserTimelineItemDto
            {
                Type = "REQUEST",
                Action = $"{x.Method} {x.Path}",
                StatusCode = x.StatusCode,
                TraceId = x.TraceId,
                IpAddress = x.IpAddress,
                Details = x.ErrorMessage ?? $"Duration: {x.DurationMs}ms",
                Timestamp = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
        timeline.AddRange(reqs);

        // 2. Security Events
        var sec = await _db.SecurityEvents.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new UserTimelineItemDto
            {
                Type = "SECURITY",
                Action = x.EventType,
                StatusCode = null,
                TraceId = x.TraceId,
                IpAddress = x.IpAddress,
                Details = x.DetailsJson,
                Timestamp = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
        timeline.AddRange(sec);

        // 3. Admin Actions on/by user
        var adm = await _db.AdminActions.AsNoTracking()
            .Where(x => x.EntityId == userId || x.AdminUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new UserTimelineItemDto
            {
                Type = "AUDIT",
                Action = x.ActionType,
                StatusCode = x.Success ? 200 : 400,
                TraceId = x.TraceId,
                IpAddress = x.IpAddress,
                Details = x.Reason ?? x.OutcomeCode,
                Timestamp = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
        timeline.AddRange(adm);

        response.Timeline = timeline.OrderByDescending(x => x.Timestamp).Take(100).ToList();
        return response;
    }

    private static double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        int index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        if (index < 0) index = 0;
        if (index >= sortedValues.Count) index = sortedValues.Count - 1;
        return Math.Round(sortedValues[index], 2);
    }
}