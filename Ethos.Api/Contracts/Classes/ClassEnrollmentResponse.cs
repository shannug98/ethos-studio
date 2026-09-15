using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Classes;

public class ClassEnrollmentResponse
{
    public Guid Id { get; set; }

    public Guid DanceClassId { get; set; }

    public string DanceClassName { get; set; } = string.Empty;

    public string DanceStyle { get; set; } = string.Empty;

    public Guid? StudentPackageId { get; set; }

    public DateTime EnrollmentDate { get; set; }

    public EnrollmentStatus Status { get; set; }
}
