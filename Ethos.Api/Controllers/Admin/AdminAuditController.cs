using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Audit & Security")]
public class AdminAuditController : ControllerBase
{
    private readonly IAdminAuditService _auditService;
    private readonly IAdminAuthorizationService _authService;

    public AdminAuditController(
        IAdminAuditService auditService,
        IAdminAuthorizationService authService)
    {
        _auditService = auditService;
        _authService = authService;
    }

    [HttpGet("audit-logs")]
    public async Task<ActionResult<PagedResult<AdminAuditLogResponse>>> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? category = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? entityId = null,
        [FromQuery] Guid? adminUserId = null,
        [FromQuery] string? traceId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AdminAuditView,
            "AUDIT_LOG",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _auditService.GetAuditLogsAsync(
            page,
            pageSize,
            category,
            action,
            entityType,
            entityId,
            adminUserId,
            traceId,
            startDate,
            endDate,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("audit-logs/{id:guid}")]
    public async Task<ActionResult<AdminAuditLogResponse>> GetAuditLogById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AdminAuditView,
            "AUDIT_LOG",
            id,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _auditService.GetAuditLogByIdAsync(id, cancellationToken);
        if (result == null) return NotFound(new { message = "Audit log not found." });

        return Ok(result);
    }

    [HttpGet("security-events")]
    public async Task<ActionResult<PagedResult<AdminSecurityEventResponse>>> GetSecurityEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? eventType = null,
        [FromQuery] string? severity = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? traceId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AdminSecurityView,
            "SECURITY_EVENT",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _auditService.GetSecurityEventsAsync(
            page,
            pageSize,
            eventType,
            severity,
            userId,
            traceId,
            startDate,
            endDate,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("security-events/{id:guid}")]
    public async Task<ActionResult<AdminSecurityEventResponse>> GetSecurityEventById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AdminSecurityView,
            "SECURITY_EVENT",
            id,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _auditService.GetSecurityEventByIdAsync(id, cancellationToken);
        if (result == null) return NotFound(new { message = "Security event not found." });

        return Ok(result);
    }
}
