using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminAttendanceService : IAdminAttendanceService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;

    public AdminAttendanceService(
        AppDbContext db,
        IAdminAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<AdminClassSessionResponse>> GetSessionsAsync(
        DateTime? date,
        Guid? classId,
        Guid? scheduleId,
        CancellationToken cancellationToken)
    {
        var query = _db.ClassSessions
            .AsNoTracking()
            .Include(s => s.ClassSchedule)
            .ThenInclude(cs => cs.DanceClass)
            .AsQueryable();

        if (date.HasValue)
        {
            var d = date.Value.Date;
            query = query.Where(s => s.SessionDate.Date == d);
        }

        if (scheduleId.HasValue)
            query = query.Where(s => s.ClassScheduleId == scheduleId.Value);

        if (classId.HasValue)
            query = query.Where(s => s.ClassSchedule.DanceClassId == classId.Value);

        var sessions = await query
            .OrderByDescending(s => s.SessionDate)
            .ThenBy(s => s.StartTime)
            .ToListAsync(cancellationToken);

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var scheduleIds = sessions.Select(s => s.ClassScheduleId).Distinct().ToList();
        var classIds = sessions.Select(s => s.ClassSchedule.DanceClassId).Distinct().ToList();

        var attendanceCounts = await _db.AttendanceRecords
            .AsNoTracking()
            .Where(a => sessionIds.Contains(a.ClassSessionId) && a.Status == AttendanceStatus.Present)
            .GroupBy(a => a.ClassSessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count, cancellationToken);

        var enrollmentCounts = await _db.ClassEnrollments
            .AsNoTracking()
            .Where(e => classIds.Contains(e.DanceClassId) && e.Status == EnrollmentStatus.Active)
            .GroupBy(e => e.DanceClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count, cancellationToken);

        return sessions.Select(s => new AdminClassSessionResponse
        {
            Id = s.Id,
            ClassScheduleId = s.ClassScheduleId,
            DanceClassId = s.ClassSchedule.DanceClassId,
            DanceClassName = s.ClassSchedule.DanceClass?.Name ?? "Dance Class",
            StudioRoom = s.ClassSchedule.StudioRoom,
            SessionDate = s.SessionDate,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            Status = s.Status.ToString(),
            Capacity = s.ClassSchedule.Capacity,
            EnrolledCount = enrollmentCounts.GetValueOrDefault(s.ClassSchedule.DanceClassId, 0),
            AttendedCount = attendanceCounts.GetValueOrDefault(s.Id, 0)
        }).ToList();
    }

    public async Task<AdminSessionRosterResponse?> GetSessionRosterAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await _db.ClassSessions
            .AsNoTracking()
            .Include(s => s.ClassSchedule)
            .ThenInclude(cs => cs.DanceClass)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null) return null;

        var classId = session.ClassSchedule.DanceClassId;

        var enrollments = await _db.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.StudentProfile)
            .ThenInclude(sp => sp.User)
            .Where(e => e.DanceClassId == classId && e.Status == EnrollmentStatus.Active)
            .OrderBy(e => e.StudentProfile.User.FullName)
            .ToListAsync(cancellationToken);

        var studentIds = enrollments.Select(e => e.StudentProfileId).ToList();

        var records = await _db.AttendanceRecords
            .AsNoTracking()
            .Include(a => a.StudentProfile)
            .Where(a => a.ClassSessionId == sessionId)
            .ToDictionaryAsync(a => a.StudentProfileId, cancellationToken);

        var adminUserIds = records.Values
            .Where(r => r.MarkedByUserId.HasValue)
            .Select(r => r.MarkedByUserId!.Value)
            .Distinct()
            .ToList();

        var adminNames = await _db.Users
            .AsNoTracking()
            .Where(u => adminUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var studentItems = enrollments.Select(e =>
        {
            records.TryGetValue(e.StudentProfileId, out var rec);
            string? markedByName = null;
            if (rec?.MarkedByUserId.HasValue == true)
            {
                adminNames.TryGetValue(rec.MarkedByUserId.Value, out markedByName);
            }

            return new AdminRosterStudentItem
            {
                StudentProfileId = e.StudentProfileId,
                StudentName = e.StudentProfile.User.FullName,
                StudentPhone = e.StudentProfile.User.Phone,
                CustomerCode = e.StudentProfile.User.CustomerCode,
                AttendanceStatus = rec != null ? (int)rec.Status : null,
                AttendanceStatusName = rec != null ? rec.Status.ToString() : "Unmarked",
                MarkedAt = rec?.MarkedAt,
                MarkedByAdminName = markedByName,
                Notes = rec?.Notes
            };
        }).ToList();

        return new AdminSessionRosterResponse
        {
            SessionId = session.Id,
            DanceClassId = classId,
            DanceClassName = session.ClassSchedule.DanceClass?.Name ?? "Dance Class",
            SessionDate = session.SessionDate,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            StudioRoom = session.ClassSchedule.StudioRoom,
            Capacity = session.ClassSchedule.Capacity,
            Students = studentItems
        };
    }

    public async Task MarkSessionAttendanceAsync(
        Guid sessionId,
        Guid adminUserId,
        AdminMarkAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _db.ClassSessions
            .Include(s => s.ClassSchedule)
            .ThenInclude(cs => cs.DanceClass)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
            throw new ArgumentException("Class session not found.");

        if (request.Records == null || request.Records.Count == 0)
            throw new ArgumentException("No attendance records provided.");

        var classId = session.ClassSchedule.DanceClassId;

        // Verify all students are enrolled in this class
        var requestedStudentIds = request.Records.Select(r => r.StudentProfileId).Distinct().ToList();
        var validEnrollments = await _db.ClassEnrollments
            .AsNoTracking()
            .Where(e => e.DanceClassId == classId &&
                        e.Status == EnrollmentStatus.Active &&
                        requestedStudentIds.Contains(e.StudentProfileId))
            .Select(e => e.StudentProfileId)
            .ToListAsync(cancellationToken);

        var invalidStudentIds = requestedStudentIds.Except(validEnrollments).ToList();
        if (invalidStudentIds.Any())
        {
            throw new InvalidOperationException($"One or more students are not actively enrolled in this class: {string.Join(", ", invalidStudentIds)}");
        }

        var existingRecords = await _db.AttendanceRecords
            .Where(a => a.ClassSessionId == sessionId && requestedStudentIds.Contains(a.StudentProfileId))
            .ToListAsync(cancellationToken);

        var existingMap = existingRecords.ToDictionary(a => a.StudentProfileId);

        foreach (var item in request.Records)
        {
            var targetStatus = (AttendanceStatus)item.Status;

            if (existingMap.TryGetValue(item.StudentProfileId, out var existing))
            {
                if (existing.Status != targetStatus || existing.Notes != item.Notes)
                {
                    var before = existing.Status.ToString();
                    var after = targetStatus.ToString();

                    existing.Status = targetStatus;
                    existing.Notes = item.Notes;
                    existing.MarkedAt = DateTime.UtcNow;
                    existing.MarkedByUserId = adminUserId;

                    _auditService.AddAuditLog(
                        adminUserId,
                        "ATTENDANCE_CORRECTED",
                        "AttendanceRecord",
                        existing.Id,
                        $"Corrected attendance for student {item.StudentProfileId} from {before} to {after}. Notes: {item.Notes ?? "none"}");
                }
            }
            else
            {
                var newRecord = new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    ClassSessionId = sessionId,
                    StudentProfileId = item.StudentProfileId,
                    Status = targetStatus,
                    MarkedAt = DateTime.UtcNow,
                    MarkedByUserId = adminUserId,
                    Notes = item.Notes
                };

                _db.AttendanceRecords.Add(newRecord);

                _auditService.AddAuditLog(
                    adminUserId,
                    "ATTENDANCE_MARKED",
                    "AttendanceRecord",
                    newRecord.Id,
                    $"Marked attendance as {targetStatus} for student {item.StudentProfileId}. Notes: {item.Notes ?? "none"}");
            }
        }

        if (request.Records.Count > 1)
        {
            _auditService.AddAuditLog(
                adminUserId,
                "ATTENDANCE_BULK_MARKED",
                "ClassSession",
                sessionId,
                $"Bulk marked attendance for {request.Records.Count} students in {session.ClassSchedule.DanceClass?.Name} on {session.SessionDate:yyyy-MM-dd}");
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkWorkshopAttendanceAsync(
        Guid workshopId,
        Guid adminUserId,
        AdminMarkWorkshopAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        WorkshopBooking? booking = null;

        if (request.BookingId.HasValue && request.BookingId.Value != Guid.Empty)
        {
            booking = await _db.WorkshopBookings
                .Include(b => b.Workshop)
                .Include(b => b.StudentProfile)
                .ThenInclude(sp => sp.User)
                .FirstOrDefaultAsync(b => b.Id == request.BookingId.Value, cancellationToken);
        }

        if (booking == null)
        {
            booking = await _db.WorkshopBookings
                .Include(b => b.Workshop)
                .Include(b => b.StudentProfile)
                .ThenInclude(sp => sp.User)
                .FirstOrDefaultAsync(b => b.WorkshopId == workshopId && b.StudentProfileId == request.StudentProfileId, cancellationToken);
        }

        if (booking == null)
            throw new ArgumentException("Workshop booking not found for this attendee.");

        if (booking.Status == WorkshopBookingStatus.Cancelled)
            throw new InvalidOperationException("Cannot mark attendance for a cancelled workshop booking.");

        var targetStatus = (WorkshopBookingStatus)request.Status;
        if (targetStatus != WorkshopBookingStatus.Attended &&
            targetStatus != WorkshopBookingStatus.NoShow &&
            targetStatus != WorkshopBookingStatus.Confirmed)
        {
            throw new ArgumentException("Invalid workshop attendance status. Allowed values: Attended (4), NoShow (5), or Confirmed (2 to undo).");
        }

        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var before = booking.Status;
            booking.Status = targetStatus;

            var attendeeName = booking.StudentProfile?.User?.FullName ?? "Attendee";
            string actionDescription = targetStatus switch
            {
                WorkshopBookingStatus.Attended => $"Checked in attendee {attendeeName} for workshop '{booking.Workshop?.Title}' (Status changed from {before} to ATTENDED)",
                WorkshopBookingStatus.NoShow => $"Marked attendee {attendeeName} as NO-SHOW for workshop '{booking.Workshop?.Title}' (Status changed from {before} to NO_SHOW)",
                WorkshopBookingStatus.Confirmed => $"Reverted check-in for attendee {attendeeName} for workshop '{booking.Workshop?.Title}' (Status changed from {before} to CONFIRMED)",
                _ => $"Updated attendance for attendee {attendeeName} to {targetStatus} (was: {before})"
            };

            if (before == WorkshopBookingStatus.Attended && targetStatus != WorkshopBookingStatus.Attended)
            {
                var activeFeedbacks = await _db.WorkshopFeedbacks
                    .Where(f => f.WorkshopBookingId == booking.Id && f.IsValid)
                    .ToListAsync(cancellationToken);

                foreach (var fb in activeFeedbacks)
                {
                    fb.IsValid = false;
                    fb.InvalidationReason = $"Attendance reverted from {before} to {targetStatus}.";
                    fb.InvalidatedAt = DateTime.UtcNow;

                    var meta = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        bookingId = booking.Id,
                        feedbackId = fb.Id,
                        previousAttendanceStatus = before.ToString(),
                        newAttendanceStatus = targetStatus.ToString(),
                        adminUserId = adminUserId,
                        reason = fb.InvalidationReason,
                        timestamp = DateTime.UtcNow
                    });

                    _auditService.AddAuditLog(
                        adminUserId,
                        "WORKSHOP_FEEDBACK_INVALIDATED",
                        "WorkshopFeedback",
                        fb.Id,
                        $"Quarantined feedback {fb.Id} due to attendance change for booking {booking.Id}: {before} -> {targetStatus}",
                        "OPERATIONS",
                        metadataJson: meta);
                }
            }

            _auditService.AddAuditLog(
                adminUserId,
                "WORKSHOP_ATTENDANCE_MARKED",
                "WorkshopBooking",
                booking.Id,
                actionDescription);

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}