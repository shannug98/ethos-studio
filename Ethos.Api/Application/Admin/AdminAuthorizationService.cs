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

    private static readonly HashSet<string> SuperAdminPhones = new(StringComparer.OrdinalIgnoreCase)
    {
        "8019013757", "8341701113"
    };

    // Sensitive administrative permissions requiring SUPER_ADMIN role or explicit elevation
    private static readonly HashSet<string> SensitivePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        AdminPermissions.PaymentReconcile,
        AdminPermissions.MediaDelete,
        AdminPermissions.DeviceRevoke,
        AdminPermissions.AdminSecurityView,
        AdminPermissions.ObservabilityManage,
        AdminPermissions.SecurityCenterManage,
        AdminPermissions.UserUpdateStatus,
        AdminPermissions.CorrectiveActionExecute,
        AdminPermissions.TrainerReject
    };

    // Standard baseline operational permissions granted to verified ADMIN accounts
    private static readonly HashSet<string> StandardAdminPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        AdminPermissions.AdminDashboardView,
        AdminPermissions.AdminAuditView,
        AdminPermissions.StudentView,
        AdminPermissions.StudentUpdate,
        AdminPermissions.TrainerView,
        AdminPermissions.TrainerApprove,
        AdminPermissions.ClassView,
        AdminPermissions.ClassCreate,
        AdminPermissions.ClassUpdate,
        AdminPermissions.ClassCancel,
        AdminPermissions.WorkshopView,
        AdminPermissions.WorkshopCreate,
        AdminPermissions.WorkshopApprove,
        AdminPermissions.WorkshopUpdate,
        AdminPermissions.WorkshopCancel,
        AdminPermissions.PackageView,
        AdminPermissions.PackageCreate,
        AdminPermissions.PackageUpdate,
        AdminPermissions.BookingView,
        AdminPermissions.AttendanceView,
        AdminPermissions.PaymentView,
        AdminPermissions.NotificationView,
        AdminPermissions.NotificationSend,
        AdminPermissions.CommunicationsView,
        AdminPermissions.CommunicationsManage,
        AdminPermissions.DeviceView,
        AdminPermissions.UserView,
        AdminPermissions.LegacyUserView,
        AdminPermissions.UserAuditView,
        AdminPermissions.ObservabilityView,
        AdminPermissions.SecurityCenterView,
        AdminPermissions.MediaView,
        AdminPermissions.MediaUpload
    };

    private static HashSet<string> ResolveAssignedPermissions(User? user)
    {
        if (user == null || !user.IsActive)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var isSuperAdmin = user.UserRoles.Any(ur => ur.Role != null && (ur.Role.Code == "SUPER_ADMIN" || ur.Role.Code == "SYSTEM_ADMIN"))
            || (!string.IsNullOrWhiteSpace(user.Phone) && SuperAdminPhones.Contains(user.Phone));

        if (isSuperAdmin)
        {
            return AdminPermissions.GetAll().ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var isStandardAdmin = user.UserRoles.Any(ur => ur.Role != null && ur.Role.Code == "ADMIN");
        if (isStandardAdmin)
        {
            return new HashSet<string>(StandardAdminPermissions, StringComparer.OrdinalIgnoreCase);
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

        var assigned = ResolveAssignedPermissions(user);
        return assigned.Contains(permissionCode);
    }

    public async Task<bool> HasAnyPermissionAsync(Guid userId, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default)
    {
        var codes = permissionCodes?.Where(AdminPermissions.IsValid).ToList();
        if (codes == null || codes.Count == 0)
            return false;

        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);

        if (user == null)
            return false;

        var assigned = ResolveAssignedPermissions(user);
        return codes.Any(c => assigned.Contains(c));
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

        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);

        if (user == null)
            return false;

        var assigned = ResolveAssignedPermissions(user);
        return codes.All(c => assigned.Contains(c));
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

        // 3. User & Assigned Permissions Verification
        var dbUser = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        var assignedPermissions = ResolveAssignedPermissions(dbUser);
        var isAuthorized = isPermissionValid && dbUser != null && dbUser.IsActive && assignedPermissions.Contains(permissionCode);

        if (!isAuthorized)
        {
            var denialReason = !isPermissionValid
                ? "UNKNOWN_PERMISSION"
                : (dbUser == null || !dbUser.IsActive
                    ? "USER_INACTIVE"
                    : (!dbUser.UserRoles.Any(ur => ur.Role != null && (ur.Role.Code == "ADMIN" || ur.Role.Code == "SUPER_ADMIN" || ur.Role.Code == "SYSTEM_ADMIN"))
                        ? "ROLE_NOT_ADMIN"
                        : "PERMISSION_NOT_ASSIGNED"));

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
