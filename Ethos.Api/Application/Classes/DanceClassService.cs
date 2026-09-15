using Ethos.Api.Application.Notifications;
using Ethos.Api.Contracts.Classes;
using Ethos.Api.Contracts.Notifications;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Classes;

public class DanceClassService : IDanceClassService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notificationService;

    public DanceClassService(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _notificationService = notificationService;
    }

    public async Task<IReadOnlyList<DanceClassResponse>> GetActiveClassesAsync()
    {
        return await _dbContext.DanceClasses
            .Include(c => c.Schedules.Where(s => s.IsActive))
            .Where(c => c.IsActive && !c.IsArchived)
            .OrderBy(c => c.Name)
            .Select(c => MapToClassResponse(c))
            .ToListAsync();
    }

    public async Task<DanceClassResponse?> GetClassByIdAsync(Guid id)
    {
        var danceClass = await _dbContext.DanceClasses
            .Include(c => c.Schedules.Where(s => s.IsActive))
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive && !c.IsArchived);

        if (danceClass == null)
        {
            return null;
        }

        return MapToClassResponse(danceClass);
    }

    public async Task<IReadOnlyList<ClassScheduleResponse>> GetClassSchedulesAsync(Guid danceClassId)
    {
        return await _dbContext.ClassSchedules
            .Where(s => s.DanceClassId == danceClassId && s.IsActive && !s.DanceClass.IsArchived && s.DanceClass.IsActive)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .Select(s => MapToScheduleResponse(s))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ClassScheduleResponse>> GetAllSchedulesAsync()
    {
        return await _dbContext.ClassSchedules
            .Where(s => s.IsActive && s.DanceClass.IsActive && !s.DanceClass.IsArchived)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .Select(s => MapToScheduleResponse(s))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ClassEnrollmentResponse>> GetMyEnrollmentsAsync()
    {
        var userId = _currentUser.UserId;

        var studentProfile = await _dbContext.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (studentProfile == null)
        {
            return Array.Empty<ClassEnrollmentResponse>();
        }

        return await _dbContext.ClassEnrollments
            .Include(e => e.DanceClass)
            .Where(e => e.StudentProfileId == studentProfile.Id)
            .OrderByDescending(e => e.EnrollmentDate)
            .Select(e => new ClassEnrollmentResponse
            {
                Id = e.Id,
                DanceClassId = e.DanceClassId,
                DanceClassName = e.DanceClass.Name,
                DanceStyle = e.DanceClass.DanceStyle,
                StudentPackageId = e.StudentPackageId,
                EnrollmentDate = e.EnrollmentDate,
                Status = e.Status
            })
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DanceClassResponse>> GetMyClassesAsync()
    {
        var userId = _currentUser.UserId;

        var studentProfile = await _dbContext.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (studentProfile == null)
        {
            return Array.Empty<DanceClassResponse>();
        }

        return await _dbContext.ClassEnrollments
            .Include(e => e.DanceClass)
                .ThenInclude(c => c.Schedules.Where(s => s.IsActive))
            .Where(e => e.StudentProfileId == studentProfile.Id && e.Status == EnrollmentStatus.Active)
            .Select(e => MapToClassResponse(e.DanceClass))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ClassScheduleResponse>> GetMyScheduleAsync()
    {
        var userId = _currentUser.UserId;

        var studentProfile = await _dbContext.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (studentProfile == null)
        {
            return Array.Empty<ClassScheduleResponse>();
        }

        var enrolledClassIds = await _dbContext.ClassEnrollments
            .Where(e => e.StudentProfileId == studentProfile.Id && e.Status == EnrollmentStatus.Active)
            .Select(e => e.DanceClassId)
            .ToListAsync();

        return await _dbContext.ClassSchedules
            .Where(s => enrolledClassIds.Contains(s.DanceClassId) && s.IsActive)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .Select(s => MapToScheduleResponse(s))
            .ToListAsync();
    }

    public async Task<ClassEnrollmentResponse> EnrollInClassAsync(EnrollInClassRequest request)
    {
        var userId = _currentUser.UserId;

        var studentProfile = await _dbContext.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (studentProfile == null)
        {
            throw new InvalidOperationException("Please complete your student profile before enrolling in classes.");
        }

        var danceClass = await _dbContext.DanceClasses
            .FirstOrDefaultAsync(c => c.Id == request.DanceClassId && c.IsActive && !c.IsArchived);

        if (danceClass == null)
        {
            throw new ArgumentException("Class not found or is currently inactive/archived.");
        }

        // Transaction-safe enrollment and credit consumption
        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        // Active package requirement check
        var activePackage = await _dbContext.StudentPackages
            .Where(sp => sp.StudentProfileId == studentProfile.Id &&
                         sp.Status == StudentPackageStatus.Active &&
                         sp.ExpiryDate > DateTime.UtcNow)
            .OrderByDescending(sp => sp.ExpiryDate)
            .FirstOrDefaultAsync();

        if (activePackage == null)
        {
            throw new InvalidOperationException("An active package is required to enroll in a class.");
        }

        if (activePackage.ClassesAllowed.HasValue && activePackage.ClassesUsed >= activePackage.ClassesAllowed.Value)
        {
            throw new InvalidOperationException("You have reached the class limit for your active package.");
        }

        // Check for any existing enrollment (Active or Cancelled) to prevent unique index violation on (StudentProfileId, DanceClassId)
        var existingEnrollment = await _dbContext.ClassEnrollments
            .FirstOrDefaultAsync(e => e.StudentProfileId == studentProfile.Id &&
                                      e.DanceClassId == request.DanceClassId);

        ClassEnrollment enrollment;

        if (existingEnrollment != null)
        {
            if (existingEnrollment.Status == EnrollmentStatus.Active)
            {
                throw new InvalidOperationException("You are already actively enrolled in this class.");
            }

            // Reactivate previously cancelled enrollment
            existingEnrollment.Status = EnrollmentStatus.Active;
            existingEnrollment.StudentPackageId = activePackage.Id;
            existingEnrollment.EnrollmentDate = DateTime.UtcNow;
            existingEnrollment.UpdatedAt = DateTime.UtcNow;
            enrollment = existingEnrollment;
        }
        else
        {
            enrollment = new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                StudentProfileId = studentProfile.Id,
                DanceClassId = danceClass.Id,
                StudentPackageId = activePackage.Id,
                EnrollmentDate = DateTime.UtcNow,
                Status = EnrollmentStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.ClassEnrollments.Add(enrollment);
        }

        // Atomically increment ClassesUsed
        activePackage.ClassesUsed += 1;
        activePackage.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        var remaining = activePackage.ClassesAllowed.HasValue
            ? (activePackage.ClassesAllowed.Value - activePackage.ClassesUsed).ToString()
            : "Unlimited";

        await _notificationService.SendNotificationAsync(new CreateNotificationRequest
        {
            UserId = userId,
            Type = NotificationType.Class,
            Title = "Class Enrollment Confirmed 💃",
            Message = $"You are enrolled in \"{danceClass.Name}\" ({danceClass.DanceStyle}). Remaining package credits: {remaining}.",
            Channel = NotificationChannel.InApp,
            ActionUrl = "/student/classes",
            EventKey = $"ClassEnrollment:{enrollment.Id}:{enrollment.EnrollmentDate.Ticks}"
        });

        return new ClassEnrollmentResponse
        {
            Id = enrollment.Id,
            DanceClassId = danceClass.Id,
            DanceClassName = danceClass.Name,
            DanceStyle = danceClass.DanceStyle,
            StudentPackageId = enrollment.StudentPackageId,
            EnrollmentDate = enrollment.EnrollmentDate,
            Status = enrollment.Status
        };
    }

    public async Task<bool> CancelEnrollmentAsync(Guid enrollmentId)
    {
        var userId = _currentUser.UserId;

        var studentProfile = await _dbContext.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (studentProfile == null)
        {
            return false;
        }

        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        var enrollment = await _dbContext.ClassEnrollments
            .Include(e => e.DanceClass)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.StudentProfileId == studentProfile.Id);

        if (enrollment == null || enrollment.Status != EnrollmentStatus.Active)
        {
            return false;
        }

        enrollment.Status = EnrollmentStatus.Cancelled;
        enrollment.UpdatedAt = DateTime.UtcNow;

        // Atomically restore class credit if linked to a package
        if (enrollment.StudentPackageId.HasValue)
        {
            var studentPackage = await _dbContext.StudentPackages
                .FirstOrDefaultAsync(sp => sp.Id == enrollment.StudentPackageId.Value);

            if (studentPackage != null && studentPackage.ClassesUsed > 0)
            {
                studentPackage.ClassesUsed -= 1;
                studentPackage.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        await _notificationService.SendNotificationAsync(new CreateNotificationRequest
        {
            UserId = userId,
            Type = NotificationType.Class,
            Title = "Class Enrollment Cancelled",
            Message = $"Your enrollment for \"{enrollment.DanceClass?.Name ?? "Dance Class"}\" has been cancelled. One class credit has been restored to your package.",
            Channel = NotificationChannel.InApp,
            ActionUrl = "/student/classes",
            EventKey = $"ClassCancellation:{enrollment.Id}:{DateTime.UtcNow.Ticks}"
        });

        return true;
    }

    private static DanceClassResponse MapToClassResponse(DanceClass c)
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
            Schedules = c.Schedules.Select(s => MapToScheduleResponse(s)).ToList()
        };
    }

    private static ClassScheduleResponse MapToScheduleResponse(ClassSchedule s)
    {
        return new ClassScheduleResponse
        {
            Id = s.Id,
            DanceClassId = s.DanceClassId,
            DayOfWeek = s.DayOfWeek,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            StudioRoom = s.StudioRoom,
            Capacity = s.Capacity
        };
    }
}
