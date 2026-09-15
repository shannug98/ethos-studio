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
            ipAddress,
            userAgent,
            cancellationToken);

        if (result == null)
        {
            return Unauthorized(new
            {
                message = "Invalid administrative credentials."
            });
        }

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

            return BadRequest(new
            {
                message = result.Message
            });
        }

        return Ok(result);
    }

    [HttpPost("verify-mfa")]
    public async Task<IActionResult> VerifyMfa(
        [FromBody] AdminVerifyMfaRequest request,
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

        var result = await _adminAuthService.VerifyMfaAsync(
            request.Phone,
            request.Otp,
            deviceCredential,
            request.DeviceName,
            request.FingerprintTelemetry,
            ipAddress,
            userAgent,
            cancellationToken);

        if (!result.Success)
        {
            if (result.StatusCode == StatusCodes.Status403Forbidden)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new AdminDeviceErrorResponse
                {
                    Code = result.ErrorCode ?? "DEVICE_NOT_AUTHORIZED",
                    Message = result.ErrorMessage ?? "Device authorization failed.",
                    ActiveSessions = result.ActiveSessions
                });
            }

            return Unauthorized(new
            {
                code = result.ErrorCode ?? "INVALID_MFA",
                message = result.ErrorMessage ?? "Invalid or expired MFA verification code."
            });
        }

        // Set HttpOnly, Secure, SameSite=Lax cookies
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
    [HttpPost("change-password/request-otp")]
    public async Task<IActionResult> RequestChangePasswordOtp(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var adminUserId))
        {
            return Unauthorized(new { message = "Invalid administrative authentication identity." });
        }

        var result = await _adminAuthService.RequestChangePasswordOtpAsync(adminUserId, cancellationToken);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.Message });
        }

        return Ok(new AdminRequestOtpResponse
        {
            Success = true,
            Message = result.Message,
            DevelopmentOtp = result.DevelopmentOtp
        });
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

        var result = await _adminAuthService.ChangePasswordAsync(
            adminUserId,
            request.NewPassword,
            request.Otp,
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
    [HttpPost("forgot-password/request-otp")]
    public async Task<IActionResult> RequestForgotPasswordOtp(
        [FromBody] AdminForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _adminAuthService.RequestForgotPasswordOtpAsync(request.Phone, cancellationToken);

        return Ok(new AdminRequestOtpResponse
        {
            Success = true,
            Message = result.Message,
            DevelopmentOtp = result.DevelopmentOtp
        });
    }

    [AllowAnonymous]
    [HttpPost("forgot-password/reset")]
    public async Task<IActionResult> ResetForgotPassword(
        [FromBody] AdminResetForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _adminAuthService.ResetForgotPasswordAsync(
            request.Phone,
            request.Otp,
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
