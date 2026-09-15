using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Application.Admin;

public interface IAdminAttendanceService
{
    Task<IReadOnlyList<AdminClassSessionResponse>> GetSessionsAsync(
        DateTime? date,
        Guid? classId,
        Guid? scheduleId,
        CancellationToken cancellationToken);

    Task<AdminSessionRosterResponse?> GetSessionRosterAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    Task MarkSessionAttendanceAsync(
        Guid sessionId,
        Guid adminUserId,
        AdminMarkAttendanceRequest request,
        CancellationToken cancellationToken);

    Task MarkWorkshopAttendanceAsync(
        Guid workshopId,
        Guid adminUserId,
        AdminMarkWorkshopAttendanceRequest request,
        CancellationToken cancellationToken);
}