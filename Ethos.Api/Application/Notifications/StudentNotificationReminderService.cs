using Ethos.Api.Contracts.Notifications;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Application.Notifications;

public class StudentNotificationReminderService : IStudentNotificationReminderService
{
    private readonly AppDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<StudentNotificationReminderService> _logger;

    public StudentNotificationReminderService(
        AppDbContext dbContext,
        INotificationService notificationService,
        ILogger<StudentNotificationReminderService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<int> ProcessPackageExpiryRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var thresholdDate = now.AddDays(5);

        var expiringPackages = await _dbContext.StudentPackages
            .Include(sp => sp.StudentProfile)
            .Include(sp => sp.Package)
            .Where(sp => sp.Status == StudentPackageStatus.Active &&
                         sp.ExpiryDate > now &&
                         sp.ExpiryDate <= thresholdDate)
            .ToListAsync(cancellationToken);

        int sentCount = 0;
        foreach (var sp in expiringPackages)
        {
            var daysRemaining = Math.Max(1, (int)Math.Ceiling((sp.ExpiryDate - now).TotalDays));
            var eventKey = $"PackageExpiryReminder:{sp.Id}:{sp.ExpiryDate:yyyyMMdd}";

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == sp.StudentProfile.UserId, cancellationToken);

            if (user == null) continue;

            await _notificationService.SendNotificationAsync(new CreateNotificationRequest
            {
                UserId = user.Id,
                Type = NotificationType.Package,
                Title = "Package Expiring Soon ⏳",
                Message = $"Your {sp.Package.Name} package expires in {daysRemaining} {(daysRemaining == 1 ? "day" : "days")} on {sp.ExpiryDate:dd MMM yyyy}. Renew soon to keep dancing!",
                Channel = NotificationChannel.InApp,
                ActionUrl = "/student/dashboard",
                EventKey = eventKey,
                SendExternal = true,
                RecipientEmail = user.Email,
                RecipientPhone = user.Phone
            }, cancellationToken);

            sentCount++;
        }

        return sentCount;
    }

    public async Task<int> ProcessLowCreditRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var lowCreditPackages = await _dbContext.StudentPackages
            .Include(sp => sp.StudentProfile)
            .Include(sp => sp.Package)
            .Where(sp => sp.Status == StudentPackageStatus.Active &&
                         sp.ExpiryDate > now &&
                         sp.ClassesAllowed.HasValue &&
                         sp.ClassesAllowed.Value - sp.ClassesUsed <= 1 &&
                         sp.ClassesAllowed.Value - sp.ClassesUsed >= 0)
            .ToListAsync(cancellationToken);

        int sentCount = 0;
        foreach (var sp in lowCreditPackages)
        {
            var remaining = sp.ClassesAllowed!.Value - sp.ClassesUsed;
            var eventKey = $"LowCreditReminder:{sp.Id}:{remaining}";

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == sp.StudentProfile.UserId, cancellationToken);

            if (user == null) continue;

            await _notificationService.SendNotificationAsync(new CreateNotificationRequest
            {
                UserId = user.Id,
                Type = NotificationType.Package,
                Title = remaining == 0 ? "Class Package Completed 🎯" : "Only 1 Class Remaining! ⚡",
                Message = remaining == 0
                    ? $"You have completed all classes in your {sp.Package.Name} pass. Top up or purchase a new package to book more classes!"
                    : $"You have 1 class left in your {sp.Package.Name} pass. Plan your next session or renew early!",
                Channel = NotificationChannel.InApp,
                ActionUrl = "/student/dashboard",
                EventKey = eventKey
            }, cancellationToken);

            sentCount++;
        }

        return sentCount;
    }

    public async Task<int> ProcessFeedbackRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var pastCutoff = now.AddDays(-14);

        var pendingClassEnrollments = await _dbContext.ClassEnrollments
            .Include(e => e.DanceClass)
            .Include(e => e.StudentProfile)
            .Where(e => e.Status == EnrollmentStatus.Active &&
                        e.EnrollmentDate >= pastCutoff &&
                        !_dbContext.ClassFeedbacks.Any(f => f.ClassEnrollmentId == e.Id))
            .ToListAsync(cancellationToken);

        int sentCount = 0;
        foreach (var e in pendingClassEnrollments)
        {
            var eventKey = $"FeedbackReminder:Class:{e.Id}";

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == e.StudentProfile.UserId, cancellationToken);

            if (user == null) continue;

            await _notificationService.SendNotificationAsync(new CreateNotificationRequest
            {
                UserId = user.Id,
                Type = NotificationType.Feedback,
                Title = "How Was Your Class? ⭐",
                Message = $"We'd love your feedback on \"{e.DanceClass.Name}\". Rate your experience to help our trainers continually improve!",
                Channel = NotificationChannel.InApp,
                ActionUrl = "/student/feedback",
                EventKey = eventKey
            }, cancellationToken);

            sentCount++;
        }

        return sentCount;
    }
}
