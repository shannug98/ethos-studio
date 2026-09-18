using Ethos.Api.Application.Auth;
using Ethos.Api.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] PasswordChangeRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userIdClaim = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier);

        if (userIdClaim is null ||
            !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var success = await _authService.ChangePasswordAsync(
            userId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        if (!success)
        {
            return BadRequest(new
            {
                message = "Current password is incorrect or the new password is invalid."
            });
        }

        return Ok(new
        {
            message = "Password changed successfully. Please log in again."
        });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            userId = User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier
            )?.Value,

            name = User.Identity?.Name,

            phone = User.FindFirst(
                System.Security.Claims.ClaimTypes.MobilePhone
            )?.Value,

            roles = User.FindAll(
                System.Security.Claims.ClaimTypes.Role
            ).Select(x => x.Value).ToList()
        });
    }
}

