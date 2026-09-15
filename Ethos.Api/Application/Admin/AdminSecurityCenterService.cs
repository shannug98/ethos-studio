using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public interface IAdminSecurityCenterService
{
    Task<SecurityFleetResponse> GetFleetSecurityAsync(CancellationToken cancellationToken = default);

    Task<SecurityThreatsSummaryResponse> GetThreatSummaryAsync(CancellationToken cancellationToken = default);

    Task<SecurityInvestigationResponse> InvestigateEntityAsync(
        string targetType,
        string targetValue,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeSessionAsync(
        Guid sessionId,
        Guid adminUserId,
        string reason,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeDeviceAsync(
        Guid deviceId,
        Guid adminUserId,
        string reason,
        string traceId,
        CancellationToken cancellationToken = default);
}

public class AdminSecurityCenterService : IAdminSecurityCenterService
{
    private readonly AppDbContext _db;

    public AdminSecurityCenterService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SecurityFleetResponse> GetFleetSecurityAsync(CancellationToken cancellationToken = default)
    {
        var devices = await _db.AdminDevices.AsNoTracking()
            .Include(x => x.AdminUser)
            .OrderByDescending(x => x.RegisteredAt)
            .ToListAsync(cancellationToken);

        var activeDevices = devices.Where(x => x.Status == AdminDeviceStatus.Active).ToList();
        var revokedDevices = devices.Where(x => x.Status == AdminDeviceStatus.Revoked).ToList();

        var sessions = await _db.AdminSessions.AsNoTracking()
            .Include(x => x.AdminUser)
            .Include(x => x.AdminDevice)
            .Where(x => x.IsActive && x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return new SecurityFleetResponse
        {
            MaxAllowedSlots = 2,
            ActiveSlotCount = activeDevices.Count,
            ActiveDevices = activeDevices.Select(d => new AdminDeviceResponse
            {
                Id = d.Id,
                AdminUserId = d.AdminUserId,
                AdminFullName = d.AdminUser?.FullName ?? "Admin",
                AdminPhone = d.AdminUser?.Phone ?? "",
                DeviceName = d.DeviceName,
                Status = d.Status.ToString(),
                RegisteredAt = d.RegisteredAt,
                LastSeenAt = d.LastSeenAt,
                LastSeenIp = d.LastSeenIp,
                UserAgent = d.UserAgent
            }).ToList(),
            ActiveSessions = sessions.Select(s => new AdminSessionResponse
            {
                Id = s.Id,
                AdminUserId = s.AdminUserId,
                AdminDeviceId = s.AdminDeviceId,
                DeviceName = s.AdminDevice?.DeviceName ?? "Unknown Device",
                CreatedAt = s.CreatedAt,
                ExpiresAt = s.ExpiresAt,
                LastSeenAt = s.LastSeenAt,
                IsActive = s.IsActive
            }).ToList(),
            RevokedDevices = revokedDevices.Select(d => new AdminDeviceResponse
            {
                Id = d.Id,
                AdminUserId = d.AdminUserId,
                AdminFullName = d.AdminUser?.FullName ?? "Admin",
                AdminPhone = d.AdminUser?.Phone ?? "",
                DeviceName = d.DeviceName,
                Status = d.Status.ToString(),
                RegisteredAt = d.RegisteredAt,
                LastSeenAt = d.LastSeenAt,
                LastSeenIp = d.LastSeenIp,
                UserAgent = d.UserAgent
            }).ToList()
        };
    }

    public async Task<SecurityThreatsSummaryResponse> GetThreatSummaryAsync(CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var recentEvents = await _db.SecurityEvents.AsNoTracking()
            .Where(x => x.CreatedAt >= since)
            .ToListAsync(cancellationToken);

        // 1. Failed Logins by IP
        var loginFailures = recentEvents
            .Where(x => x.EventType.Contains("LOGIN_FAILED") || x.EventType.Contains("AUTH_FAILED"))
            .GroupBy(x => x.IpAddress ?? "Unknown")
            .Select(g => new FailedLoginClusterDto
            {
                IpAddress = g.Key,
                TargetedPhones = g.Where(x => !string.IsNullOrEmpty(x.MaskedPhone)).Select(x => x.MaskedPhone!).Distinct().ToList(),
                AttemptCount = g.Count(),
                FirstAttempt = g.Min(x => x.CreatedAt),
                LastAttempt = g.Max(x => x.CreatedAt),
                Severity = g.Count() >= 10 ? "CRITICAL" : (g.Count() >= 5 ? "HIGH" : "WARNING")
            })
            .OrderByDescending(x => x.AttemptCount)
            .Take(10)
            .ToList();

        // 2. Authorization Denials by User
        var authDenials = recentEvents
            .Where(x => x.EventType == "ADMIN_AUTHORIZATION_DENIED")
            .GroupBy(x => x.UserId)
            .Select(g => new AuthorizationDenialClusterDto
            {
                UserId = g.Key,
                AdminCustomerCode = "ADMIN_USER",
                AttemptedPermission = g.FirstOrDefault()?.DetailsJson ?? "ADMIN_PERMISSION",
                DenialCount = g.Count(),
                LastAttempt = g.Max(x => x.CreatedAt)
            })
            .OrderByDescending(x => x.DenialCount)
            .Take(10)
            .ToList();

        // 3. Device Violations
        var deviceViolations = recentEvents
            .Where(x => x.EventType.Contains("DEVICE_NOT_AUTHORIZED") || x.EventType.Contains("DEVICE_REVOKED") || x.EventType.Contains("DEVICE_LIMIT_EXCEEDED"))
            .GroupBy(x => new { Ip = x.IpAddress ?? "Unknown", x.AdminDeviceId, x.EventType })
            .Select(g => new DeviceViolationClusterDto
            {
                IpAddress = g.Key.Ip,
                DeviceId = g.Key.AdminDeviceId,
                ViolationType = g.Key.EventType,
                AttemptCount = g.Count(),
                LastAttempt = g.Max(x => x.CreatedAt)
            })
            .OrderByDescending(x => x.AttemptCount)
            .Take(10)
            .ToList();

        var criticalCount = recentEvents.Count(x => x.Severity == "CRITICAL" || x.Severity == "HIGH");

        return new SecurityThreatsSummaryResponse
        {
            FailedLoginClusters = loginFailures,
            AuthorizationDenialClusters = authDenials,
            DeviceViolationClusters = deviceViolations,
            TotalSecurityEventsLast24h = recentEvents.Count,
            CriticalThreatCount = criticalCount
        };
    }

    public async Task<SecurityInvestigationResponse> InvestigateEntityAsync(
        string targetType,
        string targetValue,
        CancellationToken cancellationToken = default)
    {
        var cleanType = (targetType ?? "IP").Trim().ToUpperInvariant();
        var cleanVal = (targetValue ?? "").Trim();

        var query = _db.SecurityEvents.AsNoTracking().AsQueryable();

        if (cleanType == "IP")
        {
            query = query.Where(x => x.IpAddress == cleanVal);
        }
        else if (cleanType == "USER" && Guid.TryParse(cleanVal, out var uId))
        {
            query = query.Where(x => x.UserId == uId);
        }
        else if (cleanType == "DEVICE" && Guid.TryParse(cleanVal, out var dId))
        {
            query = query.Where(x => x.AdminDeviceId == dId);
        }
        else if (cleanType == "TRACE")
        {
            query = query.Where(x => x.TraceId == cleanVal);
        }

        var events = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        // Deterministic Risk Scoring (User Hard Rule 3)
        var factors = new List<RiskFactorDto>();
        int score = 0;

        var failedLogins = events.Count(x => x.EventType.Contains("LOGIN_FAILED") || x.EventType.Contains("AUTH_FAILED"));
        if (failedLogins > 0)
        {
            var impact = Math.Min(40, failedLogins * 10);
            score += impact;
            factors.Add(new RiskFactorDto
            {
                FactorName = "Failed Authentication Attempts",
                ScoreImpact = impact,
                Evidence = $"{failedLogins} failed authentication attempt(s) detected (+10 pts each, max 40)."
            });
        }

        var authDenials = events.Count(x => x.EventType == "ADMIN_AUTHORIZATION_DENIED");
        if (authDenials > 0)
        {
            var impact = Math.Min(30, authDenials * 15);
            score += impact;
            factors.Add(new RiskFactorDto
            {
                FactorName = "Authorization Privilege Denials",
                ScoreImpact = impact,
                Evidence = $"{authDenials} unauthorized administrative access attempt(s) (+15 pts each, max 30)."
            });
        }

        var deviceViolations = events.Count(x => x.EventType.Contains("DEVICE_NOT_AUTHORIZED") || x.EventType.Contains("DEVICE_REVOKED"));
        if (deviceViolations > 0)
        {
            var impact = Math.Min(50, deviceViolations * 25);
            score += impact;
            factors.Add(new RiskFactorDto
            {
                FactorName = "Device Slot / Credential Tampering",
                ScoreImpact = impact,
                Evidence = $"{deviceViolations} device security violation(s) (+25 pts each, max 50)."
            });
        }

        if (score > 100) score = 100;

        string riskLevel = score switch
        {
            >= 75 => "CRITICAL",
            >= 50 => "HIGH",
            >= 25 => "MEDIUM",
            _ => "LOW"
        };

        var policy = "Deterministic Risk Policy v1: FailedLogins (10pts/ea, max 40) + AuthDenials (15pts/ea, max 30) + DeviceViolations (25pts/ea, max 50). Capped at 100.";

        return new SecurityInvestigationResponse
        {
            TargetType = cleanType,
            TargetValue = cleanVal,
            RiskScore = score,
            RiskLevel = riskLevel,
            ScoringPolicy = policy,
            ContributingFactors = factors,
            EventHistory = events.Select(x => new AdminSecurityEventResponse
            {
                Id = x.Id,
                EventType = x.EventType,
                Severity = x.Severity,
                IpAddress = x.IpAddress,
                UserAgent = x.UserAgent,
                UserId = x.UserId,
                MaskedPhone = x.MaskedPhone,
                TraceId = x.TraceId,
                DetailsJson = x.DetailsJson,
                CreatedAt = x.CreatedAt
            }).ToList()
        };
    }

    public async Task<bool> RevokeSessionAsync(
        Guid sessionId,
        Guid adminUserId,
        string reason,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.AdminSessions
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        if (session == null) return false;

        session.IsActive = false;
        session.ExpiresAt = DateTime.UtcNow;

        _db.AdminActions.Add(new AdminAction
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            ActionType = "ADMIN_SESSION_TERMINATED",
            Category = "SECURITY",
            EntityType = "ADMIN_SESSION",
            EntityId = sessionId,
            Success = true,
            Reason = reason,
            TraceId = traceId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RevokeDeviceAsync(
        Guid deviceId,
        Guid adminUserId,
        string reason,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var device = await _db.AdminDevices
            .FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);

        if (device == null) return false;

        device.Status = AdminDeviceStatus.Revoked;
        device.RevokedAt = DateTime.UtcNow;

        // Revoke all active sessions on this device
        var sessions = await _db.AdminSessions
            .Where(x => x.AdminDeviceId == deviceId && x.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var s in sessions)
        {
            s.IsActive = false;
            s.ExpiresAt = DateTime.UtcNow;
        }

        _db.AdminActions.Add(new AdminAction
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            ActionType = "ADMIN_DEVICE_REVOKED",
            Category = "SECURITY",
            EntityType = "ADMIN_DEVICE",
            EntityId = deviceId,
            Success = true,
            Reason = reason,
            TraceId = traceId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}