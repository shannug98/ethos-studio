using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ethos.Api.Application.Auth;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminAuthService : IAdminAuthService
{
    private static readonly HashSet<string> AuthorizedAdminPhones = new(StringComparer.OrdinalIgnoreCase)
    {
        "8019013757",
        "8341701113"
    };

    private readonly AppDbContext _db;
    private readonly IOtpService _otpService;
    private readonly IJwtService _jwtService;
    private readonly IPasswordService _passwordService;
    private readonly IAdminDeviceService _adminDeviceService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminAuthService> _logger;

    public AdminAuthService(
        AppDbContext db,
        IOtpService otpService,
        IJwtService jwtService,
        IPasswordService passwordService,
        IAdminDeviceService adminDeviceService,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<AdminAuthService> logger)
    {
        _db = db;
        _otpService = otpService;
        _jwtService = jwtService;
        _passwordService = passwordService;
        _adminDeviceService = adminDeviceService;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> ValidateCredentialsAsync(
        string phone,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = NormalizePhone(phone);
        if (string.IsNullOrWhiteSpace(normalizedPhone) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        if (!AuthorizedAdminPhones.Contains(normalizedPhone))
        {
            return false;
        }

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Phone == normalizedPhone, cancellationToken);

        if (user == null || !user.IsActive)
        {
            return false;
        }

        if (!user.UserRoles.Any(ur => ur.Role?.Code == "ADMIN"))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return false;
        }

        return _passwordService.VerifyPassword(password, user.PasswordHash);
    }

    public async Task<AdminLoginResult?> LoginAsync(
        string phone,
        string password,
        string? deviceCredential,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = NormalizePhone(phone);

        if (string.IsNullOrWhiteSpace(normalizedPhone) || string.IsNullOrWhiteSpace(password))
        {
            await RecordSecurityEventAsync(
                "ADMIN_LOGIN_REJECTED",
                "WARNING",
                ipAddress,
                userAgent,
                null,
                MaskPhone(phone),
                new { reason = "EMPTY_CREDENTIALS" },
                cancellationToken);
            return null;
        }

        // 1. Strict Whitelist Check
        if (!AuthorizedAdminPhones.Contains(normalizedPhone))
        {
            _logger.LogWarning("Admin login rejected: unauthorized phone number from IP {IpAddress}", ipAddress);
            await RecordSecurityEventAsync(
                "ADMIN_LOGIN_REJECTED",
                "WARNING",
                ipAddress,
                userAgent,
                null,
                MaskPhone(normalizedPhone),
                new { reason = "INVALID_ADMIN_IDENTITY" },
                cancellationToken);
            return null;
        }

        // 2. User Lookup & Status Check
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Phone == normalizedPhone, cancellationToken);

        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Admin login failed: account missing or inactive for phone from IP {IpAddress}", ipAddress);
            await RecordSecurityEventAsync(
                "ADMIN_ACCOUNT_INACTIVE",
                "WARNING",
                ipAddress,
                userAgent,
                user?.Id,
                MaskPhone(normalizedPhone),
                new { reason = "ACCOUNT_NOT_FOUND_OR_INACTIVE" },
                cancellationToken);
            return null;
        }

        var hasAdminRole = user.UserRoles.Any(ur => ur.Role?.Code == "ADMIN");
        if (!hasAdminRole)
        {
            _logger.LogWarning("Admin login failed: user {UserId} lacks ADMIN role", user.Id);
            await RecordSecurityEventAsync(
                "ADMIN_LOGIN_REJECTED",
                "WARNING",
                ipAddress,
                userAgent,
                user.Id,
                MaskPhone(normalizedPhone),
                new { reason = "USER_LACKS_ADMIN_ROLE" },
                cancellationToken);
            return null;
        }

        // 3. Password Verification
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            _logger.LogWarning("Admin login failed: user {UserId} has no password hash set", user.Id);
            await RecordSecurityEventAsync(
                "ADMIN_LOGIN_PASSWORD_FAILED",
                "WARNING",
                ipAddress,
                userAgent,
                user.Id,
                MaskPhone(normalizedPhone),
                new { reason = "NO_PASSWORD_SET" },
                cancellationToken);
            return null;
        }

        var isPasswordValid = _passwordService.VerifyPassword(password, user.PasswordHash);
        if (!isPasswordValid)
        {
            _logger.LogWarning("Admin login failed: invalid password for user {UserId} from IP {IpAddress}", user.Id, ipAddress);
            await RecordSecurityEventAsync(
                "ADMIN_LOGIN_PASSWORD_FAILED",
                "WARNING",
                ipAddress,
                userAgent,
                user.Id,
                MaskPhone(normalizedPhone),
                new { reason = "PASSWORD_MISMATCH", customerCode = user.CustomerCode },
                cancellationToken);
            return null;
        }

        // 3b. Pre-MFA Device Slot Enforcement Check
        var deviceCheck = await _adminDeviceService.CanAttemptLoginAsync(
            user.Id,
            deviceCredential,
            cancellationToken);

        if (!deviceCheck.Success)
        {
            _logger.LogWarning("Admin login pre-MFA check failed for user {UserId}: {ErrorCode} - {ErrorMessage}", user.Id, deviceCheck.ErrorCode, deviceCheck.ErrorMessage);

            await RecordSecurityEventAsync(
                "ADMIN_LOGIN_DEVICE_BLOCKED",
                "WARNING",
                ipAddress,
                userAgent,
                user.Id,
                MaskPhone(normalizedPhone),
                new { reason = deviceCheck.ErrorCode, message = deviceCheck.ErrorMessage },
                cancellationToken);

            return new AdminLoginResult
            {
                Success = false,
                StatusCode = StatusCodes.Status403Forbidden,
                ErrorCode = deviceCheck.ErrorCode ?? "DEVICE_AUTHORIZATION_BLOCKED",
                Message = deviceCheck.ErrorMessage ?? "Both approved device sessions are currently active. Please sign out from one approved device to continue.",
                OtpRequired = false,
                Phone = normalizedPhone,
                ActiveSessions = deviceCheck.ActiveSessions
            };
        }

        // 4. Request Admin OTP
        var otpResult = await _otpService.RequestOtpAsync(
            normalizedPhone,
            OtpPurpose.AdminLogin,
            cancellationToken);

        if (!otpResult.Success)
        {
            return new AdminLoginResult
            {
                Success = false,
                Message = otpResult.Message,
                OtpRequired = false,
                Phone = normalizedPhone
            };
        }

        string? devOtp = null;
        if (_environment.IsDevelopment() && _configuration.GetValue<bool>("Admin:ExposeDevelopmentOtp", true))
        {
            devOtp = otpResult.DevelopmentOtp;
        }

        return new AdminLoginResult
        {
            Success = true,
            Message = "Password verified. OTP sent successfully.",
            OtpRequired = true,
            Phone = normalizedPhone,
            DevelopmentOtp = devOtp
        };
    }

    public async Task<AdminMfaVerificationResult> VerifyMfaAsync(
        string phone,
        string otp,
        string? deviceCredential,
        string? deviceName,
        string? fingerprintTelemetry,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = NormalizePhone(phone);

        if (string.IsNullOrWhiteSpace(normalizedPhone) || string.IsNullOrWhiteSpace(otp))
        {
            return new AdminMfaVerificationResult
            {
                Success = false,
                StatusCode = 400,
                ErrorCode = "INVALID_REQUEST",
                ErrorMessage = "Phone and OTP are required."
            };
        }

        if (!AuthorizedAdminPhones.Contains(normalizedPhone))
        {
            await RecordSecurityEventAsync(
                "ADMIN_MFA_FAILED",
                "WARNING",
                ipAddress,
                userAgent,
                null,
                MaskPhone(normalizedPhone),
                new { reason = "UNAUTHORIZED_ADMIN_PHONE" },
                cancellationToken);

            return new AdminMfaVerificationResult
            {
                Success = false,
                StatusCode = 401,
                ErrorCode = "INVALID_CREDENTIALS",
                ErrorMessage = "Invalid administrative credentials."
            };
        }

        // Verify OTP with dedicated OtpPurpose.AdminLogin
        var otpResult = await _otpService.VerifyOtpAsync(
            normalizedPhone,
            otp,
            OtpPurpose.AdminLogin,
            cancellationToken);

        if (!otpResult.Success)
        {
            await RecordSecurityEventAsync(
                "ADMIN_MFA_FAILED",
                "WARNING",
                ipAddress,
                userAgent,
                null,
                MaskPhone(normalizedPhone),
                new { reason = "INVALID_OR_EXPIRED_MFA", message = otpResult.Message },
                cancellationToken);

            return new AdminMfaVerificationResult
            {
                Success = false,
                StatusCode = 401,
                ErrorCode = "INVALID_OR_EXPIRED_MFA",
                ErrorMessage = otpResult.Message ?? "Invalid or expired MFA code."
            };
        }

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Phone == normalizedPhone, cancellationToken);

        if (user == null || !user.IsActive)
        {
            return new AdminMfaVerificationResult
            {
                Success = false,
                StatusCode = 401,
                ErrorCode = "INVALID_CREDENTIALS",
                ErrorMessage = "Invalid administrative credentials."
            };
        }

        var roles = user.UserRoles
            .Where(x => x.Role != null)
            .Select(x => x.Role!.Code)
            .Distinct()
            .ToList();

        if (!roles.Contains("ADMIN"))
        {
            return new AdminMfaVerificationResult
            {
                Success = false,
                StatusCode = 403,
                ErrorCode = "FORBIDDEN",
                ErrorMessage = "Account is not authorized for administrative access."
            };
        }

        // --- Phase 18.2: Device Verification & Registration ---
        var deviceResult = await _adminDeviceService.ValidateOrRegisterDeviceAsync(
            user.Id,
            deviceCredential,
            deviceName,
            ipAddress,
            userAgent,
            fingerprintTelemetry,
            cancellationToken);

        if (!deviceResult.Success)
        {
            return new AdminMfaVerificationResult
            {
                Success = false,
                StatusCode = 403,
                ErrorCode = deviceResult.ErrorCode ?? "DEVICE_NOT_AUTHORIZED",
                ErrorMessage = deviceResult.ErrorMessage ?? "Device authorization failed.",
                ActiveSessions = deviceResult.ActiveSessions
            };
        }

        // --- Phase 18.2: Session Creation ---
        var (session, rawSessionToken) = await _adminDeviceService.CreateSessionAsync(
            deviceResult.Device!.Id,
            user.Id,
            ipAddress,
            userAgent,
            cancellationToken);

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // JWT contains user claims + session_id and device_id claims
        var extraClaims = new List<Claim>
        {
            new("session_id", session.Id.ToString()),
            new("device_id", deviceResult.Device.Id.ToString())
        };

        var token = _jwtService.GenerateToken(user, roles, extraClaims);

        var authResponse = new AdminAuthResponse
        {
            AccessToken = token.AccessToken,
            ExpiresAt = token.ExpiresAt,
            User = new AdminUserInfoResponse
            {
                Id = user.Id,
                CustomerCode = user.CustomerCode,
                FullName = user.FullName,
                Phone = user.Phone,
                Roles = roles
            },
            DeviceId = deviceResult.Device.Id,
            DeviceName = deviceResult.Device.DeviceName,
            DeviceCredential = deviceResult.IssuedRawCredential ?? deviceCredential
        };

        return new AdminMfaVerificationResult
        {
            Success = true,
            StatusCode = 200,
            AuthResponse = authResponse,
            RawDeviceCredential = deviceResult.IssuedRawCredential,
            RawSessionToken = rawSessionToken
        };
    }

    private async Task RecordSecurityEventAsync(
        string eventType,
        string severity,
        string? ipAddress,
        string? userAgent,
        Guid? userId,
        string? maskedPhone,
        object details,
        CancellationToken cancellationToken)
    {
        try
        {
            var securityEvent = new SecurityEvent
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                Severity = severity,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                UserId = userId,
                MaskedPhone = maskedPhone,
                DetailsJson = JsonSerializer.Serialize(details),
                CreatedAt = DateTime.UtcNow
            };

            _db.SecurityEvents.Add(securityEvent);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record security event {EventType}", eventType);
        }
    }

    public async Task<AdminPasswordOperationResult> RequestChangePasswordOtpAsync(
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == adminUserId, cancellationToken);

        if (user == null || !user.IsActive)
        {
            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Administrator account not found or inactive."
            };
        }

        if (!AuthorizedAdminPhones.Contains(user.Phone) || !user.UserRoles.Any(ur => ur.Role?.Code == "ADMIN"))
        {
            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status403Forbidden,
                Message = "Account is not authorized for administrative password management."
            };
        }

        var otpResult = await _otpService.RequestOtpAsync(
            user.Phone,
            OtpPurpose.AdminPasswordChange,
            cancellationToken);

        if (!otpResult.Success)
        {
            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status400BadRequest,
                Message = otpResult.Message
            };
        }

        string? devOtp = null;
        if (_environment.IsDevelopment() && _configuration.GetValue<bool>("Admin:ExposeDevelopmentOtp", true))
        {
            devOtp = otpResult.DevelopmentOtp;
        }

        return new AdminPasswordOperationResult
        {
            Success = true,
            StatusCode = StatusCodes.Status200OK,
            Message = "Verification code sent to registered administrator mobile number.",
            DevelopmentOtp = devOtp
        };
    }

    public async Task<AdminPasswordOperationResult> ChangePasswordAsync(
        Guid adminUserId,
        string newPassword,
        string otp,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == adminUserId, cancellationToken);

        if (user == null || !user.IsActive)
        {
            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Administrator account not found or inactive."
            };
        }

        if (!AuthorizedAdminPhones.Contains(user.Phone) || !user.UserRoles.Any(ur => ur.Role?.Code == "ADMIN"))
        {
            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status403Forbidden,
                Message = "Account is not authorized for administrative password management."
            };
        }

        // 1. Validate New Password Complexity
        var (isComplexityValid, complexityError) = ValidatePasswordComplexity(newPassword);
        if (!isComplexityValid)
        {
            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status400BadRequest,
                Message = complexityError ?? "New password does not meet security requirements."
            };
        }

        // 2. Prevent reusing identical current password
        if (!string.IsNullOrWhiteSpace(user.PasswordHash) && _passwordService.VerifyPassword(newPassword, user.PasswordHash))
        {
            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "New password must differ from your current password."
            };
        }

        // 3. Verify OTP specifically with OtpPurpose.AdminPasswordChange
        var otpResult = await _otpService.VerifyOtpAsync(
            user.Phone,
            otp,
            OtpPurpose.AdminPasswordChange,
            cancellationToken);

        if (!otpResult.Success)
        {
            await RecordSecurityEventAsync(
                "ADMIN_PASSWORD_CHANGE_FAILED",
                "WARNING",
                ipAddress,
                userAgent,
                user.Id,
                MaskPhone(user.Phone),
                new { reason = "INVALID_OTP", message = otpResult.Message },
                cancellationToken);

            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status400BadRequest,
                Message = otpResult.Message ?? "Invalid or expired verification code."
            };
        }

        // 4. Update Password Hash
        user.PasswordHash = _passwordService.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await RecordSecurityEventAsync(
            "ADMIN_PASSWORD_CHANGED",
            "INFO",
            ipAddress,
            userAgent,
            user.Id,
            MaskPhone(user.Phone),
            new { reason = "PASSWORD_CHANGED_WITH_MFA" },
            cancellationToken);

        return new AdminPasswordOperationResult
        {
            Success = true,
            StatusCode = StatusCodes.Status200OK,
            Message = "Administrative password updated successfully."
        };
    }

    public async Task<AdminPasswordOperationResult> RequestForgotPasswordOtpAsync(
        string phone,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = NormalizePhone(phone);

        var otpResult = await _otpService.RequestOtpAsync(
            normalizedPhone,
            OtpPurpose.AdminForgotPassword,
            cancellationToken);

        string? devOtp = null;
        if (_environment.IsDevelopment() && _configuration.GetValue<bool>("Admin:ExposeDevelopmentOtp", true))
        {
            devOtp = otpResult.DevelopmentOtp;
        }

        return new AdminPasswordOperationResult
        {
            Success = true,
            StatusCode = StatusCodes.Status200OK,
            Message = "If the number belongs to an authorized administrator, an OTP has been generated.",
            DevelopmentOtp = devOtp
        };
    }

    public async Task<AdminPasswordOperationResult> ResetForgotPasswordAsync(
        string phone,
        string otp,
        string newPassword,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = NormalizePhone(phone);

        if (string.IsNullOrWhiteSpace(normalizedPhone) || !AuthorizedAdminPhones.Contains(normalizedPhone))
        {
            await RecordSecurityEventAsync(
                "ADMIN_FORGOT_PASSWORD_REJECTED",
                "WARNING",
                ipAddress,
                userAgent,
                null,
                MaskPhone(phone),
                new { reason = "UNAUTHORIZED_ADMIN_PHONE" },
                cancellationToken);

            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Invalid request or authorization failed."
            };
        }

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Phone == normalizedPhone, cancellationToken);

        if (user == null || !user.IsActive || !user.UserRoles.Any(ur => ur.Role?.Code == "ADMIN"))
        {
            await RecordSecurityEventAsync(
                "ADMIN_FORGOT_PASSWORD_REJECTED",
                "WARNING",
                ipAddress,
                userAgent,
                user?.Id,
                MaskPhone(normalizedPhone),
                new { reason = "ACCOUNT_INACTIVE_OR_NOT_ADMIN" },
                cancellationToken);

            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Invalid request or authorization failed."
            };
        }

        // Validate Password Complexity
        var (isComplexityValid, complexityError) = ValidatePasswordComplexity(newPassword);
        if (!isComplexityValid)
        {
            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status400BadRequest,
                Message = complexityError ?? "New password does not meet security requirements."
            };
        }

        if (!string.IsNullOrWhiteSpace(user.PasswordHash) && _passwordService.VerifyPassword(newPassword, user.PasswordHash))
        {
            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "New password must differ from the current password."
            };
        }

        // Verify OTP specifically with OtpPurpose.AdminForgotPassword
        var otpResult = await _otpService.VerifyOtpAsync(
            normalizedPhone,
            otp,
            OtpPurpose.AdminForgotPassword,
            cancellationToken);

        if (!otpResult.Success)
        {
            await RecordSecurityEventAsync(
                "ADMIN_FORGOT_PASSWORD_FAILED",
                "WARNING",
                ipAddress,
                userAgent,
                user.Id,
                MaskPhone(normalizedPhone),
                new { reason = "INVALID_OTP", message = otpResult.Message },
                cancellationToken);

            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status400BadRequest,
                Message = otpResult.Message ?? "Invalid or expired verification code."
            };
        }

        user.PasswordHash = _passwordService.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await RecordSecurityEventAsync(
            "ADMIN_PASSWORD_RESET",
            "INFO",
            ipAddress,
            userAgent,
            user.Id,
            MaskPhone(normalizedPhone),
            new { reason = "PASSWORD_RESET_VIA_FORGOT_PASSWORD" },
            cancellationToken);

        return new AdminPasswordOperationResult
        {
            Success = true,
            StatusCode = StatusCodes.Status200OK,
            Message = "Administrative password reset successfully. Please log in with your new password."
        };
    }

    private static (bool IsValid, string? ErrorMessage) ValidatePasswordComplexity(string password, string? currentPassword = null)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            return (false, "Password must be at least 12 characters in length.");
        }
        if (!password.Any(char.IsUpper))
        {
            return (false, "Password must contain at least one uppercase letter.");
        }
        if (!password.Any(char.IsLower))
        {
            return (false, "Password must contain at least one lowercase letter.");
        }
        if (!password.Any(char.IsDigit))
        {
            return (false, "Password must contain at least one numerical digit.");
        }
        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            return (false, "Password must contain at least one special character (e.g. !@#$%^&*).");
        }
        if (!string.IsNullOrEmpty(currentPassword) && string.Equals(password, currentPassword, StringComparison.Ordinal))
        {
            return (false, "New password must differ from the current password.");
        }
        return (true, null);
    }

    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        var digitsOnly = Regex.Replace(phone, @"[^\d]", string.Empty);

        if (digitsOnly.StartsWith("91") && digitsOnly.Length == 12)
        {
            return digitsOnly.Substring(2);
        }

        return digitsOnly;
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "UNKNOWN";

        var clean = Regex.Replace(phone, @"[^\d]", string.Empty);
        if (clean.Length >= 6)
        {
            return $"{clean.Substring(0, 2)}*****{clean.Substring(clean.Length - 3)}";
        }

        return "***";
    }
}
