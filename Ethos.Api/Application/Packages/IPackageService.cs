using Ethos.Api.Contracts.Packages;

namespace Ethos.Api.Application.Packages;

public interface IPackageService
{
    Task<IReadOnlyList<PackageResponse>> GetActivePackagesAsync();

    Task<PackageResponse?> GetPackageByIdAsync(Guid id);

    Task<IReadOnlyList<StudentPackageResponse>> GetMyPackagesAsync();

    Task<StudentPackageResponse?> GetMyActivePackageAsync();
}
