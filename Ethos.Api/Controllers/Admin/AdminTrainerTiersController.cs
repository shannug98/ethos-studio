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
[Tags("Admin - Tiers")]
public class AdminTrainerTiersController : ControllerBase
{
    private readonly IAdminTrainerTierService _tierService;
    private readonly IAdminTrainerUpgradeService _upgradeService;
    private readonly IAdminAuthorizationService _authService;

    public AdminTrainerTiersController(
        IAdminTrainerTierService tierService,
        IAdminTrainerUpgradeService upgradeService,
        IAdminAuthorizationService authService)
    {
        _tierService = tierService;
        _upgradeService = upgradeService;
        _authService = authService;
    }

    private Guid AdminUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("trainer-tiers")]
    public async Task<ActionResult<IReadOnlyList<TrainerTierResponse>>> GetAllTiers(CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "TrainerTier", null, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _tierService.GetAllTiersAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("trainers/{trainerId:guid}/tier")]
    public async Task<ActionResult<TrainerTierResponse>> GetTrainerTier(
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "TrainerProfile", trainerId, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _tierService.GetTrainerTierByTrainerIdAsync(trainerId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("trainers/{trainerId:guid}/tier-history")]
    public async Task<ActionResult<IReadOnlyList<TrainerTierHistoryResponse>>> GetTrainerTierHistory(
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "TrainerProfile", trainerId, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _tierService.GetTrainerTierHistoryByTrainerIdAsync(trainerId, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("trainer-tiers/{id:guid}/permissions")]
    public async Task<IActionResult> UpdateTierPermissions(
        Guid id,
        [FromBody] AdminUpdateTierPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerApprove, "TrainerTier", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        try
        {
            await _tierService.UpdateTierPermissionsAsync(id, AdminUserId, request, cancellationToken);
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

    [HttpGet("trainer-upgrades")]
    public async Task<ActionResult<PagedResult<TrainerUpgradeRequestResponse>>> GetUpgradeRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] TrainerUpgradeRequestStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "TrainerUpgradeRequest", null, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _upgradeService.GetUpgradeRequestsAsync(page, pageSize, status, cancellationToken);
        return Ok(result);
    }

    [HttpGet("trainer-upgrades/{upgradeId:guid}")]
    public async Task<ActionResult<TrainerUpgradeRequestResponse>> GetUpgradeRequestById(
        Guid upgradeId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "TrainerUpgradeRequest", upgradeId, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _upgradeService.GetUpgradeRequestByIdAsync(upgradeId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("trainer-upgrades/{id:guid}/approve")]
    public async Task<IActionResult> ApproveUpgradeRequest(
        Guid id,
        [FromBody] AdminApproveUpgradeRequestBody? body,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerApprove, "TrainerUpgradeRequest", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        try
        {
            await _upgradeService.ApproveUpgradeRequestAsync(id, AdminUserId, body?.Notes, cancellationToken);
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

    [HttpPost("trainer-upgrades/{id:guid}/reject")]
    public async Task<IActionResult> RejectUpgradeRequest(
        Guid id,
        [FromBody] AdminRejectUpgradeRequestBody body,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerReject, "TrainerUpgradeRequest", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        if (string.IsNullOrWhiteSpace(body?.Reason))
        {
            return BadRequest(new { message = "A reason is mandatory for rejecting a tier upgrade request." });
        }

        try
        {
            await _upgradeService.RejectUpgradeRequestAsync(id, AdminUserId, body.Reason, cancellationToken);
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
}
