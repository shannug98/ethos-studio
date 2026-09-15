using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Packages;

namespace Ethos.Api.Application.Admin;

public interface IAdminPackageService
{
    Task<PagedResult<PackageResponse>> GetPackagesAsync(
        int page,
        int pageSize,
        string? search,
        bool? isActive,
        CancellationToken cancellationToken);

    Task<PackageResponse?> GetPackageByIdAsync(
        Guid packageId,
        CancellationToken cancellationToken);

    Task<PackageResponse> CreatePackageAsync(
        Guid adminUserId,
        CreatePackageRequest request,
        CancellationToken cancellationToken);

    Task<PackageResponse?> UpdatePackageAsync(
        Guid packageId,
        Guid adminUserId,
        UpdatePackageRequest request,
        CancellationToken cancellationToken);

    Task UpdatePackageStatusAsync(
        Guid packageId,
        Guid adminUserId,
        bool isActive,
        string? reason,
        CancellationToken cancellationToken);

    Task<AdminPackageStatsResponse> GetPackageStatsAsync(
        CancellationToken cancellationToken);

    Task<PackageDependencyCheckResponse> CheckPackageDependenciesAsync(
        Guid packageId,
        CancellationToken cancellationToken);

    Task DeletePackageAsync(
        Guid packageId,
        Guid adminUserId,
        CancellationToken cancellationToken);

    Task<AdminPackageDetailResponse?> GetPackageDetailAsync(
        Guid packageId,
        CancellationToken cancellationToken);

    Task<PagedResult<AdminPackageActivityItem>> GetPackageActivityHistoryAsync(
        Guid packageId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
