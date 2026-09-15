using Ethos.Api.Contracts.Classes;

namespace Ethos.Api.Application.Classes;

public interface IDanceClassService
{
    Task<IReadOnlyList<DanceClassResponse>> GetActiveClassesAsync();

    Task<DanceClassResponse?> GetClassByIdAsync(Guid id);

    Task<IReadOnlyList<ClassScheduleResponse>> GetClassSchedulesAsync(Guid danceClassId);

    Task<IReadOnlyList<ClassScheduleResponse>> GetAllSchedulesAsync();

    Task<IReadOnlyList<ClassEnrollmentResponse>> GetMyEnrollmentsAsync();

    Task<IReadOnlyList<DanceClassResponse>> GetMyClassesAsync();

    Task<IReadOnlyList<ClassScheduleResponse>> GetMyScheduleAsync();

    Task<ClassEnrollmentResponse> EnrollInClassAsync(EnrollInClassRequest request);

    Task<bool> CancelEnrollmentAsync(Guid enrollmentId);
}
