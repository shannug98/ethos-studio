using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/observability")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Observability & Telemetry")]
public class AdminObservabilityController : ControllerBase
{
    private readonly IAdminObservabilityService _observabilityService;
    private readonly IAdminAuthorizationService _authService;

    public AdminObservabilityController(
        IAdminObservabilityService observabilityService,
        IAdminAuthorizationService authService)
    {
        _observabilityService = observabilityService;
        _authService = authService;
    }

    [HttpGet("logs")]
    public async Task<ActionResult<PagedResult<ApiRequestLogResponse>>> GetRequestLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? statusCode = null,
        [FromQuery] string? method = null,
        [FromQuery] string? path = null,
        [FromQuery] string? traceId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] long? minDurationMs = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.ObservabilityView, "OBSERVABILITY", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _observabilityService.GetRequestLogsAsync(page, pageSize, statusCode, method, path, traceId, startDate, endDate, minDurationMs, cancellationToken);
        return Ok(result);
    }

    [HttpGet("traces/{traceId}")]
    public async Task<ActionResult<TraceDeepDiveResponse>> GetTraceDeepDive(
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.ObservabilityView, "OBSERVABILITY", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _observabilityService.GetTraceDeepDiveAsync(traceId, cancellationToken);
        if (result == null) return NotFound(new { message = $"Trace {traceId} not found." });

        return Ok(result);
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<ObservabilityMetricsResponse>> GetMetrics(
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.ObservabilityView, "OBSERVABILITY", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _observabilityService.GetObservabilityMetricsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("health")]
    public async Task<ActionResult<DeepHealthCheckResponse>> GetDeepHealthCheck(
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.ObservabilityView, "OBSERVABILITY", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _observabilityService.GetDeepHealthCheckAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("users/{userId:guid}/timeline")]
    public async Task<ActionResult<UserTechnicalTimelineResponse>> GetUserTimeline(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.ObservabilityView, "OBSERVABILITY", userId, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _observabilityService.GetUserTechnicalTimelineAsync(userId, cancellationToken);
        return Ok(result);
    }
}