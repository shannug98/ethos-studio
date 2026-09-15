using System.ComponentModel.DataAnnotations;

namespace Ethos.Api.Contracts.Admin;

public class AdminLoginRequest
{
    [Required]
    [Phone]
    public string Phone { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;

    public string? DeviceCredential { get; set; }

    [StringLength(100)]
    public string? DeviceName { get; set; }
}

public class AdminVerifyMfaRequest
{
    [Required]
    [Phone]
    public string Phone { get; set; } = null!;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Otp { get; set; } = null!;

    public string? DeviceCredential { get; set; }

    [StringLength(100)]
    public string? DeviceName { get; set; }

    [StringLength(2000)]
    public string? FingerprintTelemetry { get; set; }
}

public class AdminLoginResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = null!;

    public bool OtpRequired { get; set; }

    public string Phone { get; set; } = null!;

    public string? DevelopmentOtp { get; set; }

    public string? ErrorCode { get; set; }

    public int StatusCode { get; set; } = 200;

    public List<AdminSessionResponse>? ActiveSessions { get; set; }
}

public class AdminAuthResponse
{
    public string AccessToken { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public AdminUserInfoResponse User { get; set; } = null!;

    public Guid? DeviceId { get; set; }

    public string? DeviceName { get; set; }

    public string? DeviceCredential { get; set; }
}

public class AdminUserInfoResponse
{
    public Guid Id { get; set; }

    public string CustomerCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public List<string> Roles { get; set; } = new();
}

public class AdminDeviceResponse
{
    public Guid Id { get; set; }

    public Guid AdminUserId { get; set; }

    public string AdminFullName { get; set; } = null!;

    public string AdminPhone { get; set; } = null!;

    public string DeviceName { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime RegisteredAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    public string? LastSeenIp { get; set; }

    public string? UserAgent { get; set; }
}

public class AdminSessionResponse
{
    public Guid Id { get; set; }

    public Guid AdminDeviceId { get; set; }

    public string DeviceName { get; set; } = null!;

    public string Browser { get; set; } = string.Empty;

    public string OperatingSystem { get; set; } = string.Empty;

    public Guid AdminUserId { get; set; }

    public string PartnerName { get; set; } = string.Empty;

    public string PartnerPhone { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime LastActivityAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsActive { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime? LoggedOutAt { get; set; }

    public bool IsCurrent { get; set; }
}

public class AdminDeviceErrorResponse
{
    public string Code { get; set; } = null!;

    public string Message { get; set; } = null!;

    public List<AdminSessionResponse>? ActiveSessions { get; set; }
}

public class AdminTerminateSessionRequest
{
    [Required]
    [Phone]
    public string Phone { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;

    [Required]
    public Guid SessionId { get; set; }
}

public class AdminChangePasswordRequest
{
    [Required]
    public string NewPassword { get; set; } = null!;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Otp { get; set; } = null!;
}

public class AdminRequestOtpResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = null!;

    public string? DevelopmentOtp { get; set; }
}

public class AdminForgotPasswordRequest
{
    [Required]
    [Phone]
    public string Phone { get; set; } = null!;
}

public class AdminResetForgotPasswordRequest
{
    [Required]
    [Phone]
    public string Phone { get; set; } = null!;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Otp { get; set; } = null!;

    [Required]
    public string NewPassword { get; set; } = null!;
}

