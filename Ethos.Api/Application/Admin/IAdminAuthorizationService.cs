using System.Security.Claims;

namespace Ethos.Api.Application.Admin;

public class AdminAuthorizationResult
{
    public bool Success { get; set; }
    public int StatusCode { get; set; } = 200;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid AdminUserId { get; set; }
    public string? AdminCustomerCode { get; set; }
    public Guid? AdminDeviceId { get; set; }
    public Guid? AdminSessionId { get; set; }
}

public interface IAdminAuthorizationService
{
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default);
    Task<bool> HasAnyPermissionAsync(Guid userId, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default);
    Task<bool> HasAllPermissionsAsync(Guid userId, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default);
    Task<AdminAuthorizationResult> AuthorizeActionAsync(
        ClaimsPrincipal user,
        string permissionCode,
        string? resourceType = null,
        Guid? resourceId = null,
        HttpContext? httpContext = null,
        CancellationToken cancellationToken = default);
}
