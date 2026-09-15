using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;

namespace Ethos.Api.Application.Admin;

public class DeviceAuthResult
{
    public bool Success { get; set; }

    public AdminDevice? Device { get; set; }

    public string? IssuedRawCredential { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public List<AdminSessionResponse>? ActiveSessions { get; set; }
}

public interface IAdminDeviceService
{
    Task<DeviceAuthResult> CanAttemptLoginAsync(
        Guid userId,
        string? rawDeviceCredential,
        CancellationToken cancellationToken = default);

    Task<DeviceAuthResult> ValidateOrRegisterDeviceAsync(
        Guid userId,
        string? rawDeviceCredential,
        string? rawDeviceName,
        string? ipAddress,
        string? userAgent,
        string? fingerprintTelemetry,
        CancellationToken cancellationToken = default);

    Task<(AdminSession Session, string RawSessionToken)> CreateSessionAsync(
        Guid deviceId,
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<bool> HeartbeatSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<int> CleanupInactiveSessionsAsync(
        CancellationToken cancellationToken = default);

    Task<List<AdminDeviceResponse>> GetDevicesAsync(
        CancellationToken cancellationToken = default);

    Task<bool> RevokeDeviceAsync(
        Guid deviceId,
        Guid requestingUserId,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    Task<List<AdminSessionResponse>> GetActiveSessionsAsync(
        Guid? userId = null,
        Guid? currentSessionId = null,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeSessionAsync(
        Guid sessionId,
        Guid requestingUserId,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    Task<bool> LogoutSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<int> LogoutAllSessionsAsync(
        Guid? userId = null,
        CancellationToken cancellationToken = default);
}
