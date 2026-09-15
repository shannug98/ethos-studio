using System.Security.Claims;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Infrastructure.Authentication;

public class AdminSessionValidationFilter : IAsyncActionFilter
{
    private readonly AppDbContext _db;
    private readonly ILogger<AdminSessionValidationFilter> _logger;

    public AdminSessionValidationFilter(
        AppDbContext db,
        ILogger<AdminSessionValidationFilter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var path = httpContext.Request.Path.Value ?? string.Empty;

        // Only enforce on /api/admin/* endpoints
        if (!path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        // Skip unauthenticated login/mfa endpoints
        if (path.Equals("/api/admin/auth/login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/admin/auth/verify-mfa", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        // If user is not authenticated or not in ADMIN role, let standard authorization filter handle 401/403
        var user = httpContext.User;
        if (user.Identity == null || !user.Identity.IsAuthenticated || !user.IsInRole("ADMIN"))
        {
            await next();
            return;
        }

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var sessionIdClaim = user.FindFirst("session_id")?.Value;
        var deviceIdClaim = user.FindFirst("device_id")?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) ||
            string.IsNullOrWhiteSpace(sessionIdClaim) ||
            string.IsNullOrWhiteSpace(deviceIdClaim) ||
            !Guid.TryParse(userIdClaim, out var userId) ||
            !Guid.TryParse(sessionIdClaim, out var sessionId) ||
            !Guid.TryParse(deviceIdClaim, out var deviceId))
        {
            context.Result = new ObjectResult(new
            {
                code = "INVALID_SESSION_CLAIMS",
                message = "Admin session claims missing or invalid."
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        // Fetch session and device
        var device = await _db.AdminDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId, httpContext.RequestAborted);

        // 1. Device validity check (Primary security boundary)
        if (device == null || device.Status != AdminDeviceStatus.Active || device.RevokedAt != null)
        {
            _logger.LogWarning("Admin request rejected: device {DeviceId} revoked", deviceId);
            context.Result = new ObjectResult(new
            {
                code = "DEVICE_REVOKED",
                message = "This device authorization has been revoked."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        // Fetch session
        var session = await _db.AdminSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, httpContext.RequestAborted);

        // 2. Session validity check
        if (session == null || !session.IsActive || session.RevokedAt != null || session.LoggedOutAt != null || session.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Admin request rejected: session {SessionId} expired, logged out, or revoked", sessionId);
            context.Result = new ObjectResult(new
            {
                code = "SESSION_EXPIRED",
                message = "Admin session has expired or been revoked."
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        // 3. Complete Relational Ownership Chain Cross-Validation
        // Validate: JWT.UserId == session.AdminUserId
        if (session.AdminUserId != userId)
        {
            _logger.LogWarning("Admin security violation: JWT userId {UserId} does not match session owner {SessionUserId}", userId, session.AdminUserId);
            context.Result = new ObjectResult(new
            {
                code = "IDENTITY_MISMATCH",
                message = "Session does not belong to authenticated user."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        // Validate: session.AdminDeviceId == deviceId
        if (session.AdminDeviceId != deviceId)
        {
            _logger.LogWarning("Admin security violation: session device {SessionDeviceId} does not match token device {TokenDeviceId}", session.AdminDeviceId, deviceId);
            context.Result = new ObjectResult(new
            {
                code = "DEVICE_MISMATCH",
                message = "Session is not associated with specified device."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        // Validate: device.AdminUserId == userId
        if (device.AdminUserId != userId)
        {
            _logger.LogWarning("Admin security violation: device owner {DeviceOwnerId} does not match JWT userId {UserId}", device.AdminUserId, userId);
            context.Result = new ObjectResult(new
            {
                code = "DEVICE_OWNER_MISMATCH",
                message = "Device does not belong to authenticated user."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        // Validate: device.Id == deviceId
        if (device.Id != deviceId)
        {
            _logger.LogWarning("Admin security violation: device ID {ActualDeviceId} does not match claim device ID {ClaimDeviceId}", device.Id, deviceId);
            context.Result = new ObjectResult(new
            {
                code = "DEVICE_MISMATCH",
                message = "Device identifier mismatch."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        // Telemetry update (throttled to once every 2 minutes)
        if (session.LastSeenAt < DateTime.UtcNow.AddMinutes(-2))
        {
            session.LastSeenAt = DateTime.UtcNow;
            device.LastSeenAt = DateTime.UtcNow;
            device.LastSeenIp = httpContext.Connection.RemoteIpAddress?.ToString();
            await _db.SaveChangesAsync(httpContext.RequestAborted);
        }

        await next();
    }
}
