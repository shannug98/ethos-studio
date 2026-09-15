using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Application.Admin;

public class AdminMfaVerificationResult
{
    public bool Success { get; set; }

    public AdminAuthResponse? AuthResponse { get; set; }

    public string? RawDeviceCredential { get; set; }

    public string? RawSessionToken { get; set; }

    public int StatusCode { get; set; } = 200;

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public List<AdminSessionResponse>? ActiveSessions { get; set; }
}

public class AdminPasswordOperationResult
{
    public bool Success { get; set; }

    public int StatusCode { get; set; } = 200;

    public string Message { get; set; } = null!;

    public string? DevelopmentOtp { get; set; }
}

public interface IAdminAuthService
{
    Task<bool> ValidateCredentialsAsync(
        string phone,
        string password,
        CancellationToken cancellationToken = default);

    Task<AdminLoginResult?> LoginAsync(
        string phone,
        string password,
        string? deviceCredential,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<AdminMfaVerificationResult> VerifyMfaAsync(
        string phone,
        string otp,
        string? deviceCredential,
        string? deviceName,
        string? fingerprintTelemetry,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<AdminPasswordOperationResult> RequestChangePasswordOtpAsync(
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    Task<AdminPasswordOperationResult> ChangePasswordAsync(
        Guid adminUserId,
        string newPassword,
        string otp,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<AdminPasswordOperationResult> RequestForgotPasswordOtpAsync(
        string phone,
        CancellationToken cancellationToken = default);

    Task<AdminPasswordOperationResult> ResetForgotPasswordAsync(
        string phone,
        string otp,
        string newPassword,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);
}
