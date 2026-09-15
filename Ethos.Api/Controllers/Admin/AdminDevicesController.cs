using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Devices & Sessions")]
public class AdminDevicesController : ControllerBase
{
    private readonly IAdminDeviceService _adminDeviceService;
    private readonly IAdminAuthorizationService _authService;
    private readonly ILogger<AdminDevicesController> _logger;

    public AdminDevicesController(
        IAdminDeviceService adminDeviceService,
        IAdminAuthorizationService authService,
        ILogger<AdminDevicesController> logger)
    {
        _adminDeviceService = adminDeviceService;
        _authService = authService;
        _logger = logger;
    }

    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices(CancellationToken cancellationToken)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.DeviceView, "AdminDevice", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var devices = await _adminDeviceService.GetDevicesAsync(cancellationToken);
        return Ok(devices);
    }

    [HttpPost("devices/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeDevice(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.DeviceRevoke, "AdminDevice", id, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var success = await _adminDeviceService.RevokeDeviceAsync(
            id,
            userId,
            ipAddress,
            userAgent,
            cancellationToken);

        if (!success)
        {
            return NotFound(new { message = "Device not found." });
        }

        return Ok(new { message = "Device authorization revoked successfully." });
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.DeviceView, "AdminDevice", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? userId = Guid.TryParse(userIdString, out var uid) ? uid : null;
        var sessionIdString = User.FindFirst("session_id")?.Value;
        Guid? sessionId = Guid.TryParse(sessionIdString, out var sid) ? sid : null;

        var sessions = await _adminDeviceService.GetActiveSessionsAsync(userId, sessionId, cancellationToken);
        return Ok(sessions);
    }

    [HttpPost("sessions/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeSession(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.DeviceRevoke, "AdminDevice", id, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var success = await _adminDeviceService.RevokeSessionAsync(
            id,
            userId,
            ipAddress,
            userAgent,
            cancellationToken);

        if (!success)
        {
            return NotFound(new { message = "Session not found." });
        }

        return Ok(new { message = "Session revoked successfully." });
    }

    [HttpPost("sessions/heartbeat")]
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
}
