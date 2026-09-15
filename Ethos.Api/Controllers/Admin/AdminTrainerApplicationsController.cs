using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Applications")]
public class AdminTrainerApplicationsController : ControllerBase
{
    private readonly IAdminTrainerApplicationService _applicationService;
    private readonly IAdminAuthorizationService _authService;

    public AdminTrainerApplicationsController(
        IAdminTrainerApplicationService applicationService,
        IAdminAuthorizationService authService)
    {
        _applicationService = applicationService;
        _authService = authService;
    }

    private Guid AdminUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("trainer-applications")]
    public async Task<ActionResult<PagedResult<TrainerApplicationResponse>>> GetApplications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] TrainerApplicationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "TrainerApplication", null, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _applicationService.GetApplicationsAsync(page, pageSize, status, cancellationToken);
        return Ok(result);
    }

    [HttpGet("trainer-applications/{id:guid}")]
    public async Task<ActionResult<TrainerApplicationResponse>> GetApplicationById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "TrainerApplication", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _applicationService.GetApplicationByIdAsync(id, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("trainer-applications/{id:guid}/approve")]
    public async Task<IActionResult> ApproveApplication(
        Guid id,
        [FromBody] AdminApproveApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerApprove, "TrainerApplication", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        try
        {
            await _applicationService.ApproveApplicationAsync(id, AdminUserId, request, cancellationToken);
            return NoContent();
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

    [HttpPost("trainer-applications/{id:guid}/reject")]
    public async Task<IActionResult> RejectApplication(
        Guid id,
        [FromBody] AdminRejectApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerReject, "TrainerApplication", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        try
        {
            await _applicationService.RejectApplicationAsync(id, AdminUserId, request, cancellationToken);
            return NoContent();
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

    [HttpPost("trainer-applications/{id:guid}/request-changes")]
    public async Task<IActionResult> RequestChanges(
        Guid id,
        [FromBody] AdminRequestChangesRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerReject, "TrainerApplication", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        try
        {
            await _applicationService.RequestChangesAsync(id, AdminUserId, request, cancellationToken);
            return NoContent();
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

    [HttpGet("trainer-applications/{id:guid}/video/stream")]
    public async Task<IActionResult> StreamApplicationVideo(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "TrainerApplication", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var streamInfo = await _applicationService.GetApplicationVideoStreamAsync(id, cancellationToken);
        if (streamInfo == null)
        {
            return NotFound(new { message = "No audition video found for this application." });
        }

        return PhysicalFile(streamInfo.Value.PhysicalPath, streamInfo.Value.ContentType, enableRangeProcessing: true);
    }
}

