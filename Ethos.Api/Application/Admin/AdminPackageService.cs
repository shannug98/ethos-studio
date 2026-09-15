using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Packages;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminPackageService : IAdminPackageService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;

    public AdminPackageService(
        AppDbContext db,
        IAdminAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<PagedResult<PackageResponse>> GetPackagesAsync(
        int page,
        int pageSize,
        string? search,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Packages
            .AsNoTracking()
            .AsQueryable();

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(s) ||
                (p.Description != null && p.Description.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var packages = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = packages.Select(p => Map(p)).ToList();

        return new PagedResult<PackageResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<PackageResponse?> GetPackageByIdAsync(
        Guid packageId,
        CancellationToken cancellationToken)
    {
        var pkg = await _db.Packages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == packageId, cancellationToken);

        return pkg == null ? null : Map(pkg);
    }

    public async Task<PackageResponse> CreatePackageAsync(
        Guid adminUserId,
        CreatePackageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Package name is required.");
        if (request.Price <= 0)
            throw new ArgumentException("Package price must be greater than zero.");
        if (request.DurationDays <= 0)
            throw new ArgumentException("Package duration in days must be greater than zero.");
        if (request.ClassLimit.HasValue && request.ClassLimit.Value <= 0)
            throw new ArgumentException("Package class limit must be greater than zero.");

        var pkg = new Package
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            DurationDays = request.DurationDays,
            ClassLimit = request.ClassLimit,
            IsFeatured = request.IsFeatured,
            FeaturesJson = request.FeaturesJson?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Packages.Add(pkg);

        _auditService.AddAuditLog(
            adminUserId,
            "PACKAGE_CREATED",
            "Package",
            pkg.Id,
            $"Created package {pkg.Name}");

        await _db.SaveChangesAsync(cancellationToken);

        return Map(pkg);
    }

    public async Task<PackageResponse?> UpdatePackageAsync(
        Guid packageId,
        Guid adminUserId,
        UpdatePackageRequest request,
        CancellationToken cancellationToken)
    {
        var pkg = await _db.Packages.FirstOrDefaultAsync(p => p.Id == packageId, cancellationToken);
        if (pkg == null) return null;

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Package name is required.");
        if (request.Price <= 0)
            throw new ArgumentException("Package price must be greater than zero.");
        if (request.DurationDays <= 0)
            throw new ArgumentException("Package duration in days must be greater than zero.");
        if (request.ClassLimit.HasValue && request.ClassLimit.Value <= 0)
            throw new ArgumentException("Package class limit must be greater than zero.");

        var oldName = pkg.Name;
        var oldPrice = pkg.Price;
        var oldLimit = pkg.ClassLimit;
        var oldDays = pkg.DurationDays;

        pkg.Name = request.Name.Trim();
        pkg.Description = request.Description?.Trim();
        pkg.Price = request.Price;
        pkg.DurationDays = request.DurationDays;
        pkg.ClassLimit = request.ClassLimit;
        pkg.IsFeatured = request.IsFeatured;
        pkg.FeaturesJson = request.FeaturesJson?.Trim();
        pkg.UpdatedAt = DateTime.UtcNow;

        var changeSummary = $"Updated package '{pkg.Name}': Price ₹{oldPrice} -> ₹{pkg.Price}, Validity {oldDays}d -> {pkg.DurationDays}d, Quota {oldLimit?.ToString() ?? "Unlimited"} -> {pkg.ClassLimit?.ToString() ?? "Unlimited"}";
        var metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            packageId = pkg.Id,
            oldValues = new { name = oldName, price = oldPrice, durationDays = oldDays, classLimit = oldLimit },
            newValues = new { name = pkg.Name, price = pkg.Price, durationDays = pkg.DurationDays, classLimit = pkg.ClassLimit }
        });

        _auditService.AddAuditLog(
            adminUserId,
            "PACKAGE_UPDATED",
            "Package",
            pkg.Id,
            changeSummary,
            category: "OPERATIONS",
            metadataJson: metadata);

        await _db.SaveChangesAsync(cancellationToken);

        return Map(pkg);
    }

    public async Task UpdatePackageStatusAsync(
        Guid packageId,
        Guid adminUserId,
        bool isActive,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is mandatory for changing package status.");
        }

        var pkg = await _db.Packages.FirstOrDefaultAsync(p => p.Id == packageId, cancellationToken);
        if (pkg == null)
        {
            throw new ArgumentException("Package not found.");
        }

        if (pkg.IsActive == isActive) return;

        pkg.IsActive = isActive;
        pkg.UpdatedAt = DateTime.UtcNow;

        var actionType = isActive ? "PACKAGE_ACTIVATED" : "PACKAGE_DEACTIVATED";
        _auditService.AddAuditLog(
            adminUserId,
            actionType,
            "Package",
            pkg.Id,
            reason);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminPackageStatsResponse> GetPackageStatsAsync(
        CancellationToken cancellationToken)
    {
        var totalPackages = await _db.Packages.CountAsync(cancellationToken);
        var availableForNewPurchases = await _db.Packages.CountAsync(p => p.IsActive, cancellationToken);
        var notAvailableForNewPurchases = await _db.Packages.CountAsync(p => !p.IsActive, cancellationToken);

        // Student packages purchased (excluding cancelled if any)
        var studentPackagesPurchased = await _db.StudentPackages
            .CountAsync(sp => sp.Status != Domain.Enums.StudentPackageStatus.Cancelled, cancellationToken);

        // Total Revenue: strictly sum of successful (Status == Paid) package purchase payment transactions
        var totalRevenue = await _db.PaymentTransactions
            .Where(pt => pt.Purpose == Domain.Enums.PaymentPurpose.PackagePurchase &&
                         pt.Status == Domain.Enums.PaymentStatus.Paid)
            .SumAsync(pt => (decimal?)pt.Amount, cancellationToken) ?? 0m;

        // Included classes allocated and remaining
        var activeStudentPackages = await _db.StudentPackages
            .Where(sp => sp.Status != Domain.Enums.StudentPackageStatus.Cancelled)
            .Select(sp => new { sp.ClassesAllowed, sp.ClassesUsed })
            .ToListAsync(cancellationToken);

        var includedClassesAllocated = activeStudentPackages.Sum(sp => sp.ClassesAllowed ?? 0);
        var includedClassesUsed = activeStudentPackages.Sum(sp => sp.ClassesUsed);
        var includedClassesRemaining = Math.Max(0, includedClassesAllocated - includedClassesUsed);

        // Average catalog package price (kept separate from revenue)
        var averagePackagePrice = totalPackages > 0
            ? await _db.Packages.AverageAsync(p => p.Price, cancellationToken)
            : 0m;

        return new AdminPackageStatsResponse
        {
            TotalPackages = totalPackages,
            AvailableForNewPurchases = availableForNewPurchases,
            NotAvailableForNewPurchases = notAvailableForNewPurchases,
            StudentPackagesPurchased = studentPackagesPurchased,
            TotalRevenue = totalRevenue,
            IncludedClassesAllocated = includedClassesAllocated,
            IncludedClassesRemaining = includedClassesRemaining,
            AveragePackagePrice = Math.Round(averagePackagePrice, 2)
        };
    }

    public async Task<PackageDependencyCheckResponse> CheckPackageDependenciesAsync(
        Guid packageId,
        CancellationToken cancellationToken)
    {
        var pkg = await _db.Packages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == packageId, cancellationToken);

        if (pkg == null)
            throw new ArgumentException("Dance package not found.");

        var studentPackages = await _db.StudentPackages
            .AsNoTracking()
            .Where(sp => sp.PackageId == packageId)
            .ToListAsync(cancellationToken);

        var studentPackageIds = studentPackages.Select(sp => sp.Id).ToList();

        var studentPackageRecords = studentPackages.Count;
        var activeStudentPackages = studentPackages.Count(sp => sp.Status == Domain.Enums.StudentPackageStatus.Active);

        var paymentRecords = await _db.PaymentTransactions
            .AsNoTracking()
            .CountAsync(pt => pt.ReferenceId == packageId ||
                              (pt.Purpose == Domain.Enums.PaymentPurpose.PackagePurchase && studentPackageIds.Contains(pt.ReferenceId)),
                        cancellationToken);

        var classEnrollmentRecords = await _db.ClassEnrollments
            .AsNoTracking()
            .CountAsync(ce => ce.StudentPackageId.HasValue && studentPackageIds.Contains(ce.StudentPackageId.Value), cancellationToken);

        var hasDependencies = studentPackageRecords > 0 || paymentRecords > 0 || classEnrollmentRecords > 0;

        return new PackageDependencyCheckResponse
        {
            PackageId = pkg.Id,
            PackageName = pkg.Name,
            CanDelete = !hasDependencies,
            CanDeactivate = true,
            StatusMessage = hasDependencies
                ? "This package cannot be permanently deleted because existing student or payment records use it. Stop New Purchases instead."
                : "This package has no student purchase or payment records and can be safely deleted.",
            RecommendedAction = hasDependencies
                ? "Stop New Purchases (Deactivate package to preserve student quota history)"
                : "Safe to delete",
            RecordsUsingThisPackage = new PackageDependenciesDetail
            {
                StudentPackageRecords = studentPackageRecords,
                ActiveStudentPackages = activeStudentPackages,
                PaymentRecords = paymentRecords,
                ClassEnrollmentRecords = classEnrollmentRecords
            }
        };
    }

    public async Task DeletePackageAsync(
        Guid packageId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var pkg = await _db.Packages.FirstOrDefaultAsync(p => p.Id == packageId, cancellationToken);
        if (pkg == null)
            throw new ArgumentException("Dance package not found.");

        // Check dependencies
        var check = await CheckPackageDependenciesAsync(packageId, cancellationToken);
        if (!check.CanDelete)
        {
            throw new InvalidOperationException("This package cannot be permanently deleted because existing student or payment records use it. Stop New Purchases instead.");
        }

        _db.Packages.Remove(pkg);

        _auditService.AddAuditLog(
            adminUserId,
            "PACKAGE_DELETED",
            "Package",
            pkg.Id,
            $"Permanently deleted unreferenced package {pkg.Name}");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminPackageDetailResponse?> GetPackageDetailAsync(
        Guid packageId,
        CancellationToken cancellationToken)
    {
        var pkg = await _db.Packages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == packageId, cancellationToken);

        if (pkg == null) return null;

        var now = DateTime.UtcNow;

        // Student Packages metrics using authoritative fields ClassesAllowed and ClassesUsed
        var studentPackagesQuery = _db.StudentPackages
            .AsNoTracking()
            .Where(sp => sp.PackageId == packageId);

        var studentPackagesPurchased = await studentPackagesQuery.CountAsync(cancellationToken);

        var activeStudentPasses = await studentPackagesQuery
            .CountAsync(sp => sp.Status == Domain.Enums.StudentPackageStatus.Active && sp.ExpiryDate > now, cancellationToken);

        var totalClassesAllocated = await studentPackagesQuery
            .SumAsync(sp => sp.ClassesAllowed ?? 0, cancellationToken);

        var totalClassesUsed = await studentPackagesQuery
            .SumAsync(sp => sp.ClassesUsed, cancellationToken);

        var totalClassesRemaining = Math.Max(0, totalClassesAllocated - totalClassesUsed);

        double? classConsumptionRatePercent = totalClassesAllocated > 0
            ? Math.Round(((double)totalClassesUsed / totalClassesAllocated) * 100.0, 1)
            : null;

        // Class Bookings from ClassEnrollments associated with this package's student packages
        var studentPackageIds = await studentPackagesQuery
            .Select(sp => sp.Id)
            .ToListAsync(cancellationToken);

        var classEnrollmentRecordsCount = await _db.ClassEnrollments
            .AsNoTracking()
            .CountAsync(ce => ce.StudentPackageId.HasValue && studentPackageIds.Contains(ce.StudentPackageId.Value), cancellationToken);

        // Payment transactions count related to this package
        var paymentRecordsCount = await _db.PaymentTransactions
            .AsNoTracking()
            .CountAsync(pt => pt.ReferenceId == packageId ||
                              (pt.Purpose == Domain.Enums.PaymentPurpose.PackagePurchase && studentPackageIds.Contains(pt.ReferenceId)),
                        cancellationToken);

        var featuresList = ParseFeatures(pkg.FeaturesJson);

        return new AdminPackageDetailResponse
        {
            Id = pkg.Id,
            Name = pkg.Name,
            Description = pkg.Description,
            Price = pkg.Price,
            DurationDays = pkg.DurationDays,
            ClassLimit = pkg.ClassLimit,
            IsActive = pkg.IsActive,
            IsFeatured = pkg.IsFeatured,
            Features = featuresList,
            CreatedAt = pkg.CreatedAt,
            UpdatedAt = pkg.UpdatedAt,
            StudentPackagesPurchased = studentPackagesPurchased,
            ActiveStudentPasses = activeStudentPasses,
            TotalClassesAllocated = totalClassesAllocated,
            TotalClassesUsed = totalClassesUsed,
            TotalClassesRemaining = totalClassesRemaining,
            ClassConsumptionRatePercent = classConsumptionRatePercent,
            PaymentRecordsCount = paymentRecordsCount,
            ClassEnrollmentRecordsCount = classEnrollmentRecordsCount
        };
    }

    public async Task<PagedResult<AdminPackageActivityItem>> GetPackageActivityHistoryAsync(
        Guid packageId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 50) pageSize = 50;

        // Query AdminActions for EntityType == "PACKAGE" / "Package" and EntityId == packageId directly
        // This ensures PACKAGE_DELETED and past audit history remain queryable even if Package row was deleted.
        var query = _db.AdminActions
            .AsNoTracking()
            .Where(a => (a.EntityType == "PACKAGE" || a.EntityType == "Package") && a.EntityId == packageId);

        var totalCount = await query.CountAsync(cancellationToken);

        var rawActions = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var adminUserIds = rawActions.Select(a => a.AdminUserId).Distinct().ToList();
        var adminUsers = await _db.Users
            .AsNoTracking()
            .Where(u => adminUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var items = rawActions.Select(a =>
        {
            PackageUpdateMetadata? updateDetails = null;
            if (!string.IsNullOrWhiteSpace(a.MetadataJson))
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(a.MetadataJson);
                    if (parsed.TryGetProperty("oldValues", out var oldVal) && parsed.TryGetProperty("newValues", out var newVal))
                    {
                        updateDetails = new PackageUpdateMetadata
                        {
                            PackageId = packageId,
                            OldValues = new PackageAuditValues
                            {
                                Name = oldVal.TryGetProperty("name", out var on) ? on.GetString() : null,
                                Price = oldVal.TryGetProperty("price", out var op) && op.TryGetDecimal(out var opDec) ? opDec : null,
                                DurationDays = oldVal.TryGetProperty("durationDays", out var od) && od.TryGetInt32(out var odInt) ? odInt : null,
                                ClassLimit = oldVal.TryGetProperty("classLimit", out var ocl) && ocl.TryGetInt32(out var oclInt) ? oclInt : null,
                            },
                            NewValues = new PackageAuditValues
                            {
                                Name = newVal.TryGetProperty("name", out var nn) ? nn.GetString() : null,
                                Price = newVal.TryGetProperty("price", out var np) && np.TryGetDecimal(out var npDec) ? npDec : null,
                                DurationDays = newVal.TryGetProperty("durationDays", out var nd) && nd.TryGetInt32(out var ndInt) ? ndInt : null,
                                ClassLimit = newVal.TryGetProperty("classLimit", out var ncl) && ncl.TryGetInt32(out var nclInt) ? nclInt : null,
                            }
                        };
                    }
                }
                catch
                {
                    // Fallback to null if custom metadata format
                }
            }

            return new AdminPackageActivityItem
            {
                Id = a.Id,
                ActionType = a.ActionType,
                DisplayAction = FormatActionType(a.ActionType),
                Reason = a.Reason,
                AdminName = adminUsers.TryGetValue(a.AdminUserId, out var name) ? name : "Administrator",
                TraceId = a.TraceId,
                Timestamp = a.CreatedAt,
                UpdateDetails = updateDetails
            };
        }).ToList();

        return new PagedResult<AdminPackageActivityItem>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    private static string FormatActionType(string actionType)
    {
        return actionType switch
        {
            "PACKAGE_CREATED" => "Package Created",
            "PACKAGE_UPDATED" => "Package Updated",
            "PACKAGE_DEACTIVATED" => "New Purchases Stopped",
            "PACKAGE_ACTIVATED" => "New Purchases Allowed",
            "PACKAGE_DELETED" => "Package Permanently Deleted",
            _ => actionType.Replace("_", " ")
        };
    }

    private static List<string> ParseFeatures(string? featuresJson)
    {
        if (string.IsNullOrWhiteSpace(featuresJson)) return new List<string>();

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(featuresJson);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var str = el.GetString();
                        if (!string.IsNullOrWhiteSpace(str)) list.Add(str.Trim());
                    }
                }
                return list;
            }
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                var list = new List<string>();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                        list.Add($"{prop.Name}: {prop.Value.GetString()}");
                    else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.True)
                        list.Add(prop.Name);
                }
                return list;
            }
        }
        catch
        {
            // If comma-separated or plain text fallback
            if (featuresJson.Contains(","))
            {
                return featuresJson.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }
        }

        return new List<string> { featuresJson.Trim() };
    }

    private static PackageResponse Map(Package p)
    {
        return new PackageResponse
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            DurationDays = p.DurationDays,
            ClassLimit = p.ClassLimit,
            IsFeatured = p.IsFeatured,
            FeaturesJson = p.FeaturesJson,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt
        };
    }
}
