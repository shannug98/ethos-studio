using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Classes;

namespace Ethos.Api.Application.Admin;

public interface IAdminClassService
{
    Task<PagedResult<DanceClassResponse>> GetClassesAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        bool? isActive,
        CancellationToken cancellationToken);

    Task<DanceClassResponse?> GetClassByIdAsync(
        Guid classId,
        CancellationToken cancellationToken);

    Task<DanceClassResponse> CreateClassAsync(
        Guid adminUserId,
        CreateDanceClassRequest request,
        CancellationToken cancellationToken);

    Task<DanceClassResponse?> UpdateClassAsync(
        Guid classId,
        Guid adminUserId,
        UpdateDanceClassRequest request,
        CancellationToken cancellationToken);

    Task UpdateClassStatusAsync(
        Guid classId,
        Guid adminUserId,
        bool isActive,
        string? reason,
        CancellationToken cancellationToken);

    Task<ClassDependencyCheckResponse> CheckClassDependenciesAsync(
        Guid classId,
        CancellationToken cancellationToken);

    Task DeleteClassAsync(
        Guid classId,
        Guid adminUserId,
        CancellationToken cancellationToken);

    Task ArchiveClassAsync(
        Guid classId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken);

    Task RestoreClassAsync(
        Guid classId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminClassScheduleResponse>> GetClassSchedulesAsync(
        Guid classId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminClassScheduleResponse>> GetAllSchedulesAsync(
        CancellationToken cancellationToken);

    Task<AdminClassScheduleResponse> CreateScheduleAsync(
        Guid classId,
        Guid adminUserId,
        AdminClassScheduleRequest request,
        CancellationToken cancellationToken);

    Task<AdminClassScheduleResponse?> UpdateScheduleAsync(
        Guid classId,
        Guid scheduleId,
        Guid adminUserId,
        AdminClassScheduleRequest request,
        CancellationToken cancellationToken);

    Task DeactivateScheduleAsync(
        Guid classId,
        Guid scheduleId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken);

    Task ActivateScheduleAsync(
        Guid classId,
        Guid scheduleId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken);

    Task<ScheduleDependencyCheckResponse> CheckScheduleDependenciesAsync(
        Guid classId,
        Guid scheduleId,
        CancellationToken cancellationToken);

    Task DeleteScheduleAsync(
        Guid classId,
        Guid scheduleId,
        Guid adminUserId,
        CancellationToken cancellationToken);
}
