using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Packages;

public class StudentPackageResponse
{
    public Guid Id { get; set; }

    public Guid PackageId { get; set; }

    public string PackageName { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime ExpiryDate { get; set; }

    public StudentPackageStatus Status { get; set; }

    public int? ClassesAllowed { get; set; }

    public int ClassesUsed { get; set; }

    public bool IsActive { get; set; }
}
