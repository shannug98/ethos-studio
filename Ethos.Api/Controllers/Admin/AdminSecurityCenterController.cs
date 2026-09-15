using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/security")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Security Center")]
public class AdminSecurityCenterController : ControllerBase
{
    private readonly IAdminSecurityCenterService _securityCenterService;
    private readonly IAdminAuthorizationService _authService;

    public AdminSecurityCenterController(
        IAdminSecurityCenterService securityCenterService,
        IAdminAuthorizationService authService)
    {
        _securityCenterService = securityCenterService;
        _authService = authService;
    }

    [HttpGet("fleet")]
    public async Task<ActionResult<SecurityFleetResponse>> GetFleet(
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.SecurityCenterView, "SECURITY_CENTER", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _securityCenterService.GetFleetSecurityAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("threats")]
    public async Task<ActionResult<SecurityThreatsSummaryResponse>> GetThreats(
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.SecurityCenterView, "SECURITY_CENTER", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _securityCenterService.GetThreatSummaryAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("investigate")]
    public async Task<ActionResult<SecurityInvestigationResponse>> Investigate(
        [FromQuery] string targetType = "IP",
        [FromQuery] string targetValue = "",
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.SecurityCenterView, "SECURITY_CENTER", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _securityCenterService.InvestigateEntityAsync(targetType, targetValue, cancellationToken);
        return Ok(result);
    }

    [HttpPost("sessions/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeSession(
        Guid id,
        [FromBody] RevokeSecurityTargetRequest? request,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.DeviceRevoke, "ADMIN_SESSION", id, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var reason = !string.IsNullOrWhiteSpace(request?.Reason) ? request.Reason.Trim() : "Administrative emergency termination via Security Center";
        var traceId = HttpContext.GetTraceId();

        var success = await _securityCenterService.RevokeSessionAsync(id, auth.AdminUserId, reason, traceId, cancellationToken);
        if (!success) return NotFound(new { message = "Session not found." });

        return Ok(new { message = "Session successfully terminated." });
    }

    [HttpPost("devices/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeDevice(
        Guid id,
        [FromBody] RevokeSecurityTargetRequest? request,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.DeviceRevoke, "ADMIN_DEVICE", id, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var reason = !string.IsNullOrWhiteSpace(request?.Reason) ? request.Reason.Trim() : "Administrative device revocation via Security Center";
        var traceId = HttpContext.GetTraceId();

        var success = await _securityCenterService.RevokeDeviceAsync(id, auth.AdminUserId, reason, traceId, cancellationToken);
        if (!success) return NotFound(new { message = "Device not found." });

        return Ok(new { message = "Device successfully revoked and sessions terminated." });
    }
}