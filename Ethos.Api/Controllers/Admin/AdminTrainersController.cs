using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Trainers")]
public class AdminTrainersController : ControllerBase
{
    private readonly IAdminTrainerService _trainerService;
    private readonly IAdminAuthorizationService _authService;

    public AdminTrainersController(
        IAdminTrainerService trainerService,
        IAdminAuthorizationService authService)
    {
        _trainerService = trainerService;
        _authService = authService;
    }

    private Guid AdminUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("trainers")]
    public async Task<ActionResult<PagedResult<AdminTrainerListResponse>>> GetTrainers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? tierId = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "Trainer", null, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _trainerService.GetTrainersAsync(page, pageSize, search, tierId, status, cancellationToken);
        return Ok(result);
    }

    [HttpPost("trainers")]
    public async Task<ActionResult<AdminTrainerListResponse>> CreateTrainer(
        [FromBody] AdminCreateTrainerRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerApprove, "Trainer", null, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        try
        {
            var result = await _trainerService.CreateTrainerAsync(request, AdminUserId, cancellationToken);
            return Created($"/api/admin/trainers/{result.TrainerId}", result);
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

    [HttpGet("trainers/stats")]
    public async Task<ActionResult<AdminTrainerSummaryStatsResponse>> GetTrainerStats(
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "Trainer", null, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _trainerService.GetTrainerStatsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("trainers/{id:guid}")]
    public async Task<ActionResult<AdminTrainerDetailsResponse>> GetTrainerById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "Trainer", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _trainerService.GetTrainerDetailsByIdAsync(id, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("trainers/{id:guid}/diagnostics")]
    public async Task<ActionResult<TrainerDiagnosticReport>> GetTrainerDiagnostics(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "Trainer", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var traceId = HttpContext.GetTraceId();
        var report = await _trainerService.GetTrainerDiagnosticsAsync(id, traceId, cancellationToken);
        if (report == null) return NotFound();
        return Ok(report);
    }

    [HttpPatch("trainers/{id:guid}/tier")]
    public async Task<IActionResult> UpdateTrainerTier(
        Guid id,
        [FromBody] AdminUpdateTrainerTierRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerApprove, "Trainer", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        try
        {
            await _trainerService.UpdateTrainerTierAsync(id, AdminUserId, request, cancellationToken);
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

    [HttpPatch("trainers/{id:guid}/status")]
    public async Task<IActionResult> UpdateTrainerStatus(
        Guid id,
        [FromBody] AdminUpdateTrainerStatusRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerApprove, "Trainer", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        try
        {
            await _trainerService.UpdateTrainerStatusAsync(id, AdminUserId, request.Status, request.Reason, cancellationToken);
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

    [HttpPut("trainers/{id:guid}")]
    public async Task<ActionResult<AdminTrainerListResponse>> UpdateTrainer(
        Guid id,
        [FromBody] AdminUpdateTrainerRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerApprove, "Trainer", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        try
        {
            var result = await _trainerService.UpdateTrainerAsync(id, request, AdminUserId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("trainers/{id:guid}")]
    public async Task<ActionResult<AdminDeleteTrainerResult>> DeleteTrainer(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerApprove, "Trainer", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        try
        {
            var result = await _trainerService.DeleteOrArchiveTrainerAsync(id, AdminUserId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
