using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Permissions")]
public class AdminPermissionsController : ControllerBase
{
    private readonly IAdminTrainerPermissionService _permissionService;
    private readonly IAdminAuthorizationService _authService;

    public AdminPermissionsController(
        IAdminTrainerPermissionService permissionService,
        IAdminAuthorizationService authService)
    {
        _permissionService = permissionService;
        _authService = authService;
    }

    private Guid AdminUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("permissions")]
    public async Task<ActionResult<IReadOnlyList<TrainerPermissionResponse>>> GetAllPermissions(CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "Permission", null, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _permissionService.GetAllPermissionsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("trainers/{trainerId:guid}/permissions")]
    public async Task<ActionResult<IReadOnlyList<AdminTrainerPermissionDetailResponse>>> GetTrainerPermissions(
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "TrainerProfile", trainerId, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _permissionService.GetTrainerPermissionsAsync(trainerId, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("trainers/{id:guid}/permissions")]
    public async Task<IActionResult> SetPermissionOverride(
        Guid id,
        [FromBody] AdminPermissionOverrideRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerApprove, "TrainerProfile", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        if (string.IsNullOrWhiteSpace(request?.Reason))
        {
            return BadRequest(new { message = "A reason is mandatory for overriding trainer permissions." });
        }

        try
        {
            var updated = await _permissionService.SetTrainerPermissionOverrideAsync(id, AdminUserId, request, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("trainers/{id:guid}/permissions/{permissionCode}")]
    public async Task<IActionResult> SetPermissionOverrideByCode(
        Guid id,
        string permissionCode,
        [FromBody] AdminPermissionOverrideBody body,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerApprove, "TrainerProfile", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var request = new AdminPermissionOverrideRequest
        {
            PermissionCode = permissionCode,
            IsAllowed = body?.IsAllowed,
            ClearOverride = body?.ClearOverride ?? false,
            Reason = body?.Reason ?? string.Empty
        };

        try
        {
            var updated = await _permissionService.SetTrainerPermissionOverrideAsync(id, AdminUserId, request, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("trainers/{id:guid}/permissions/{permissionCode}")]
    public async Task<IActionResult> ClearPermissionOverrideByCode(
        Guid id,
        string permissionCode,
        [FromQuery] string? reason,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerApprove, "TrainerProfile", id, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        if (string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest(new { message = "A reason is mandatory for resetting trainer permissions." });
        }

        try
        {
            var updated = await _permissionService.ClearTrainerPermissionOverrideAsync(id, AdminUserId, permissionCode, reason.Trim(), cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public class AdminPermissionOverrideBody
{
    public bool? IsAllowed { get; set; }
    public bool ClearOverride { get; set; }
    public string? Reason { get; set; }
}
