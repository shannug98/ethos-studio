using Ethos.Api.Contracts.Packages;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Packages;

public class PackageService : IPackageService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public PackageService(
        AppDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PackageResponse>> GetActivePackagesAsync()
    {
        return await _dbContext.Packages
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.IsFeatured)
            .ThenBy(p => p.Price)
            .Select(p => new PackageResponse
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                DurationDays = p.DurationDays,
                ClassLimit = p.ClassLimit,
                IsFeatured = p.IsFeatured,
                FeaturesJson = p.FeaturesJson
            })
            .ToListAsync();
    }

    public async Task<PackageResponse?> GetPackageByIdAsync(Guid id)
    {
        var package = await _dbContext.Packages
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

        if (package == null)
        {
            return null;
        }

        return new PackageResponse
        {
            Id = package.Id,
            Name = package.Name,
            Description = package.Description,
            Price = package.Price,
            DurationDays = package.DurationDays,
            ClassLimit = package.ClassLimit,
            IsFeatured = package.IsFeatured,
            FeaturesJson = package.FeaturesJson
        };
    }

    public async Task<IReadOnlyList<StudentPackageResponse>> GetMyPackagesAsync()
    {
        var userId = _currentUser.UserId;

        var studentProfile = await _dbContext.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (studentProfile == null)
        {
            return Array.Empty<StudentPackageResponse>();
        }

        var now = DateTime.UtcNow;

        return await _dbContext.StudentPackages
            .Include(sp => sp.Package)
            .Where(sp => sp.StudentProfileId == studentProfile.Id)
            .OrderByDescending(sp => sp.CreatedAt)
            .Select(sp => new StudentPackageResponse
            {
                Id = sp.Id,
                PackageId = sp.PackageId,
                PackageName = sp.Package.Name,
                StartDate = sp.StartDate,
                ExpiryDate = sp.ExpiryDate,
                Status = sp.Status,
                ClassesAllowed = sp.ClassesAllowed,
                ClassesUsed = sp.ClassesUsed,
                IsActive = sp.Status == StudentPackageStatus.Active && sp.ExpiryDate > now
            })
            .ToListAsync();
    }

    public async Task<StudentPackageResponse?> GetMyActivePackageAsync()
    {
        var packages = await GetMyPackagesAsync();

        return packages.FirstOrDefault(p => p.IsActive);
    }
}
