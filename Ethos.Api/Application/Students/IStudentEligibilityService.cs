namespace Ethos.Api.Application.Students;

public interface IStudentEligibilityService
{
    Task<StudentEligibilityResult> CheckEligibilityByPhoneAsync(
        string phone,
        CancellationToken cancellationToken = default);

    Task<bool> IsStudentPortalEligibleAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public record StudentEligibilityResult(
    bool UserExists,
    bool HasStudentRole,
    bool HasActivePackage,
    string? Message = null);
