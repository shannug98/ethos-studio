using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class ClassEnrollment
{
    public Guid Id { get; set; }

    public Guid StudentProfileId { get; set; }

    public Guid DanceClassId { get; set; }

    public Guid? StudentPackageId { get; set; }

    public DateTime EnrollmentDate { get; set; }

    public EnrollmentStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public StudentProfile StudentProfile { get; set; } = null!;

    public DanceClass DanceClass { get; set; } = null!;

    public StudentPackage? StudentPackage { get; set; }
}
