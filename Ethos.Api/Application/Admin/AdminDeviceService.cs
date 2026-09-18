using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminDeviceService : IAdminDeviceService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AdminDeviceService> _logger;

    public AdminDeviceService(
        AppDbContext db,
        ILogger<AdminDeviceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> CleanupInactiveSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-5);

        var staleSessions = await _db.AdminSessions
            .Where(s => s.IsActive && s.RevokedAt == null && s.LoggedOutAt == null && (s.ExpiresAt <= now || s.LastSeenAt < cutoff))
            .ToListAsync(cancellationToken);

        if (staleSessions.Count == 0)
        {
            return 0;
        }

        foreach (var s in staleSessions)
        {
            s.IsActive = false;
            s.RevokedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return staleSessions.Count;
    }

    public async Task<DeviceAuthResult> CanAttemptLoginAsync(
        Guid userId,
        string? rawDeviceCredential,
        CancellationToken cancellationToken = default)
    {
        await CleanupInactiveSessionsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-5);

        // 1. Fetch genuinely active sessions for THIS ADMIN
        var activeSessions = await _db.AdminSessions
            .Include(x => x.AdminDevice)
            .Include(x => x.AdminUser)
            .Where(x =>
                x.AdminUserId == userId &&
                x.IsActive &&
                x.RevokedAt == null &&
                x.LoggedOutAt == null &&
                x.ExpiresAt > now)
            .OrderByDescending(x => x.LastSeenAt)
            .ToListAsync(cancellationToken);

        // 2. If an existing device credential was presented, check if it's the same system
        if (!string.IsNullOrWhiteSpace(rawDeviceCredential))
        {
            var hash = HashToken(rawDeviceCredential.Trim());
            var device = await _db.AdminDevices
                .FirstOrDefaultAsync(d => d.DeviceCredentialHash == hash, cancellationToken);

            if (device != null)
            {
                if (device.Status == AdminDeviceStatus.Revoked || device.RevokedAt != null)
                {
                    return new DeviceAuthResult
                    {
                        Success = false,
                        ErrorCode = "DEVICE_REVOKED",
                        ErrorMessage = "This device authorization has been revoked."
                    };
                }

                if (device.AdminUserId != userId)
                {
                    return new DeviceAuthResult
                    {
                        Success = false,
                        ErrorCode = "DEVICE_NOT_AUTHORIZED",
                        ErrorMessage = "Device credential belongs to another administrative account."
                    };
                }

                // Count other active devices for THIS ADMIN
                var otherActiveCount = activeSessions
                    .Where(s => s.AdminDeviceId != device.Id)
                    .Select(s => s.AdminDeviceId)
                    .Distinct()
                    .Count();

                if (otherActiveCount >= 2)
                {
                    return new DeviceAuthResult
                    {
                        Success = false,
                        ErrorCode = "DEVICE_AUTHORIZATION_BLOCKED",
                        ErrorMessage = "Both approved device sessions are currently active. Please sign out from one approved device to continue.",
                        ActiveSessions = MapSessions(activeSessions)
                    };
                }

                return new DeviceAuthResult
                {
                    Success = true,
                    Device = device
                };
            }
        }

        // 3. New system / unrecognized credential: Count distinct active devices for THIS ADMIN
        var distinctActiveDevices = activeSessions
            .Select(s => s.AdminDeviceId)
            .Distinct()
            .Count();

        if (distinctActiveDevices >= 2)
        {
            return new DeviceAuthResult
            {
                Success = false,
                ErrorCode = "DEVICE_AUTHORIZATION_BLOCKED",
                ErrorMessage = "Both approved device sessions are currently active. Please sign out from one approved device to continue.",
                ActiveSessions = MapSessions(activeSessions)
            };
        }

        return new DeviceAuthResult
        {
            Success = true
        };
    }

    public async Task<DeviceAuthResult> ValidateOrRegisterDeviceAsync(
        Guid userId,
        string? rawDeviceCredential,
        string? rawDeviceName,
        string? ipAddress,
        string? userAgent,
        string? fingerprintTelemetry,
        CancellationToken cancellationToken = default)
    {
        await CleanupInactiveSessionsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-5);

        // Fetch active sessions for THIS ADMIN
        var activeSessions = await _db.AdminSessions
            .Include(x => x.AdminDevice)
            .Include(x => x.AdminUser)
            .Where(x =>
                x.AdminUserId == userId &&
                x.IsActive &&
                x.RevokedAt == null &&
                x.LoggedOutAt == null &&
                x.ExpiresAt > now)
            .OrderByDescending(x => x.LastSeenAt)
            .ToListAsync(cancellationToken);

        // 1. Check if an existing credential was presented
        if (!string.IsNullOrWhiteSpace(rawDeviceCredential))
        {
            var hash = HashToken(rawDeviceCredential.Trim());
            var device = await _db.AdminDevices
                .Include(d => d.AdminUser)
                .FirstOrDefaultAsync(d => d.DeviceCredentialHash == hash, cancellationToken);

            if (device != null)
            {
                // A. Check if the device was revoked
                if (device.Status == AdminDeviceStatus.Revoked || device.RevokedAt != null)
                {
                    _logger.LogWarning("Login attempt on revoked admin device {DeviceId} from IP {IpAddress}", device.Id, ipAddress);
                    await RecordSecurityEventAsync(
                        "ADMIN_REVOKED_DEVICE_LOGIN_ATTEMPT",
                        "WARNING",
                        ipAddress,
                        userAgent,
                        userId,
                        new { deviceId = device.Id, deviceName = device.DeviceName },
                        cancellationToken);

                    return new DeviceAuthResult
                    {
                        Success = false,
                        ErrorCode = "DEVICE_REVOKED",
                        ErrorMessage = "This device authorization has been revoked."
                    };
                }

                // B. Identity Binding: Verify AdminUserId matches authenticated Admin
                if (device.AdminUserId != userId)
                {
                    _logger.LogWarning("Identity mismatch: Partner {UserId} presented device credential belonging to {DeviceOwnerId}", userId, device.AdminUserId);
                    await RecordSecurityEventAsync(
                        "ADMIN_DEVICE_IDENTITY_MISMATCH",
                        "WARNING",
                        ipAddress,
                        userAgent,
                        userId,
                        new { targetDeviceId = device.Id, actualOwner = device.AdminUserId },
                        cancellationToken);

                    return new DeviceAuthResult
                    {
                        Success = false,
                        ErrorCode = "DEVICE_NOT_AUTHORIZED",
                        ErrorMessage = "Device credential belongs to another administrative account."
                    };
                }

                // Check other active device count for THIS ADMIN
                var otherActiveCount = activeSessions
                    .Where(s => s.AdminDeviceId != device.Id)
                    .Select(s => s.AdminDeviceId)
                    .Distinct()
                    .Count();

                if (otherActiveCount >= 2)
                {
                    return new DeviceAuthResult
                    {
                        Success = false,
                        ErrorCode = "DEVICE_AUTHORIZATION_BLOCKED",
                        ErrorMessage = "Both approved device sessions are currently active. Please sign out from one approved device to continue.",
                        ActiveSessions = MapSessions(activeSessions)
                    };
                }

                // C. Approved Active Device: Update telemetry, status, and return
                device.Status = AdminDeviceStatus.Active;
                device.RevokedAt = null;
                device.LastSeenAt = now;
                device.LastSeenIp = ipAddress;
                device.UserAgent = userAgent;
                if (!string.IsNullOrWhiteSpace(fingerprintTelemetry))
                {
                    device.FingerprintTelemetry = fingerprintTelemetry;
                }

                await _db.SaveChangesAsync(cancellationToken);

                return new DeviceAuthResult
                {
                    Success = true,
                    Device = device,
                    IssuedRawCredential = null
                };
            }
        }

        // 2. Unrecognized or no credential: Enter Atomic Device Registration
        // Using PostgreSQL transaction and advisory lock to strictly guarantee maximum 2 devices
        var isRelational = _db.Database.IsRelational();
        using var transaction = isRelational
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;
        try
        {
            if (isRelational)
            {
                // Acquire exclusive transaction advisory lock (key 182001)
                await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(182001);", cancellationToken);
            }

            var activeDeviceCount = await _db.AdminSessions
                .Where(s =>
                    s.AdminUserId == userId &&
                    s.IsActive &&
                    s.RevokedAt == null &&
                    s.LoggedOutAt == null &&
                    s.ExpiresAt > now)
                .Select(s => s.AdminDeviceId)
                .Distinct()
                .CountAsync(cancellationToken);

            if (activeDeviceCount >= 2)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                _logger.LogWarning("Admin device limit exceeded for user {UserId}. Active: {Count}/2. Blocked new device from IP {IpAddress}", userId, activeDeviceCount, ipAddress);

                await RecordSecurityEventAsync(
                    "UNAPPROVED_DEVICE_LOGIN_BLOCKED",
                    "CRITICAL",
                    ipAddress,
                    userAgent,
                    userId,
                    new { activeDeviceCount, attemptedDeviceName = rawDeviceName },
                    cancellationToken);

                return new DeviceAuthResult
                {
                    Success = false,
                    ErrorCode = "DEVICE_AUTHORIZATION_BLOCKED",
                    ErrorMessage = "Both approved device sessions are currently active. Please sign out from one approved device to continue.",
                    ActiveSessions = MapSessions(activeSessions)
                };
            }

            // Generate 256-bit cryptographic secret and hash
            var rawSecret = GenerateCryptographicSecret("ethdev_");
            var secretHash = HashToken(rawSecret);
            var sanitizedName = SanitizeDeviceName(rawDeviceName, userAgent);

            var newDevice = new AdminDevice
            {
                Id = Guid.NewGuid(),
                AdminUserId = userId,
                DeviceCredentialHash = secretHash,
                DeviceName = sanitizedName,
                Status = AdminDeviceStatus.Active,
                RegisteredAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                LastSeenIp = ipAddress,
                UserAgent = userAgent,
                FingerprintTelemetry = fingerprintTelemetry,
                RevokedAt = null
            };

            _db.AdminDevices.Add(newDevice);
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            _logger.LogInformation("Successfully registered new admin device {DeviceId} ({DeviceName}) for user {UserId}", newDevice.Id, sanitizedName, userId);

            await RecordSecurityEventAsync(
                "ADMIN_DEVICE_REGISTERED",
                "INFO",
                ipAddress,
                userAgent,
                userId,
                new { deviceId = newDevice.Id, deviceName = sanitizedName, activeDeviceCount = activeDeviceCount + 1 },
                cancellationToken);

            return new DeviceAuthResult
            {
                Success = true,
                Device = newDevice,
                IssuedRawCredential = rawSecret
            };
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _logger.LogError(ex, "Error occurred during atomic device registration");
            throw;
        }
    }

    public async Task<(AdminSession Session, string RawSessionToken)> CreateSessionAsync(
        Guid deviceId,
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var rawSessionToken = GenerateCryptographicSecret("ethsess_");
        var sessionHash = HashToken(rawSessionToken);

        // Revoke any previous active sessions for this device (re-login on same system)
        var existingSessions = await _db.AdminSessions
            .Where(s => s.AdminDeviceId == deviceId && s.IsActive && s.RevokedAt == null && s.LoggedOutAt == null)
            .ToListAsync(cancellationToken);

        foreach (var oldSession in existingSessions)
        {
            oldSession.IsActive = false;
            oldSession.RevokedAt = DateTime.UtcNow;
        }

        var session = new AdminSession
        {
            Id = Guid.NewGuid(),
            AdminDeviceId = deviceId,
            AdminUserId = userId,
            SessionTokenHash = sessionHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            LastSeenAt = DateTime.UtcNow,
            RevokedAt = null,
            LoggedOutAt = null,
            IsActive = true
        };

        _db.AdminSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        return (session, rawSessionToken);
    }

    public async Task<bool> HeartbeatSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.AdminSessions
            .Include(s => s.AdminDevice)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null || !session.IsActive || session.RevokedAt != null || session.LoggedOutAt != null || session.ExpiresAt <= DateTime.UtcNow)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        session.LastSeenAt = now;
        if (session.AdminDevice != null)
        {
            session.AdminDevice.LastSeenAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<AdminDeviceResponse>> GetDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        await CleanupInactiveSessionsAsync(cancellationToken);
        var cutoff = DateTime.UtcNow.AddMinutes(-5);

        var activeDeviceIds = await _db.AdminSessions
            .Where(s => s.IsActive && s.RevokedAt == null && s.LoggedOutAt == null && s.ExpiresAt > DateTime.UtcNow && s.LastSeenAt >= cutoff)
            .Select(s => s.AdminDeviceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var devices = await _db.AdminDevices
            .Include(d => d.AdminUser)
            .OrderByDescending(d => d.LastSeenAt)
            .ToListAsync(cancellationToken);

        return devices.Select(d =>
        {
            string status;
            if (d.Status == AdminDeviceStatus.Revoked || d.RevokedAt != null)
            {
                status = "Revoked";
            }
            else if (activeDeviceIds.Contains(d.Id))
            {
                status = "Active";
            }
            else
            {
                status = "Inactive";
            }

            return new AdminDeviceResponse
            {
                Id = d.Id,
                AdminUserId = d.AdminUserId,
                AdminFullName = d.AdminUser?.FullName ?? "Ethos Admin",
                AdminPhone = d.AdminUser?.Phone ?? string.Empty,
                DeviceName = d.DeviceName,
                Status = status,
                RegisteredAt = d.RegisteredAt,
                LastSeenAt = d.LastSeenAt,
                LastSeenIp = d.LastSeenIp,
                UserAgent = d.UserAgent
            };
        }).ToList();
    }

    public async Task<bool> RevokeDeviceAsync(
        Guid deviceId,
        Guid requestingUserId,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var device = await _db.AdminDevices
            .Include(d => d.AdminUser)
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

        if (device == null)
        {
            return false;
        }

        if (device.Status == AdminDeviceStatus.Revoked && device.RevokedAt != null)
        {
            return true;
        }

        device.Status = AdminDeviceStatus.Revoked;
        device.RevokedAt = DateTime.UtcNow;

        // Invalidate all sessions tied to this device
        var activeSessions = await _db.AdminSessions
            .Where(s => s.AdminDeviceId == deviceId && s.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            session.IsActive = false;
            session.RevokedAt = DateTime.UtcNow;
        }

        await RecordSecurityEventAsync(
            "ADMIN_DEVICE_REVOKED",
            "WARNING",
            ipAddress,
            userAgent,
            requestingUserId,
            new { revokedDeviceId = device.Id, deviceName = device.DeviceName, terminatedSessions = activeSessions.Count },
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<AdminSessionResponse>> GetActiveSessionsAsync(
        Guid? userId = null,
        Guid? currentSessionId = null,
        CancellationToken cancellationToken = default)
    {
        await CleanupInactiveSessionsAsync(cancellationToken);

        var query = _db.AdminSessions
            .Include(s => s.AdminDevice)
            .Include(s => s.AdminUser)
            .Where(s => s.IsActive && s.RevokedAt == null && s.LoggedOutAt == null && s.ExpiresAt > DateTime.UtcNow);

        if (userId.HasValue)
        {
            query = query.Where(s => s.AdminUserId == userId.Value);
        }

        var sessions = await query
            .OrderByDescending(s => s.LastSeenAt)
            .ToListAsync(cancellationToken);

        return MapSessions(sessions, currentSessionId);
    }

    public async Task<bool> RevokeSessionAsync(
        Guid sessionId,
        Guid requestingUserId,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.AdminSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            return false;
        }

        session.IsActive = false;
        session.LoggedOutAt = DateTime.UtcNow;
        session.RevokedAt = DateTime.UtcNow;

        await RecordSecurityEventAsync(
            "ADMIN_SESSION_REVOKED",
            "INFO",
            ipAddress,
            userAgent,
            requestingUserId,
            new { sessionId },
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> LogoutSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.AdminSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            return false;
        }

        session.IsActive = false;
        session.LoggedOutAt = DateTime.UtcNow;
        session.RevokedAt = null;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> LogoutAllSessionsAsync(
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AdminSessions.Where(s => s.IsActive);
        if (userId.HasValue)
        {
            query = query.Where(s => s.AdminUserId == userId.Value);
        }

        var sessions = await query.ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var s in sessions)
        {
            s.IsActive = false;
            s.LoggedOutAt = now;
            s.RevokedAt = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return sessions.Count;
    }

    public static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string GenerateCryptographicSecret(string prefix)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return $"{prefix}{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    public static string SanitizeDeviceName(string? rawName, string? userAgent)
    {
        var (browser, os) = ParseUserAgent(userAgent);
        return $"{os} · {browser}";
    }

    public static (string Browser, string OperatingSystem) ParseUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return ("Chrome", "Windows");
        }

        string os = "Windows";
        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)) os = "iPhone";
        else if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)) os = "iPad";
        else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)) os = "Android";
        else if (userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase)) os = "macOS";
        else if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase)) os = "Windows";
        else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase)) os = "Linux";

        string browser = "Chrome";
        if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) browser = "Edge";
        else if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) && !userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) browser = "Chrome";
        else if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)) browser = "Firefox";
        else if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase) && !userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase)) browser = "Safari";

        return (browser, os);
    }

    private static List<AdminSessionResponse> MapSessions(List<AdminSession> sessions, Guid? currentSessionId = null)
    {
        return sessions.Select(s =>
        {
            var (browser, os) = ParseUserAgent(s.AdminDevice?.UserAgent);
            var deviceLabel = $"{os} · {browser}";
            return new AdminSessionResponse
            {
                Id = s.Id,
                AdminDeviceId = s.AdminDeviceId,
                DeviceName = deviceLabel,
                Browser = browser,
                OperatingSystem = os,
                AdminUserId = s.AdminUserId,
                PartnerName = s.AdminUser?.FullName ?? "Ethos Partner 1",
                PartnerPhone = s.AdminUser?.Phone ?? string.Empty,
                CreatedAt = s.CreatedAt,
                LastActivityAt = s.LastSeenAt,
                LastSeenAt = s.LastSeenAt,
                ExpiresAt = s.ExpiresAt,
                IsActive = s.IsActive,
                IsRevoked = s.RevokedAt != null,
                RevokedAt = s.RevokedAt,
                LoggedOutAt = s.LoggedOutAt,
                IsCurrent = currentSessionId.HasValue && s.Id == currentSessionId.Value
            };
        }).ToList();
    }

    private async Task RecordSecurityEventAsync(
        string eventType,
        string severity,
        string? ipAddress,
        string? userAgent,
        Guid? userId,
        object details,
        CancellationToken cancellationToken)
    {
        try
        {
            var securityEvent = new SecurityEvent
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                Severity = severity,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                UserId = userId,
                DetailsJson = JsonSerializer.Serialize(details),
                CreatedAt = DateTime.UtcNow
            };

            _db.SecurityEvents.Add(securityEvent);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record security event {EventType}", eventType);
        }
    }
}
