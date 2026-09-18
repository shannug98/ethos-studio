using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ethos.Api.Application.Auth;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
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
    private readonly IJwtService _jwtService;
    private readonly IPasswordService _passwordService;
    private readonly IAdminDeviceService _adminDeviceService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminAuthService> _logger;

    public AdminAuthService(
        AppDbContext db,
        IJwtService jwtService,
        IPasswordService passwordService,
        IAdminDeviceService adminDeviceService,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<AdminAuthService> logger)
    {
        _db = db;
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

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return false;
        }

        return _passwordService.VerifyPassword(password, user.PasswordHash);
    }

    public async Task<AdminLoginResult> LoginAsync(
        string phone,
        string password,
        string? deviceCredential,
        string? deviceName,
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

            return new AdminLoginResult
            {
                Success = false,
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Invalid administrative credentials."
            };
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

            return new AdminLoginResult
            {
                Success = false,
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Invalid administrative credentials."
            };
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

            return new AdminLoginResult
            {
                Success = false,
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Invalid administrative credentials."
            };
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

            return new AdminLoginResult
            {
                Success = false,
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Invalid administrative credentials."
            };
        }

        // 3. Concurrency-Safe Lockout Check
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            _logger.LogWarning("Admin login rejected: account locked out for user {UserId} until {LockoutEnd}", user.Id, user.LockoutEnd.Value);
            await RecordSecurityEventAsync(
                "ADMIN_LOGIN_LOCKED_OUT",
                "WARNING",
                ipAddress,
                userAgent,
                user.Id,
                MaskPhone(normalizedPhone),
                new { reason = "ACCOUNT_LOCKED", lockoutEnd = user.LockoutEnd.Value },
                cancellationToken);

            return new AdminLoginResult
            {
                Success = false,
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Invalid administrative credentials."
            };
        }

        // 4. Password Verification
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

            await HandleFailedLoginAttemptAsync(user, cancellationToken);

            return new AdminLoginResult
            {
                Success = false,
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Invalid administrative credentials."
            };
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

            await HandleFailedLoginAttemptAsync(user, cancellationToken);

            return new AdminLoginResult
            {
                Success = false,
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Invalid administrative credentials."
            };
        }

        // 5. Successful Password Check: Reset Lockout Counter
        if (user.FailedLoginCount > 0 || user.LockoutEnd.HasValue)
        {
            if (_db.Database.IsRelational())
            {
                await _db.Users
                    .Where(u => u.Id == user.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.FailedLoginCount, 0)
                        .SetProperty(u => u.LockoutEnd, (DateTime?)null)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow), cancellationToken);
            }
            else
            {
                user.FailedLoginCount = 0;
                user.LockoutEnd = null;
                user.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        // 6. Device Slot Enforcement Check
        var deviceCheck = await _adminDeviceService.CanAttemptLoginAsync(
            user.Id,
            deviceCredential,
            cancellationToken);

        if (!deviceCheck.Success)
        {
            _logger.LogWarning("Admin login device check failed for user {UserId}: {ErrorCode} - {ErrorMessage}", user.Id, deviceCheck.ErrorCode, deviceCheck.ErrorMessage);

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
                ActiveSessions = deviceCheck.ActiveSessions
            };
        }

        // 7. Device Validation / Registration
        var deviceResult = await _adminDeviceService.ValidateOrRegisterDeviceAsync(
            user.Id,
            deviceCredential,
            deviceName,
            ipAddress,
            userAgent,
            null,
            cancellationToken);

        if (!deviceResult.Success)
        {
            return new AdminLoginResult
            {
                Success = false,
                StatusCode = StatusCodes.Status403Forbidden,
                ErrorCode = deviceResult.ErrorCode ?? "DEVICE_NOT_AUTHORIZED",
                Message = deviceResult.ErrorMessage ?? "Device authorization failed.",
                ActiveSessions = deviceResult.ActiveSessions
            };
        }

        // 8. Session Creation
        var (session, rawSessionToken) = await _adminDeviceService.CreateSessionAsync(
            deviceResult.Device!.Id,
            user.Id,
            ipAddress,
            userAgent,
            cancellationToken);

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // JWT Claims
        var roles = user.UserRoles
            .Where(x => x.Role != null)
            .Select(x => x.Role!.Code)
            .Distinct()
            .ToList();

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

        await RecordSecurityEventAsync(
            "ADMIN_LOGIN_SUCCESSFUL",
            "INFO",
            ipAddress,
            userAgent,
            user.Id,
            MaskPhone(normalizedPhone),
            new { deviceId = deviceResult.Device.Id, sessionId = session.Id },
            cancellationToken);

        return new AdminLoginResult
        {
            Success = true,
            StatusCode = StatusCodes.Status200OK,
            Message = "Administrative login successful.",
            AuthResponse = authResponse,
            RawDeviceCredential = deviceResult.IssuedRawCredential,
            RawSessionToken = rawSessionToken
        };
    }

    private async Task HandleFailedLoginAttemptAsync(User user, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var nextCount = user.FailedLoginCount + 1;
        DateTime? lockoutEnd = nextCount >= 5 ? now.AddMinutes(15) : user.LockoutEnd;

        if (_db.Database.IsRelational())
        {
            await _db.Users
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.FailedLoginCount, nextCount)
                    .SetProperty(u => u.LockoutEnd, lockoutEnd)
                    .SetProperty(u => u.UpdatedAt, now), cancellationToken);
        }
        else
        {
            user.FailedLoginCount = nextCount;
            user.LockoutEnd = lockoutEnd;
            user.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<AdminPasswordOperationResult> ChangePasswordWithCurrentAsync(
        Guid adminUserId,
        string currentPassword,
        string newPassword,
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

        // 1. Verify Current Password
        if (string.IsNullOrWhiteSpace(user.PasswordHash) || !_passwordService.VerifyPassword(currentPassword, user.PasswordHash))
        {
            await RecordSecurityEventAsync(
                "ADMIN_PASSWORD_CHANGE_FAILED",
                "WARNING",
                ipAddress,
                userAgent,
                user.Id,
                MaskPhone(user.Phone),
                new { reason = "CURRENT_PASSWORD_MISMATCH" },
                cancellationToken);

            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Current password is incorrect."
            };
        }

        // 2. Validate New Password Complexity
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

        // 3. Prevent reusing identical current password
        if (_passwordService.VerifyPassword(newPassword, user.PasswordHash))
        {
            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "New password must differ from your current password."
            };
        }

        // 4. Update Password Hash
        user.PasswordHash = _passwordService.HashPassword(newPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await RecordSecurityEventAsync(
            "ADMIN_PASSWORD_CHANGED",
            "INFO",
            ipAddress,
            userAgent,
            user.Id,
            MaskPhone(user.Phone),
            new { reason = "PASSWORD_CHANGED_WITH_CURRENT_PASSWORD" },
            cancellationToken);

        return new AdminPasswordOperationResult
        {
            Success = true,
            StatusCode = StatusCodes.Status200OK,
            Message = "Administrative password updated successfully."
        };
    }

    public async Task<AdminPasswordOperationResult> RequestPasswordResetAsync(
        string phone,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = NormalizePhone(phone);
        if (!string.IsNullOrWhiteSpace(normalizedPhone) && AuthorizedAdminPhones.Contains(normalizedPhone))
        {
            var user = await _db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Phone == normalizedPhone, cancellationToken);

            if (user != null && user.IsActive && user.UserRoles.Any(ur => ur.Role?.Code == "ADMIN"))
            {
                // Generate cryptographically secure 256-bit (32 byte) random token
                var rawTokenBytes = RandomNumberGenerator.GetBytes(32);
                var rawToken = Convert.ToHexString(rawTokenBytes).ToLowerInvariant();
                var tokenHash = HashToken(rawToken);

                var resetToken = new PasswordResetToken
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    TokenHash = tokenHash,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                    CreatedAt = DateTime.UtcNow,
                    CreatedByIpHash = !string.IsNullOrWhiteSpace(ipAddress) ? HashToken(ipAddress) : null
                };

                _db.PasswordResetTokens.Add(resetToken);
                await _db.SaveChangesAsync(cancellationToken);

                await RecordSecurityEventAsync(
                    "ADMIN_PASSWORD_RESET_REQUESTED",
                    "INFO",
                    ipAddress,
                    userAgent,
                    user.Id,
                    MaskPhone(normalizedPhone),
                    new { tokenId = resetToken.Id, expiresAt = resetToken.ExpiresAt },
                    cancellationToken);
            }
        }

        return new AdminPasswordOperationResult
        {
            Success = true,
            StatusCode = StatusCodes.Status200OK,
            Message = "If the mobile number belongs to an authorized administrator, password reset instructions have been dispatched."
        };
    }

    public async Task<AdminPasswordOperationResult> ResetPasswordWithTokenAsync(
        string token,
        string newPassword,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Password reset token is required."
            };
        }

        var tokenHash = HashToken(token.Trim());

        var resetToken = await _db.PasswordResetTokens
            .Include(t => t.User)
            .ThenInclude(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow, cancellationToken);

        if (resetToken == null || resetToken.User == null || !resetToken.User.IsActive || !resetToken.User.UserRoles.Any(ur => ur.Role?.Code == "ADMIN"))
        {
            await RecordSecurityEventAsync(
                "ADMIN_PASSWORD_RESET_TOKEN_FAILED",
                "WARNING",
                ipAddress,
                userAgent,
                null,
                null,
                new { reason = "INVALID_OR_EXPIRED_TOKEN" },
                cancellationToken);

            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Invalid or expired password reset token."
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

        var user = resetToken.User;
        if (!string.IsNullOrWhiteSpace(user.PasswordHash) && _passwordService.VerifyPassword(newPassword, user.PasswordHash))
        {
            return new AdminPasswordOperationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "New password must differ from the current password."
            };
        }

        resetToken.UsedAt = DateTime.UtcNow;
        user.PasswordHash = _passwordService.HashPassword(newPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await RecordSecurityEventAsync(
            "ADMIN_PASSWORD_RESET_COMPLETED",
            "INFO",
            ipAddress,
            userAgent,
            user.Id,
            MaskPhone(user.Phone),
            new { tokenId = resetToken.Id },
            cancellationToken);

        return new AdminPasswordOperationResult
        {
            Success = true,
            StatusCode = StatusCodes.Status200OK,
            Message = "Administrative password reset successfully. Please log in with your new password."
        };
    }

    private static string HashToken(string raw)
    {
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static (bool IsValid, string? ErrorMessage) ValidatePasswordComplexity(string password)
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
            return (false, "Password must contain at least one numeric digit.");
        }
        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            return (false, "Password must contain at least one special character.");
        }

        return (true, null);
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

    private static string NormalizePhone(string phone)
    {
        return new string(
            phone
                .Trim()
                .Where(char.IsDigit)
                .ToArray());
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 4) return phone ?? "";
        return string.Concat("******", phone.AsSpan(phone.Length - 4));
    }
}

