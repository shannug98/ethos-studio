using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class StudentPackage
{
    public Guid Id { get; set; }

    public Guid StudentProfileId { get; set; }

    public Guid PackageId { get; set; }

    public Guid? PaymentTransactionId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime ExpiryDate { get; set; }

    public StudentPackageStatus Status { get; set; }

    public int? ClassesAllowed { get; set; }

    public int ClassesUsed { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public StudentProfile StudentProfile { get; set; } = null!;

    public Package Package { get; set; } = null!;
}
