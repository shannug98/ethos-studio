using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Application.Admin;

public interface IAdminAuditService
{
    void AddAuditLog(
        Guid adminUserId,
        string actionType,
        string entityType,
        Guid entityId,
        string? reason,
        string category = "OPERATIONS",
        Guid? adminDeviceId = null,
        Guid? adminSessionId = null,
        string? traceId = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? metadataJson = null);

    Task LogActionAsync(
        Guid adminUserId,
        string actionType,
        string category,
        string entityType,
        Guid entityId,
        bool success = true,
        string? outcomeCode = null,
        string? reason = null,
        Guid? adminDeviceId = null,
        Guid? adminSessionId = null,
        string? traceId = null,
        string? requestId = null,
        string? ipAddress = null,
        string? userAgent = null,
        object? metadata = null,
        CancellationToken cancellationToken = default);

    Task LogSecurityEventAsync(
        string eventType,
        string severity,
        string? ipAddress,
        string? userAgent,
        Guid? userId = null,
        Guid? adminDeviceId = null,
        Guid? adminSessionId = null,
        string? traceId = null,
        string? maskedPhone = null,
        object? details = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AdminAuditLogResponse>> GetAuditLogsAsync(
        int page,
        int pageSize,
        string? category = null,
        string? actionType = null,
        string? entityType = null,
        Guid? entityId = null,
        Guid? adminUserId = null,
        string? traceId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    Task<AdminAuditLogResponse?> GetAuditLogByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AdminSecurityEventResponse>> GetSecurityEventsAsync(
        int page,
        int pageSize,
        string? eventType = null,
        string? severity = null,
        Guid? userId = null,
        string? traceId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    Task<AdminSecurityEventResponse?> GetSecurityEventByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
