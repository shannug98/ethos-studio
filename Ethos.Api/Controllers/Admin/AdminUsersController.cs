using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Users")]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _userService;
    private readonly IAdminAuthorizationService _authService;

    public AdminUsersController(
        IAdminUserService userService,
        IAdminAuthorizationService authService)
    {
        _userService = userService;
        _authService = authService;
    }

    private Guid AdminUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserListResponse>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] bool? status = null,
        [FromQuery] string? sortBy = null,
        CancellationToken cancellationToken = default)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.UserView, "User", null, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _userService.GetUsersAsync(page, pageSize, search, role, status, sortBy, cancellationToken);
        return Ok(result);
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<AdminUserDetailsResponse>> GetUserById(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.UserView, "User", userId, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _userService.GetUserByIdAsync(userId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPatch("users/{userId:guid}/status")]
    public async Task<IActionResult> UpdateUserStatus(
        Guid userId,
        [FromBody] AdminUpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.UserUpdateStatus, "User", userId, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        try
        {
            await _userService.UpdateUserStatusAsync(userId, AdminUserId, request.IsActive, request.Reason, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("users/{userId:guid}/audit-history")]
    public async Task<ActionResult<AdminUserAuditHistoryResponse>> GetUserAuditHistory(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.UserAuditView, "User", userId, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _userService.GetUserAuditHistoryAsync(userId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }
}
