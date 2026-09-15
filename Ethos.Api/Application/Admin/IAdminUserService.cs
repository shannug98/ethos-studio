using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Application.Admin;

public interface IAdminUserService
{
    Task<PagedResult<AdminUserListResponse>> GetUsersAsync(
        int page,
        int pageSize,
        string? search,
        string? role,
        bool? isActive,
        string? sortBy,
        CancellationToken cancellationToken);

    Task<AdminUserDetailsResponse?> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task UpdateUserStatusAsync(
        Guid userId,
        Guid adminUserId,
        bool isActive,
        string? reason,
        CancellationToken cancellationToken);

    Task<AdminUserAuditHistoryResponse?> GetUserAuditHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
