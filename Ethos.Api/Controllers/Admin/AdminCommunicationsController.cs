using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/communications")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Communications & MSG91 Engine")]
public class AdminCommunicationsController : ControllerBase
{
    private readonly IAdminCommunicationsService _commService;
    private readonly IAdminAuthorizationService _authService;

    public AdminCommunicationsController(
        IAdminCommunicationsService commService,
        IAdminAuthorizationService authService)
    {
        _commService = commService;
        _authService = authService;
    }

    [HttpGet("logs")]
    public async Task<ActionResult<PagedResult<CommunicationLogResponse>>> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? channel = null,
        [FromQuery] string? status = null,
        [FromQuery] string? templateId = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.CommunicationsView, "COMMUNICATIONS", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _commService.GetLogsAsync(page, pageSize, channel, status, templateId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("logs/{id:guid}")]
    public async Task<ActionResult<CommunicationLogResponse>> GetLogById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.CommunicationsView, "COMMUNICATIONS", id, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _commService.GetLogByIdAsync(id, cancellationToken);
        if (result == null) return NotFound(new { message = "Communication log record not found." });

        return Ok(result);
    }

    [HttpPost("send")]
    public async Task<ActionResult<CommunicationLogResponse>> SendMessage(
        [FromBody] SendCommunicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.CommunicationsManage, "COMMUNICATIONS", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        try
        {
            var traceId = HttpContext.GetTraceId();
            var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return BadRequest(new { message = "Missing required 'Idempotency-Key' header. Every communication dispatch requires an Idempotency-Key." });
            }

            var result = await _commService.SendMessageAsync(request, auth.AdminUserId, traceId, idempotencyKey, cancellationToken);
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

    [HttpPost("retry")]
    public async Task<ActionResult<CommunicationLogResponse>> RetryMessageFromBody(
        [FromBody] RetryCommunicationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.CommunicationId.HasValue || request.CommunicationId.Value == Guid.Empty)
        {
            return BadRequest(new { message = "CommunicationId is required in request body." });
        }

        return await RetryMessage(request.CommunicationId.Value, request, cancellationToken);
    }

    [HttpPost("logs/{id:guid}/retry")]
    public async Task<ActionResult<CommunicationLogResponse>> RetryMessage(
        Guid id,
        [FromBody] RetryCommunicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.CommunicationsManage, "COMMUNICATIONS", id, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        try
        {
            var traceId = HttpContext.GetTraceId();
            var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return BadRequest(new { message = "Missing required 'Idempotency-Key' header. Every communication retry requires an Idempotency-Key." });
            }

            var result = await _commService.RetryMessageAsync(id, request, auth.AdminUserId, traceId, idempotencyKey, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Communication log record not found." });
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

    [HttpGet("templates")]
    public async Task<ActionResult<List<CommunicationTemplateDto>>> GetTemplates(
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.CommunicationsView, "COMMUNICATIONS", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _commService.GetTemplatesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<CommunicationsMetricsResponse>> GetMetrics(
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.CommunicationsView, "COMMUNICATIONS", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _commService.GetMetricsAsync(cancellationToken);
        return Ok(result);
    }
}
