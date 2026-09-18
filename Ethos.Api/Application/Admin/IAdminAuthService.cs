using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Application.Admin;

public class AdminPasswordOperationResult
{
    public bool Success { get; set; }

    public int StatusCode { get; set; } = 200;

    public string Message { get; set; } = null!;

    public string? Code { get; set; }
}

public interface IAdminAuthService
{
    Task<bool> ValidateCredentialsAsync(
        string phone,
        string password,
        CancellationToken cancellationToken = default);

    Task<AdminLoginResult> LoginAsync(
        string phone,
        string password,
        string? deviceCredential,
        string? deviceName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<AdminPasswordOperationResult> ChangePasswordWithCurrentAsync(
        Guid adminUserId,
        string currentPassword,
        string newPassword,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<AdminPasswordOperationResult> RequestPasswordResetAsync(
        string phone,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<AdminPasswordOperationResult> ResetPasswordWithTokenAsync(
        string token,
        string newPassword,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);
}

