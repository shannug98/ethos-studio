using Ethos.Api.Application.Students;
using Ethos.Api.Contracts.Auth;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IOtpService _otpService;
    private readonly IJwtService _jwtService;
    private readonly IPasswordService _passwordService;
    private readonly IStudentEligibilityService _studentEligibilityService;

    public AuthService(
        AppDbContext db,
        IOtpService otpService,
        IJwtService jwtService,
        IPasswordService passwordService,
        IStudentEligibilityService studentEligibilityService)
    {
        _db = db;
        _otpService = otpService;
        _jwtService = jwtService;
        _passwordService = passwordService;
        _studentEligibilityService = studentEligibilityService;
    }

    public async Task<LoginResult?> LoginWithPasswordAsync(
        string phone,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = NormalizePhone(phone);

        if (string.IsNullOrWhiteSpace(normalizedPhone) ||
            string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(
                x => x.Phone == normalizedPhone,
                cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        // Password authentication is required for the new trainer flow.
        // Existing users without a password cannot use this endpoint yet.
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return null;
        }

        var passwordValid = _passwordService.VerifyPassword(
            password,
            user.PasswordHash);

        if (!passwordValid)
        {
            return null;
        }

        // Password is valid. Now initiate the OTP step.
        var otpResult = await _otpService.RequestOtpAsync(
            normalizedPhone,
            OtpPurpose.Login,
            cancellationToken);

        if (!otpResult.Success)
        {
            return new LoginResult
            {
                Success = false,
                Message = otpResult.Message,
                OtpRequired = false,
                MustChangePassword = user.MustChangePassword,
                Phone = normalizedPhone
            };
        }

        return new LoginResult
        {
            Success = true,
            Message = "Password verified. OTP sent successfully.",
            OtpRequired = true,
            MustChangePassword = user.MustChangePassword,
            Phone = normalizedPhone,
            DevelopmentOtp = otpResult.DevelopmentOtp
        };
    }

    public async Task<AuthResponse?> VerifyOtpAndLoginAsync(
        string phone,
        string otp,
        OtpPurpose purpose = OtpPurpose.Login,
        CancellationToken cancellationToken = default)
    {
        var verificationResult =
            await _otpService.VerifyOtpAsync(
                phone,
                otp,
                purpose,
                cancellationToken);

        if (!verificationResult.Success)
        {
            return null;
        }

        var normalizedPhone = NormalizePhone(phone);

        var user = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.Phone == normalizedPhone,
                cancellationToken);

        if (user is null)
        {
            // Only TrainerRegistration is allowed to auto-provision a new user record upon OTP verification!
            // For Login and StudentLogin, the account must already exist.
            if (purpose != OtpPurpose.TrainerRegistration)
            {
                return null;
            }

            user = new User
            {
                Id = Guid.NewGuid(),
                CustomerCode = GenerateCustomerCode(),
                FullName = "Ethos User",
                Phone = normalizedPhone,
                Email = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);

            var studentRole = await _db.Roles
                .FirstAsync(
                    x => x.Code == "STUDENT",
                    cancellationToken);

            var userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = studentRole.Id,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = null
            };

            _db.UserRoles.Add(userRole);
            user.UserRoles.Add(userRole);
        }

        if (!user.IsActive)
        {
            return null;
        }

        if (purpose == OtpPurpose.StudentLogin)
        {
            var isEligible = await _studentEligibilityService.IsStudentPortalEligibleAsync(
                user.Id,
                cancellationToken);

            if (!isEligible)
            {
                return null;
            }
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var roles = user.UserRoles
            .Where(x => x.Role != null)
            .Select(x => x.Role!.Code)
            .Distinct()
            .ToList();

        var token = _jwtService.GenerateToken(
            user,
            roles);

        return new AuthResponse
        {
            AccessToken = token.AccessToken,
            ExpiresAt = token.ExpiresAt,
            User = new UserInfoResponse
            {
                Id = user.Id,
                CustomerCode = user.CustomerCode,
                FullName = user.FullName,
                Phone = user.Phone,
                Email = user.Email,
                Roles = roles,
                MustChangePassword = user.MustChangePassword
            }
        };
    }

    public async Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) ||
            string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        if (currentPassword == newPassword)
        {
            return false;
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);

        if (user is null || !user.IsActive)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return false;
        }

        var currentPasswordValid = _passwordService.VerifyPassword(
            currentPassword,
            user.PasswordHash);

        if (!currentPasswordValid)
        {
            return false;
        }

        user.PasswordHash = _passwordService.HashPassword(newPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static string NormalizePhone(string phone)
    {
        return new string(
            phone
                .Trim()
                .Where(char.IsDigit)
                .ToArray());
    }

    private static string GenerateCustomerCode()
    {
        return $"ETH{Guid.NewGuid():N}"
            .Substring(0, 11)
            .ToUpperInvariant();
    }
}
