using System.Security.Claims;
using System.Text.Json;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Ethos.Api.Middleware;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminAuthorizationService : IAdminAuthorizationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AdminAuthorizationService> _logger;

    public AdminAuthorizationService(
        AppDbContext db,
        ILogger<AdminAuthorizationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default)
    {
        if (!AdminPermissions.IsValid(permissionCode))
            return false;

        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);

        if (user == null)
            return false;

        return user.UserRoles.Any(ur => ur.Role != null && ur.Role.Code == "ADMIN");
    }

    public async Task<bool> HasAnyPermissionAsync(Guid userId, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default)
    {
        var codes = permissionCodes?.Where(AdminPermissions.IsValid).ToList();
        if (codes == null || codes.Count == 0)
            return false;

        return await HasPermissionAsync(userId, codes.First(), cancellationToken);
    }

    public async Task<bool> HasAllPermissionsAsync(Guid userId, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default)
    {
        var codes = permissionCodes?.ToList();
        if (codes == null || codes.Count == 0)
            return false;

        foreach (var code in codes)
        {
            if (!AdminPermissions.IsValid(code))
                return false;
        }

        return await HasPermissionAsync(userId, codes.First(), cancellationToken);
    }

    public async Task<AdminAuthorizationResult> AuthorizeActionAsync(
        ClaimsPrincipal user,
        string permissionCode,
        string? resourceType = null,
        Guid? resourceId = null,
        HttpContext? httpContext = null,
        CancellationToken cancellationToken = default)
    {
        var traceId = httpContext?.GetTraceId() ?? $"trc_{Guid.NewGuid():N}";
        var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();

        // 1. Authentication Check
        if (user?.Identity?.IsAuthenticated != true)
        {
            return new AdminAuthorizationResult
            {
                Success = false,
                StatusCode = 401,
                ErrorCode = "UNAUTHORIZED",
                ErrorMessage = "Administrative authentication is required."
            };
        }

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return new AdminAuthorizationResult
            {
                Success = false,
                StatusCode = 401,
                ErrorCode = "INVALID_TOKEN",
                ErrorMessage = "Invalid identity token."
            };
        }

        Guid? deviceId = null;
        if (Guid.TryParse(user.FindFirst("device_id")?.Value, out var parsedDevId))
            deviceId = parsedDevId;

        Guid? sessionId = null;
        if (Guid.TryParse(user.FindFirst("session_id")?.Value, out var parsedSessId))
            sessionId = parsedSessId;

        // 2. Validate Permission Code
        var isPermissionValid = AdminPermissions.IsValid(permissionCode);

        // 3. User & Role Verification in Database
        var dbUser = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        var isRoleAdmin = dbUser != null &&
            dbUser.IsActive &&
            dbUser.UserRoles.Any(ur => ur.Role != null && ur.Role.Code == "ADMIN");

        if (!isPermissionValid || !isRoleAdmin)
        {
            var denialReason = !isPermissionValid ? "UNKNOWN_PERMISSION" : (!isRoleAdmin ? "ROLE_NOT_ADMIN" : "DENIED");

            _logger.LogWarning(
                "Admin authorization denied: User {UserId}, Permission {Permission}, Reason {Reason}, TraceId {TraceId}",
                userId, permissionCode, denialReason, traceId);

            // Authoritative single event logging for denial
            var secEvent = new SecurityEvent
            {
                Id = Guid.NewGuid(),
                EventType = "ADMIN_AUTHORIZATION_DENIED",
                Severity = "WARNING",
                IpAddress = ipAddress,
                UserAgent = userAgent,
                UserId = userId,
                AdminDeviceId = deviceId,
                AdminSessionId = sessionId,
                TraceId = traceId,
                MaskedPhone = MaskPhone(dbUser?.Phone),
                DetailsJson = JsonSerializer.Serialize(new
                {
                    permission = permissionCode,
                    resourceType,
                    resourceId = resourceId?.ToString(),
                    reason = denialReason
                }),
                CreatedAt = DateTime.UtcNow
            };

            _db.SecurityEvents.Add(secEvent);
            await _db.SaveChangesAsync(cancellationToken);

            return new AdminAuthorizationResult
            {
                Success = false,
                StatusCode = 403,
                ErrorCode = "FORBIDDEN",
                ErrorMessage = "You do not have administrative permission for this action.",
                AdminUserId = userId,
                AdminCustomerCode = dbUser?.CustomerCode,
                AdminDeviceId = deviceId,
                AdminSessionId = sessionId
            };
        }

        return new AdminAuthorizationResult
        {
            Success = true,
            StatusCode = 200,
            AdminUserId = userId,
            AdminCustomerCode = dbUser?.CustomerCode,
            AdminDeviceId = deviceId,
            AdminSessionId = sessionId
        };
    }

    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 4) return phone;
        return string.Concat("******", phone.AsSpan(phone.Length - 4));
    }
}
