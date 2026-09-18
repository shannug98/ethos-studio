using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/auth")]
[Tags("Admin - Authentication")]
public class AdminAuthController : ControllerBase
{
    private readonly IAdminAuthService _adminAuthService;
    private readonly IAdminDeviceService _adminDeviceService;

    public AdminAuthController(
        IAdminAuthService adminAuthService,
        IAdminDeviceService adminDeviceService)
    {
        _adminAuthService = adminAuthService;
        _adminDeviceService = adminDeviceService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] AdminLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        // Check for device credential from body, header, or cookie
        var deviceCredential = request.DeviceCredential;
        if (string.IsNullOrWhiteSpace(deviceCredential) && Request.Headers.TryGetValue("X-Admin-Device-Credential", out var headerCred))
        {
            deviceCredential = headerCred.ToString();
        }
        if (string.IsNullOrWhiteSpace(deviceCredential) && Request.Cookies.TryGetValue("ethos_admin_device", out var cookieCred))
        {
            deviceCredential = cookieCred;
        }

        var result = await _adminAuthService.LoginAsync(
            request.Phone,
            request.Password,
            deviceCredential,
            request.DeviceName,
            ipAddress,
            userAgent,
            cancellationToken);

        if (!result.Success)
        {
            if (result.StatusCode == StatusCodes.Status403Forbidden)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new AdminDeviceErrorResponse
                {
                    Code = result.ErrorCode ?? "DEVICE_AUTHORIZATION_BLOCKED",
                    Message = result.Message,
                    ActiveSessions = result.ActiveSessions
                });
            }

            return Unauthorized(new
            {
                message = result.Message
            });
        }

        // Set HttpOnly, Secure, SameSite=Lax cookies for browser session
        if (!string.IsNullOrWhiteSpace(result.RawDeviceCredential))
        {
            Response.Cookies.Append("ethos_admin_device", result.RawDeviceCredential, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/api/admin",
                Expires = DateTimeOffset.UtcNow.AddDays(365)
            });
        }

        if (!string.IsNullOrWhiteSpace(result.RawSessionToken))
        {
            Response.Cookies.Append("ethos_admin_session", result.RawSessionToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/api/admin",
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
        }

        return Ok(result.AuthResponse);
    }

    [HttpPost("terminate-session")]
    public async Task<IActionResult> TerminateSession(
        [FromBody] AdminTerminateSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var isValid = await _adminAuthService.ValidateCredentialsAsync(request.Phone, request.Password, cancellationToken);
        if (!isValid)
        {
            return Unauthorized(new { message = "Invalid administrative credentials." });
        }

        var success = await _adminDeviceService.LogoutSessionAsync(request.SessionId, cancellationToken);
        if (!success)
        {
            return NotFound(new { message = "Session not found or already terminated." });
        }

        return Ok(new { success = true, message = "Session terminated successfully." });
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var sessionIdClaim = User.FindFirst("session_id")?.Value;
        if (Guid.TryParse(sessionIdClaim, out var sessionId))
        {
            await _adminDeviceService.LogoutSessionAsync(sessionId, cancellationToken);
        }

        Response.Cookies.Delete("ethos_admin_session", new CookieOptions
        {
            Path = "/api/admin"
        });

        return Ok(new { message = "Logged out successfully." });
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Guid? userId = Guid.TryParse(userIdClaim, out var id) ? id : null;

        var count = await _adminDeviceService.LogoutAllSessionsAsync(userId, cancellationToken);

        Response.Cookies.Delete("ethos_admin_session", new CookieOptions
        {
            Path = "/api/admin"
        });

        return Ok(new { message = $"Terminated {count} admin sessions." });
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var name = User.Identity?.Name;
        var phone = User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value;
        var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(x => x.Value).ToList();
        var deviceId = User.FindFirst("device_id")?.Value;
        var sessionId = User.FindFirst("session_id")?.Value;

        return Ok(new
        {
            userId,
            name,
            phone,
            roles,
            deviceId,
            sessionId
        });
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(CancellationToken cancellationToken)
    {
        var sessionIdClaim = User.FindFirst("session_id")?.Value;
        if (!Guid.TryParse(sessionIdClaim, out var sessionId))
        {
            return Unauthorized(new { message = "Invalid session identity." });
        }

        var success = await _adminDeviceService.HeartbeatSessionAsync(sessionId, cancellationToken);
        if (!success)
        {
            return Unauthorized(new { code = "SESSION_EXPIRED", message = "Session is inactive or revoked." });
        }

        return Ok(new { status = "active", timestamp = DateTime.UtcNow });
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] AdminChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var adminUserId))
        {
            return Unauthorized(new { message = "Invalid administrative authentication identity." });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _adminAuthService.ChangePasswordWithCurrentAsync(
            adminUserId,
            request.CurrentPassword,
            request.NewPassword,
            ipAddress,
            userAgent,
            cancellationToken);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.Message });
        }

        return Ok(new { success = true, message = result.Message });
    }

    [AllowAnonymous]
    [HttpPost("forgot-password/request")]
    public async Task<IActionResult> RequestPasswordReset(
        [FromBody] AdminForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _adminAuthService.RequestPasswordResetAsync(
            request.Phone,
            ipAddress,
            userAgent,
            cancellationToken);

        return Ok(new { success = true, message = result.Message });
    }

    [AllowAnonymous]
    [HttpPost("forgot-password/reset")]
    public async Task<IActionResult> ResetPasswordWithToken(
        [FromBody] AdminResetPasswordWithTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _adminAuthService.ResetPasswordWithTokenAsync(
            request.Token,
            request.NewPassword,
            ipAddress,
            userAgent,
            cancellationToken);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.Message });
        }

        return Ok(new { success = true, message = result.Message });
    }
}

