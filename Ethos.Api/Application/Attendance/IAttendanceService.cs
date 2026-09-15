using Ethos.Api.Contracts.Attendance;

namespace Ethos.Api.Application.Attendance;

public interface IAttendanceService
{
    Task<IReadOnlyList<AttendanceRecordResponse>> GetMyAttendanceAsync();

    Task<AttendanceSummaryResponse> GetMyAttendanceSummaryAsync();
}
