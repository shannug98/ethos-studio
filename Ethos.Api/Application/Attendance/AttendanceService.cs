using Ethos.Api.Contracts.Attendance;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Attendance;

public class AttendanceService : IAttendanceService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public AttendanceService(
        AppDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AttendanceRecordResponse>> GetMyAttendanceAsync()
    {
        var userId = _currentUser.UserId;

        var studentProfile = await _dbContext.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (studentProfile == null)
        {
            return Array.Empty<AttendanceRecordResponse>();
        }

        return await _dbContext.AttendanceRecords
            .Include(a => a.ClassSession)
                .ThenInclude(cs => cs.ClassSchedule)
                    .ThenInclude(sch => sch.DanceClass)
            .Where(a => a.StudentProfileId == studentProfile.Id)
            .OrderByDescending(a => a.MarkedAt)
            .Select(a => new AttendanceRecordResponse
            {
                Id = a.Id,
                ClassSessionId = a.ClassSessionId,
                SessionDate = a.ClassSession.SessionDate,
                DanceClassName = a.ClassSession.ClassSchedule.DanceClass.Name,
                Status = a.Status,
                MarkedAt = a.MarkedAt,
                Notes = a.Notes
            })
            .ToListAsync();
    }

    public async Task<AttendanceSummaryResponse> GetMyAttendanceSummaryAsync()
    {
        var records = await GetMyAttendanceAsync();

        if (records.Count == 0)
        {
            return new AttendanceSummaryResponse();
        }

        var total = records.Count;
        var present = records.Count(r => r.Status == AttendanceStatus.Present);
        var absent = records.Count(r => r.Status == AttendanceStatus.Absent);
        var late = records.Count(r => r.Status == AttendanceStatus.Late);
        var excused = records.Count(r => r.Status == AttendanceStatus.Excused);

        var percentage = total > 0 ? Math.Round(((double)(present + late) / total) * 100, 2) : 0.0;

        return new AttendanceSummaryResponse
        {
            TotalSessions = total,
            Present = present,
            Absent = absent,
            Late = late,
            Excused = excused,
            AttendancePercentage = percentage
        };
    }
}
