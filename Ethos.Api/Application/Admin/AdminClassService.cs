using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Classes;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminClassService : IAdminClassService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;

    public AdminClassService(
        AppDbContext db,
        IAdminAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<PagedResult<DanceClassResponse>> GetClassesAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.DanceClasses
            .AsNoTracking()
            .Include(c => c.Schedules)
            .Include(c => c.Enrollments)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim().ToUpperInvariant();
            if (st == "ACTIVE")
                query = query.Where(c => c.IsActive && !c.IsArchived);
            else if (st == "INACTIVE")
                query = query.Where(c => !c.IsActive && !c.IsArchived);
            else if (st == "ARCHIVED")
                query = query.Where(c => c.IsArchived);
        }
        else if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value && !c.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(s) ||
                c.DanceStyle.ToLower().Contains(s) ||
                c.Level.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var classes = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Fetch feedback counts for these classes
        var classIds = classes.Select(c => c.Id).ToList();
        var feedbackCounts = await _db.ClassFeedbacks
            .AsNoTracking()
            .Where(f => classIds.Contains(f.DanceClassId))
            .GroupBy(f => f.DanceClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ClassId, g => g.Count, cancellationToken);

        var items = classes.Select(c =>
        {
            var resp = Map(c);
            resp.TotalFeedbacks = feedbackCounts.GetValueOrDefault(c.Id, 0);
            return resp;
        }).ToList();

        return new PagedResult<DanceClassResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<DanceClassResponse?> GetClassByIdAsync(
        Guid classId,
        CancellationToken cancellationToken)
    {
        var dc = await _db.DanceClasses
            .AsNoTracking()
            .Include(c => c.Schedules)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (dc == null) return null;

        var resp = Map(dc);
        resp.TotalFeedbacks = await _db.ClassFeedbacks
            .AsNoTracking()
            .CountAsync(f => f.DanceClassId == classId, cancellationToken);

        return resp;
    }

    public async Task<DanceClassResponse> CreateClassAsync(
        Guid adminUserId,
        CreateDanceClassRequest request,
        CancellationToken cancellationToken)
    {
        var danceClass = new DanceClass
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            DanceStyle = request.DanceStyle.Trim(),
            Level = request.Level.Trim(),
            DurationMinutes = request.DurationMinutes > 0 ? request.DurationMinutes : 60,
            ImageUrl = request.ImageUrl?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.DanceClasses.Add(danceClass);

        _auditService.AddAuditLog(
            adminUserId,
            "CLASS_CREATED",
            "DanceClass",
            danceClass.Id,
            $"Created class {danceClass.Name}");

        await _db.SaveChangesAsync(cancellationToken);

        return (await GetClassByIdAsync(danceClass.Id, cancellationToken))!;
    }

    public async Task<DanceClassResponse?> UpdateClassAsync(
        Guid classId,
        Guid adminUserId,
        UpdateDanceClassRequest request,
        CancellationToken cancellationToken)
    {
        var dc = await _db.DanceClasses.FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);
        if (dc == null) return null;

        dc.Name = request.Name.Trim();
        dc.Description = request.Description?.Trim();
        dc.DanceStyle = request.DanceStyle.Trim();
        dc.Level = request.Level.Trim();
        dc.DurationMinutes = request.DurationMinutes > 0 ? request.DurationMinutes : dc.DurationMinutes;
        dc.ImageUrl = request.ImageUrl?.Trim();
        dc.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "CLASS_UPDATED",
            "DanceClass",
            dc.Id,
            $"Updated class {dc.Name}");

        await _db.SaveChangesAsync(cancellationToken);

        return await GetClassByIdAsync(dc.Id, cancellationToken);
    }

    public async Task UpdateClassStatusAsync(
        Guid classId,
        Guid adminUserId,
        bool isActive,
        string? reason,
        CancellationToken cancellationToken)
    {
        var dc = await _db.DanceClasses.FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);
        if (dc == null)
        {
            throw new ArgumentException("Class not found.");
        }

        if (dc.IsActive == isActive) return;

        dc.IsActive = isActive;
        dc.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "CLASS_STATUS_CHANGED",
            "DanceClass",
            dc.Id,
            reason ?? $"Class status updated to {(isActive ? "Active" : "Inactive")}");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ClassDependencyCheckResponse> CheckClassDependenciesAsync(
        Guid classId,
        CancellationToken cancellationToken)
    {
        var dc = await _db.DanceClasses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (dc == null)
            throw new ArgumentException("Dance class not found.");

        var enrollmentsCount = await _db.ClassEnrollments
            .AsNoTracking()
            .CountAsync(e => e.DanceClassId == classId, cancellationToken);

        var scheduleIds = await _db.ClassSchedules
            .AsNoTracking()
            .Where(s => s.DanceClassId == classId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var sessionIds = await _db.ClassSessions
            .AsNoTracking()
            .Where(s => scheduleIds.Contains(s.ClassScheduleId))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var attendanceCount = await _db.AttendanceRecords
            .AsNoTracking()
            .CountAsync(a => sessionIds.Contains(a.ClassSessionId), cancellationToken);

        var feedbacksCount = await _db.ClassFeedbacks
            .AsNoTracking()
            .CountAsync(f => f.DanceClassId == classId, cancellationToken);

        // Class bookings are represented by class enrollments; check payments referencing enrollments
        var enrollmentIds = await _db.ClassEnrollments
            .AsNoTracking()
            .Where(e => e.DanceClassId == classId)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        var paymentsCount = await _db.PaymentTransactions
            .AsNoTracking()
            .CountAsync(p => p.ReferenceId == classId || enrollmentIds.Contains(p.ReferenceId), cancellationToken);

        var activeSchedulesCount = await _db.ClassSchedules
            .AsNoTracking()
            .CountAsync(s => s.DanceClassId == classId && s.IsActive, cancellationToken);

        var totalSchedulesCount = scheduleIds.Count;

        var auditLogsCount = await _db.AdminActions
            .AsNoTracking()
            .CountAsync(a => a.EntityType == "DanceClass" && a.EntityId == classId, cancellationToken);

        var hasBusinessDependencies = enrollmentsCount > 0 ||
                                     attendanceCount > 0 ||
                                     feedbacksCount > 0 ||
                                     paymentsCount > 0 ||
                                     sessionIds.Count > 0 ||
                                     activeSchedulesCount > 0;

        // If audit history exists, archive is recommended over permanent delete to preserve accountability.
        var canDelete = !dc.IsActive && !hasBusinessDependencies && auditLogsCount == 0;
        var canArchive = !dc.IsArchived && (hasBusinessDependencies || auditLogsCount > 0);

        string reason;
        if (dc.IsActive)
        {
            reason = "Class is currently Active. Deactivate the class first.";
        }
        else if (hasBusinessDependencies)
        {
            reason = "This class cannot be permanently deleted because historical booking, enrollment, attendance, or feedback records exist. You can archive it instead.";
        }
        else if (auditLogsCount > 0)
        {
            reason = "Audit records exist for this class. To preserve operational accountability, archiving is recommended instead of permanent deletion.";
        }
        else
        {
            reason = "This class has no bookings, attendance, payments, feedback, or active schedules. Permanent deletion is allowed.";
        }

        return new ClassDependencyCheckResponse
        {
            ClassId = classId,
            CanDelete = canDelete,
            CanArchive = canArchive,
            Reason = reason,
            Dependencies = new ClassDependenciesDetail
            {
                Enrollments = enrollmentsCount,
                Bookings = enrollmentsCount, // In Ethos classes, enrollments represent bookings
                AttendanceRecords = attendanceCount,
                PaymentReferences = paymentsCount,
                FeedbackRecords = feedbacksCount,
                Schedules = totalSchedulesCount,
                AuditRecords = auditLogsCount
            }
        };
    }

    public async Task DeleteClassAsync(
        Guid classId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var dc = await _db.DanceClasses
            .Include(c => c.Schedules)
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (dc == null)
            throw new ArgumentException("Dance class not found.");

        if (dc.IsActive)
            throw new InvalidOperationException("Cannot permanently delete an active class. Please deactivate it first.");

        var check = await CheckClassDependenciesAsync(classId, cancellationToken);
        if (!check.CanDelete)
        {
            throw new InvalidOperationException(check.Reason);
        }

        // Clean up empty schedules if any
        if (dc.Schedules.Any())
        {
            _db.ClassSchedules.RemoveRange(dc.Schedules);
        }

        _db.DanceClasses.Remove(dc);

        _auditService.AddAuditLog(
            adminUserId,
            "CLASS_PERMANENTLY_DELETED",
            "DanceClass",
            classId,
            $"Permanently deleted unused class '{dc.Name}'");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveClassAsync(
        Guid classId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var dc = await _db.DanceClasses
            .Include(c => c.Schedules)
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (dc == null)
            throw new ArgumentException("Dance class not found.");

        if (dc.IsArchived) return; // Idempotent

        dc.IsArchived = true;
        dc.IsActive = false;
        dc.ArchivedAt = DateTime.UtcNow;
        dc.UpdatedAt = DateTime.UtcNow;

        // Deactivate all schedules associated with the class
        foreach (var sch in dc.Schedules)
        {
            sch.IsActive = false;
            sch.UpdatedAt = DateTime.UtcNow;
        }

        _auditService.AddAuditLog(
            adminUserId,
            "CLASS_ARCHIVED",
            "DanceClass",
            classId,
            reason ?? $"Archived dance class '{dc.Name}' and deactivated all schedules");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreClassAsync(
        Guid classId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var dc = await _db.DanceClasses
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (dc == null)
            throw new ArgumentException("Dance class not found.");

        if (!dc.IsArchived) return; // Not archived

        // RULE: Archived -> Restore -> Inactive (NEVER directly to Active)
        dc.IsArchived = false;
        dc.IsActive = false;
        dc.ArchivedAt = null;
        dc.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "CLASS_RESTORED",
            "DanceClass",
            classId,
            reason ?? $"Restored class '{dc.Name}' from archive to Inactive status");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminClassScheduleResponse>> GetClassSchedulesAsync(
        Guid classId,
        CancellationToken cancellationToken)
    {
        var dc = await _db.DanceClasses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);
        if (dc == null)
            throw new ArgumentException("Class not found.");

        var schedules = await _db.ClassSchedules
            .AsNoTracking()
            .Where(s => s.DanceClassId == classId)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .ToListAsync(cancellationToken);

        var scheduleIds = schedules.Select(s => s.Id).ToList();

        var sessionCounts = await _db.ClassSessions
            .AsNoTracking()
            .Where(s => scheduleIds.Contains(s.ClassScheduleId))
            .GroupBy(s => s.ClassScheduleId)
            .Select(g => new { ScheduleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ScheduleId, g => g.Count, cancellationToken);

        var enrollmentsCount = await _db.ClassEnrollments
            .AsNoTracking()
            .CountAsync(e => e.DanceClassId == classId && e.Status == Domain.Enums.EnrollmentStatus.Active, cancellationToken);

        return schedules.Select(s =>
        {
            var resp = MapSchedule(s, dc.Name);
            resp.TotalSessions = sessionCounts.GetValueOrDefault(s.Id, 0);
            resp.EnrolledCount = enrollmentsCount;
            return resp;
        }).ToList();
    }

    public async Task<IReadOnlyList<AdminClassScheduleResponse>> GetAllSchedulesAsync(
        CancellationToken cancellationToken)
    {
        var schedules = await _db.ClassSchedules
            .AsNoTracking()
            .Include(s => s.DanceClass)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .ToListAsync(cancellationToken);

        var scheduleIds = schedules.Select(s => s.Id).ToList();

        var sessionCounts = await _db.ClassSessions
            .AsNoTracking()
            .Where(s => scheduleIds.Contains(s.ClassScheduleId))
            .GroupBy(s => s.ClassScheduleId)
            .Select(g => new { ScheduleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ScheduleId, g => g.Count, cancellationToken);

        return schedules.Select(s =>
        {
            var resp = MapSchedule(s, s.DanceClass?.Name ?? "Unknown Class");
            resp.TotalSessions = sessionCounts.GetValueOrDefault(s.Id, 0);
            return resp;
        }).ToList();
    }

    public async Task<AdminClassScheduleResponse> CreateScheduleAsync(
        Guid classId,
        Guid adminUserId,
        AdminClassScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var dc = await _db.DanceClasses.FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);
        if (dc == null)
            throw new ArgumentException("Class not found.");

        if (request.Capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero.");

        if (request.StartTime >= request.EndTime)
            throw new ArgumentException("StartTime must be before EndTime.");

        var schedule = new ClassSchedule
        {
            Id = Guid.NewGuid(),
            DanceClassId = classId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            StudioRoom = request.StudioRoom?.Trim() ?? "Main Studio",
            Capacity = request.Capacity,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ClassSchedules.Add(schedule);

        _auditService.AddAuditLog(
            adminUserId,
            "SCHEDULE_CREATED",
            "ClassSchedule",
            schedule.Id,
            $"Created schedule for {dc.Name} on {schedule.DayOfWeek} {schedule.StartTime:hh\\:mm}-{schedule.EndTime:hh\\:mm}");

        await _db.SaveChangesAsync(cancellationToken);

        return MapSchedule(schedule, dc.Name);
    }

    public async Task<AdminClassScheduleResponse?> UpdateScheduleAsync(
        Guid classId,
        Guid scheduleId,
        Guid adminUserId,
        AdminClassScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var schedule = await _db.ClassSchedules
            .Include(s => s.DanceClass)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.DanceClassId == classId, cancellationToken);

        if (schedule == null) return null;

        if (request.Capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero.");

        if (request.StartTime >= request.EndTime)
            throw new ArgumentException("StartTime must be before EndTime.");

        schedule.DayOfWeek = request.DayOfWeek;
        schedule.StartTime = request.StartTime;
        schedule.EndTime = request.EndTime;
        schedule.StudioRoom = request.StudioRoom?.Trim() ?? schedule.StudioRoom;
        schedule.Capacity = request.Capacity;
        schedule.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "SCHEDULE_UPDATED",
            "ClassSchedule",
            schedule.Id,
            $"Updated schedule {schedule.DayOfWeek} {schedule.StartTime:hh\\:mm}-{schedule.EndTime:hh\\:mm}");

        await _db.SaveChangesAsync(cancellationToken);

        return MapSchedule(schedule, schedule.DanceClass?.Name ?? "Dance Class");
    }

    public async Task DeactivateScheduleAsync(
        Guid classId,
        Guid scheduleId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var schedule = await _db.ClassSchedules
            .Include(s => s.DanceClass)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.DanceClassId == classId, cancellationToken);

        if (schedule == null)
            throw new ArgumentException("Schedule not found.");

        if (!schedule.IsActive) return; // Idempotent

        schedule.IsActive = false;
        schedule.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "SCHEDULE_DEACTIVATED",
            "ClassSchedule",
            schedule.Id,
            reason ?? $"Deactivated schedule for {schedule.DanceClass?.Name}");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ActivateScheduleAsync(
        Guid classId,
        Guid scheduleId,
        Guid adminUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var schedule = await _db.ClassSchedules
            .Include(s => s.DanceClass)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.DanceClassId == classId, cancellationToken);

        if (schedule == null)
            throw new ArgumentException("Schedule not found.");

        if (schedule.IsActive) return; // Idempotent

        schedule.IsActive = true;
        schedule.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "SCHEDULE_ACTIVATED",
            "ClassSchedule",
            schedule.Id,
            reason ?? $"Activated schedule for {schedule.DanceClass?.Name}");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ScheduleDependencyCheckResponse> CheckScheduleDependenciesAsync(
        Guid classId,
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        var schedule = await _db.ClassSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.DanceClassId == classId, cancellationToken);

        if (schedule == null)
            throw new ArgumentException("Schedule not found.");

        var sessionsCount = await _db.ClassSessions
            .AsNoTracking()
            .CountAsync(s => s.ClassScheduleId == scheduleId, cancellationToken);

        var sessionIds = await _db.ClassSessions
            .AsNoTracking()
            .Where(s => s.ClassScheduleId == scheduleId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var attendanceCount = await _db.AttendanceRecords
            .AsNoTracking()
            .CountAsync(a => sessionIds.Contains(a.ClassSessionId), cancellationToken);

        var canDelete = sessionsCount == 0 && attendanceCount == 0;
        var reason = canDelete
            ? "This schedule has never had sessions or attendance records. Permanent deletion is allowed."
            : $"This schedule has {sessionsCount} session(s) and {attendanceCount} attendance record(s). It cannot be permanently deleted and must be deactivated/cancelled instead.";

        return new ScheduleDependencyCheckResponse
        {
            ScheduleId = scheduleId,
            CanDelete = canDelete,
            CanDeactivate = schedule.IsActive,
            SessionsCount = sessionsCount,
            AttendanceRecordsCount = attendanceCount,
            Reason = reason
        };
    }

    public async Task DeleteScheduleAsync(
        Guid classId,
        Guid scheduleId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var schedule = await _db.ClassSchedules
            .Include(s => s.DanceClass)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.DanceClassId == classId, cancellationToken);

        if (schedule == null)
            throw new ArgumentException("Schedule not found.");

        var check = await CheckScheduleDependenciesAsync(classId, scheduleId, cancellationToken);
        if (!check.CanDelete)
        {
            throw new InvalidOperationException(check.Reason);
        }

        _db.ClassSchedules.Remove(schedule);

        _auditService.AddAuditLog(
            adminUserId,
            "SCHEDULE_PERMANENTLY_DELETED",
            "ClassSchedule",
            scheduleId,
            $"Permanently deleted unused schedule for {schedule.DanceClass?.Name}");

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static AdminClassScheduleResponse MapSchedule(ClassSchedule s, string className)
    {
        return new AdminClassScheduleResponse
        {
            Id = s.Id,
            DanceClassId = s.DanceClassId,
            ClassName = className,
            DayOfWeek = s.DayOfWeek,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            StudioRoom = s.StudioRoom,
            Capacity = s.Capacity,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt
        };
    }

    private static DanceClassResponse Map(DanceClass c)
    {
        return new DanceClassResponse
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            DanceStyle = c.DanceStyle,
            Level = c.Level,
            DurationMinutes = c.DurationMinutes,
            ImageUrl = c.ImageUrl,
            IsActive = c.IsActive,
            IsArchived = c.IsArchived,
            ArchivedAt = c.ArchivedAt,
            TotalEnrollments = c.Enrollments?.Count ?? 0,
            ActiveSchedulesCount = c.Schedules?.Count(s => s.IsActive) ?? 0,
            Schedules = c.Schedules?.Select(s => new ClassScheduleResponse
            {
                Id = s.Id,
                DanceClassId = s.DanceClassId,
                DayOfWeek = s.DayOfWeek,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                StudioRoom = s.StudioRoom,
                Capacity = s.Capacity
            }).ToList() ?? new List<ClassScheduleResponse>()
        };
    }
}
