using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Application.Admin;

public interface IAdminStudentService
{
    Task<PagedResult<AdminStudentListResponse>> GetStudentsAsync(
        int page,
        int pageSize,
        string? search,
        string? profileStatus,
        string? accountStatus,
        string? packageStatus,
        CancellationToken cancellationToken);

    Task<AdminStudentSummaryStatsResponse> GetStudentStatsAsync(
        CancellationToken cancellationToken);

    Task<AdminStudentDetailsResponse?> GetStudentByIdAsync(
        Guid studentId,
        CancellationToken cancellationToken);

    Task<StudentDiagnosticReport?> GetStudentDiagnosticsAsync(
        Guid studentId,
        string? traceId,
        CancellationToken cancellationToken);
}
