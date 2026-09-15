using Ethos.Api.Application.Auth;
using Ethos.Api.Contracts.Auth;
using Ethos.Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IOtpService _otpService;
    private readonly IAuthService _authService;

    public AuthController(
        IOtpService otpService,
        IAuthService authService)
    {
        _otpService = otpService;
        _authService = authService;
    }

    [HttpPost("request-otp")]
    public async Task<IActionResult> RequestOtp(
        [FromBody] RequestOtpRequest request,
        CancellationToken cancellationToken)
    {
        var purpose = ParsePurpose(request.Purpose);
        var result = await _otpService.RequestOtpAsync(
            request.Phone,
            purpose,
            cancellationToken);

        if (!result.Success)
        {
            if (result.Message != null && result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new
                {
                    userExists = false,
                    message = result.Message
                });
            }

            if (result.Message != null && result.Message.Contains("package", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    userExists = true,
                    hasPackage = false,
                    message = result.Message
                });
            }

            return BadRequest(new
            {
                userExists = true,
                message = result.Message
            });
        }

        return Ok(new
        {
            message = result.Message,
            developmentOtp = result.DevelopmentOtp
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _authService.LoginWithPasswordAsync(
            request.Phone,
            request.Password,
            cancellationToken);

        if (result is null)
        {
            return Unauthorized(new
            {
                message = "Invalid mobile number or password."
            });
        }

        if (!result.Success)
        {
            return BadRequest(new
            {
                message = result.Message
            });
        }

        return Ok(result);
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

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        CancellationToken cancellationToken)
    {
        var purpose = ParsePurpose(request.Purpose);
        var result = await _authService
            .VerifyOtpAndLoginAsync(
                request.Phone,
                request.Otp,
                purpose,
                cancellationToken);

        if (result is null)
        {
            return Unauthorized(new
            {
                message = "Invalid or expired OTP."
            });
        }

        return Ok(result);
    }

    private static OtpPurpose ParsePurpose(string? purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose))
            return OtpPurpose.Login;

        if (purpose.Equals("TRAINER_REGISTRATION", StringComparison.OrdinalIgnoreCase) ||
            purpose.Equals("TRAINERREGISTRATION", StringComparison.OrdinalIgnoreCase))
        {
            return OtpPurpose.TrainerRegistration;
        }

        if (purpose.Equals("STUDENT_LOGIN", StringComparison.OrdinalIgnoreCase) ||
            purpose.Equals("STUDENTLOGIN", StringComparison.OrdinalIgnoreCase))
        {
            return OtpPurpose.StudentLogin;
        }

        if (purpose.Equals("PHONE_VERIFICATION", StringComparison.OrdinalIgnoreCase))
        {
            return OtpPurpose.PhoneVerification;
        }

        return OtpPurpose.Login;
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
