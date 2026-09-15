using Ethos.Api.Application.Attendance;
using Ethos.Api.Application.Feedback;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Application.Packages;
using Ethos.Api.Application.Payments;
using Ethos.Api.Application.Students;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Dashboard;
using Ethos.Api.Contracts.Feedback;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Dashboard;

public class StudentDashboardService : IStudentDashboardService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IStudentProfileService _profileService;
    private readonly IPackageService _packageService;
    private readonly IWorkshopService _workshopService;
    private readonly IAttendanceService _attendanceService;
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notificationService;
    private readonly IStudentFeedbackService _feedbackService;

    public StudentDashboardService(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IStudentProfileService profileService,
        IPackageService packageService,
        IWorkshopService workshopService,
        IAttendanceService attendanceService,
        IPaymentService paymentService,
        INotificationService notificationService,
        IStudentFeedbackService feedbackService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _profileService = profileService;
        _packageService = packageService;
        _workshopService = workshopService;
        _attendanceService = attendanceService;
        _paymentService = paymentService;
        _notificationService = notificationService;
        _feedbackService = feedbackService;
    }

    public async Task<StudentDashboardResponse> GetStudentDashboardAsync()
    {
        var userId = _currentUser.UserId;

        var profile = await _profileService.GetMyProfileAsync();

        var activePackage = await _packageService.GetMyActivePackageAsync();

        var workshopBookings = await _workshopService.GetMyBookingsAsync();

        var attendanceSummary = await _attendanceService.GetMyAttendanceSummaryAsync();

        var payments = await _paymentService.GetMyPaymentsAsync();

        var unreadCount = await _notificationService.GetMyUnreadCountAsync();


        var studentProfileId = await _dbContext.StudentProfiles
            .AsNoTracking()
            .Where(sp => sp.UserId == userId)
            .Select(sp => (Guid?)sp.Id)
            .FirstOrDefaultAsync();

        var today = DateTime.UtcNow.Date;

        var upcomingClasses = new List<UpcomingClassSessionResponse>();

        if (studentProfileId.HasValue)
        {
            upcomingClasses = await _dbContext.ClassSessions
                .AsNoTracking()
                .Where(session =>
                    session.SessionDate >= today &&
                    session.Status == Domain.Enums.ClassSessionStatus.Scheduled &&
                    session.ClassSchedule.DanceClass.IsActive &&
                    _dbContext.ClassEnrollments.Any(
                        enrollment =>
                            enrollment.StudentProfileId == studentProfileId.Value &&
                            enrollment.DanceClassId == session.ClassSchedule.DanceClassId &&
                            enrollment.Status == Domain.Enums.EnrollmentStatus.Active))
                .OrderBy(session => session.SessionDate)
                .ThenBy(session => session.StartTime)
                .Take(5)
                .Select(session => new UpcomingClassSessionResponse
                {
                    SessionId = session.Id,
                    DanceClassId = session.ClassSchedule.DanceClassId,
                    DanceClassName = session.ClassSchedule.DanceClass.Name,
                    DanceStyle = session.ClassSchedule.DanceClass.DanceStyle,
                    SessionDate = session.SessionDate,
                    StartTime = session.StartTime,
                    EndTime = session.EndTime,
                    StudioRoom = session.ClassSchedule.StudioRoom,
                    Status = session.Status
                })
                .ToListAsync();
        }


        var upcomingWorkshops = workshopBookings
            .Where(w => w.WorkshopDate >= today)
            .OrderBy(w => w.WorkshopDate)
            .Take(5)
            .ToList();

        var learningActivity = await _feedbackService.GetLearningActivitySummaryAsync();

        return new StudentDashboardResponse
        {
            Profile = profile,
            ActivePackage = activePackage,
            UpcomingClasses = upcomingClasses,
            UpcomingWorkshops = upcomingWorkshops,
            LearningActivity = learningActivity,
            AttendanceSummary = attendanceSummary,
            RecentPayments = payments.Take(5).ToList(),
            UnreadNotifications = unreadCount
        };
    }
}
