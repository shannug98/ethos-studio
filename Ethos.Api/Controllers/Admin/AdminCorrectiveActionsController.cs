using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/corrective-actions")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Corrective Action Engine")]
public class AdminCorrectiveActionsController : ControllerBase
{
    private readonly IAdminCorrectiveActionService _actionService;
    private readonly IAdminAuthorizationService _authService;

    public AdminCorrectiveActionsController(
        IAdminCorrectiveActionService actionService,
        IAdminAuthorizationService authService)
    {
        _actionService = actionService;
        _authService = authService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CorrectiveActionResponse>>> GetActions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? actionType = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? incidentId = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.CorrectiveActionView, "CORRECTIVE_ACTION", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _actionService.GetActionsAsync(page, pageSize, actionType, status, incidentId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CorrectiveActionResponse>> GetActionById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.CorrectiveActionView, "CORRECTIVE_ACTION", id, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _actionService.GetActionByIdAsync(id, cancellationToken);
        if (result == null) return NotFound(new { message = "Corrective action record not found." });

        return Ok(result);
    }

    [HttpPost("simulate")]
    public async Task<ActionResult<CorrectiveActionDryRunResponse>> SimulateAction(
        [FromBody] ExecuteCorrectiveActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.CorrectiveActionView, "CORRECTIVE_ACTION", request.TargetEntityId, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        try
        {
            var traceId = HttpContext.GetTraceId();
            var result = await _actionService.SimulateActionAsync(request, auth.AdminUserId, traceId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("execute")]
    public async Task<ActionResult<CorrectiveActionResponse>> ExecuteAction(
        [FromBody] ExecuteCorrectiveActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.CorrectiveActionExecute, "CORRECTIVE_ACTION", request.TargetEntityId, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        try
        {
            var traceId = HttpContext.GetTraceId();
            var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return BadRequest(new { message = "Missing required 'Idempotency-Key' header. Every corrective action execution requires an Idempotency-Key." });
            }

            var result = await _actionService.ExecuteActionAsync(request, auth.AdminUserId, traceId, idempotencyKey, cancellationToken);
            return Ok(result);
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

    [HttpGet("metrics")]
    public async Task<ActionResult<CorrectiveActionMetricsResponse>> GetMetrics(
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.CorrectiveActionView, "CORRECTIVE_ACTION", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _actionService.GetMetricsAsync(cancellationToken);
        return Ok(result);
    }
}
