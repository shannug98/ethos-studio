using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/incidents")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Incident Center")]
public class AdminIncidentsController : ControllerBase
{
    private readonly IAdminIncidentService _incidentService;
    private readonly IAdminAuthorizationService _authService;

    public AdminIncidentsController(
        IAdminIncidentService incidentService,
        IAdminAuthorizationService authService)
    {
        _incidentService = incidentService;
        _authService = authService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<IncidentResponse>>> GetIncidents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? service = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.IncidentView, "INCIDENT", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _incidentService.GetIncidentsAsync(page, pageSize, status, severity, service, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IncidentResponse>> GetIncidentById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.IncidentView, "INCIDENT", id, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _incidentService.GetIncidentByIdAsync(id, cancellationToken);
        if (result == null) return NotFound(new { message = "Incident not found." });

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<IncidentResponse>> CreateIncident(
        [FromBody] CreateIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.IncidentManage, "INCIDENT", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        try
        {
            var traceId = HttpContext.GetTraceId();
            var result = await _incidentService.CreateIncidentAsync(request, auth.AdminUserId, traceId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("from-trace")]
    public async Task<ActionResult<IncidentResponse>> CreateFromTrace(
        [FromBody] CreateIncidentFromTraceRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.IncidentManage, "INCIDENT", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        try
        {
            var traceId = HttpContext.GetTraceId();
            var result = await _incidentService.CreateIncidentFromTraceAsync(request, auth.AdminUserId, traceId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("from-security-event")]
    public async Task<ActionResult<IncidentResponse>> CreateFromSecurityEvent(
        [FromBody] CreateIncidentFromSecurityEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.IncidentManage, "INCIDENT", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        try
        {
            var traceId = HttpContext.GetTraceId();
            var result = await _incidentService.CreateIncidentFromSecurityEventAsync(request, auth.AdminUserId, traceId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<IncidentResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdateIncidentStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.IncidentManage, "INCIDENT", id, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        try
        {
            var traceId = HttpContext.GetTraceId();
            var result = await _incidentService.UpdateIncidentStatusAsync(id, request, auth.AdminUserId, traceId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Incident not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/updates")]
    public async Task<ActionResult<IncidentUpdateDto>> AddUpdate(
        Guid id,
        [FromBody] AddIncidentUpdateDto request,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.IncidentManage, "INCIDENT", id, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        try
        {
            var result = await _incidentService.AddIncidentUpdateAsync(id, request, auth.AdminUserId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Incident not found." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}/assign")]
    public async Task<ActionResult<IncidentResponse>> AssignIncident(
        Guid id,
        [FromBody] AssignIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.IncidentManage, "INCIDENT", id, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        try
        {
            var traceId = HttpContext.GetTraceId();
            var result = await _incidentService.AssignIncidentAsync(id, request, auth.AdminUserId, traceId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Incident not found." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<IncidentMetricsResponse>> GetMetrics(
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.IncidentView, "INCIDENT", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _incidentService.GetIncidentMetricsAsync(cancellationToken);
        return Ok(result);
    }
}