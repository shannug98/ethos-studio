using System.Globalization;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Trainers;

public class TrainerPerformanceService : ITrainerPerformanceService
{
    private readonly AppDbContext _db;
    private readonly ITrainerPermissionService _permissionService;

    public TrainerPerformanceService(
        AppDbContext db,
        ITrainerPermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    public async Task<TrainerPerformanceResponse?> GetMyPerformanceAsync(
        Guid userId,
        CancellationToken cancellationToken,
        bool checkPermission = true)
    {
        var trainer = await _db.TrainerProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (trainer == null)
            return null;

        if (checkPermission)
        {
            var hasPerm = await _permissionService.HasPermissionAsync(trainer.Id, "TRAINER_VIEW_PERFORMANCE", cancellationToken);
            if (!hasPerm)
                throw new UnauthorizedAccessException("You do not have permission to view performance.");
        }

        var workshops = await _db.Workshops
            .Where(w => w.TrainerProfileId == trainer.Id)
            .ToListAsync(cancellationToken);

        var workshopIds = workshops.Select(w => w.Id).ToList();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = monthStart.AddMonths(1);
        var monthEnd = nextMonthStart.AddDays(-1);

        // Overall stats (Lifetime - never resets)
        var completedWorkshops = workshops
            .Count(w => w.Status == WorkshopStatus.Completed || (w.WorkshopDate < now && w.Status == WorkshopStatus.Approved));

        var totalWorkshops = workshops.Count;

        var bookings = await _db.WorkshopBookings
            .Where(b => workshopIds.Contains(b.WorkshopId) && (b.Status == WorkshopBookingStatus.Confirmed || b.Status == WorkshopBookingStatus.Attended))
            .ToListAsync(cancellationToken);

        var uniqueStudents = bookings.Select(b => b.StudentProfileId).Distinct().Count();
        var totalBookings = bookings.Count;

        var feedbacks = await _db.WorkshopFeedbacks
            .Where(f => workshopIds.Contains(f.WorkshopId) &&
                        f.IsValid &&
                        f.WorkshopBooking != null &&
                        f.WorkshopBooking.Status == WorkshopBookingStatus.Attended)
            .ToListAsync(cancellationToken);

        var feedbackCount = feedbacks.Count;
        decimal? avgRating = feedbackCount > 0 ? (decimal)Math.Round(feedbacks.Average(f => f.Rating), 2) : null;

        int totalCapacity = workshops.Where(w => w.Status == WorkshopStatus.Approved || w.Status == WorkshopStatus.Completed).Sum(w => w.Capacity);
        decimal attendancePercentage = totalCapacity > 0
            ? Math.Min(100m, Math.Round((decimal)totalBookings / totalCapacity * 100m, 2))
            : 0m;

        // Current Month stats (Date filtered - automatically starts fresh on the 1st)
        var monthlyWorkshops = workshops
            .Where(w => w.WorkshopDate >= monthStart && w.WorkshopDate < nextMonthStart)
            .ToList();
        var monthlyWorkshopIds = monthlyWorkshops.Select(w => w.Id).ToList();

        var monthlyCompletedWorkshops = monthlyWorkshops
            .Count(w => w.Status == WorkshopStatus.Completed || (w.WorkshopDate < now && w.Status == WorkshopStatus.Approved));

        var monthlyBookings = bookings
            .Where(b => monthlyWorkshopIds.Contains(b.WorkshopId) || (b.BookedAt >= monthStart && b.BookedAt < nextMonthStart))
            .ToList();

        var monthlyUniqueStudents = monthlyBookings.Select(b => b.StudentProfileId).Distinct().Count();

        var monthlyFeedbacks = feedbacks
            .Where(f => (f.SubmittedAt >= monthStart && f.SubmittedAt < nextMonthStart) || monthlyWorkshopIds.Contains(f.WorkshopId))
            .ToList();

        var monthlyFeedbackCount = monthlyFeedbacks.Count;
        decimal? monthlyAvgRating = monthlyFeedbackCount > 0 ? (decimal)Math.Round(monthlyFeedbacks.Average(f => f.Rating), 2) : null;

        int monthlyCapacity = monthlyWorkshops.Where(w => w.Status == WorkshopStatus.Approved || w.Status == WorkshopStatus.Completed).Sum(w => w.Capacity);
        decimal monthlyAttendancePercentage = monthlyCapacity > 0
            ? Math.Min(100m, Math.Round((decimal)monthlyBookings.Count / monthlyCapacity * 100m, 2))
            : 0m;

        var latestSnapshot = await _db.TrainerPerformanceSnapshots
            .Where(x => x.TrainerProfileId == trainer.Id)
            .OrderByDescending(x => x.SnapshotDate)
            .FirstOrDefaultAsync(cancellationToken);

        return new TrainerPerformanceResponse
        {
            Id = latestSnapshot?.Id ?? Guid.NewGuid(),
            SnapshotDate = DateTime.UtcNow.Date,
            AttendanceCount = totalBookings,
            Notes = latestSnapshot?.Notes ?? "Real-time calculated performance metrics based on workshop bookings and student feedback.",
            Overall = new PerformanceMetricResponse
            {
                Students = uniqueStudents,
                Workshops = totalWorkshops,
                Sessions = completedWorkshops,
                Feedback = feedbackCount,
                Rating = avgRating,
                AttendancePercentage = attendancePercentage
            },
            Monthly = new PerformancePeriodResponse
            {
                PeriodStart = monthStart,
                PeriodEnd = monthEnd,
                Students = monthlyUniqueStudents,
                Workshops = monthlyWorkshops.Count,
                Sessions = monthlyCompletedWorkshops,
                Feedback = monthlyFeedbackCount,
                Rating = monthlyAvgRating,
                AttendancePercentage = monthlyAttendancePercentage
            },
            RatingBreakdown = new TrainerRatingBreakdownResponse
            {
                FiveStars = feedbacks.Count(f => f.Rating == 5),
                FourStars = feedbacks.Count(f => f.Rating == 4),
                ThreeStars = feedbacks.Count(f => f.Rating == 3),
                TwoStars = feedbacks.Count(f => f.Rating == 2),
                OneStar = feedbacks.Count(f => f.Rating == 1)
            }
        };
    }

    public async Task<IReadOnlyList<TrainerPerformanceResponse>> GetMyPerformanceHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken,
        bool checkPermission = true)
    {
        var trainer = await _db.TrainerProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (trainer == null)
            return Array.Empty<TrainerPerformanceResponse>();

        if (checkPermission)
        {
            var hasPerm = await _permissionService.HasPermissionAsync(trainer.Id, "TRAINER_VIEW_PERFORMANCE", cancellationToken);
            if (!hasPerm)
                throw new UnauthorizedAccessException("You do not have permission to view performance.");
        }

        var current = await GetMyPerformanceAsync(userId, cancellationToken, checkPermission);

        var workshops = await _db.Workshops
            .Where(w => w.TrainerProfileId == trainer.Id)
            .ToListAsync(cancellationToken);

        var workshopIds = workshops.Select(w => w.Id).ToList();

        var bookings = await _db.WorkshopBookings
            .Where(b => workshopIds.Contains(b.WorkshopId) && (b.Status == WorkshopBookingStatus.Confirmed || b.Status == WorkshopBookingStatus.Attended))
            .ToListAsync(cancellationToken);

        var feedbacks = await _db.WorkshopFeedbacks
            .Where(f => workshopIds.Contains(f.WorkshopId))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var historyList = new List<TrainerPerformanceResponse>();

        // Build rolling monthly performance cycles for the past 6 months
        for (int i = 0; i < 6; i++)
        {
            var pStart = monthStart.AddMonths(-i);
            var pEnd = pStart.AddMonths(1);

            var mWorkshops = workshops
                .Where(w => w.WorkshopDate >= pStart && w.WorkshopDate < pEnd)
                .ToList();
            var mWorkshopIds = mWorkshops.Select(w => w.Id).ToList();

            var mFeedbacks = feedbacks
                .Where(f => (f.SubmittedAt >= pStart && f.SubmittedAt < pEnd) || mWorkshopIds.Contains(f.WorkshopId))
                .ToList();

            var mBookings = bookings
                .Where(b => mWorkshopIds.Contains(b.WorkshopId) || (b.BookedAt >= pStart && b.BookedAt < pEnd))
                .ToList();

            var mCompleted = mWorkshops
                .Count(w => w.Status == WorkshopStatus.Completed || (w.WorkshopDate < now && w.Status == WorkshopStatus.Approved));

            int mCap = mWorkshops.Where(w => w.Status == WorkshopStatus.Approved || w.Status == WorkshopStatus.Completed).Sum(w => w.Capacity);
            decimal mAtt = mCap > 0
                ? Math.Min(100m, Math.Round((decimal)mBookings.Count / mCap * 100m, 2))
                : 0m;

            decimal? mRating = mFeedbacks.Count > 0
                ? (decimal)Math.Round(mFeedbacks.Average(f => f.Rating), 2)
                : null;

            if (i == 0 || mWorkshops.Count > 0 || mFeedbacks.Count > 0 || mBookings.Count > 0)
            {
                var monthDisplay = pStart.ToString("MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
                historyList.Add(new TrainerPerformanceResponse
                {
                    Id = Guid.NewGuid(),
                    SnapshotDate = pStart,
                    AttendanceCount = mBookings.Count,
                    Notes = $"{monthDisplay} Performance Cycle",
                    Overall = current?.Overall ?? new PerformanceMetricResponse(),
                    Monthly = new PerformancePeriodResponse
                    {
                        PeriodStart = pStart,
                        PeriodEnd = pEnd.AddDays(-1),
                        Students = mBookings.Select(b => b.StudentProfileId).Distinct().Count(),
                        Workshops = mWorkshops.Count,
                        Sessions = mCompleted,
                        Feedback = mFeedbacks.Count,
                        Rating = mRating,
                        AttendancePercentage = mAtt
                    }
                });
            }
        }

        // Merge any stored snapshots from the database if they exist
        var storedSnapshots = await _db.TrainerPerformanceSnapshots
            .Where(x => x.TrainerProfile.UserId == userId)
            .OrderByDescending(x => x.SnapshotDate)
            .ToListAsync(cancellationToken);

        foreach (var s in storedSnapshots)
        {
            if (!historyList.Any(h => h.SnapshotDate.Year == s.SnapshotDate.Year && h.SnapshotDate.Month == s.SnapshotDate.Month))
            {
                historyList.Add(new TrainerPerformanceResponse
                {
                    Id = s.Id,
                    SnapshotDate = s.SnapshotDate,
                    AttendanceCount = s.AttendanceCount,
                    Notes = s.Notes,
                    Overall = current?.Overall ?? new PerformanceMetricResponse(),
                    Monthly = new PerformancePeriodResponse
                    {
                        PeriodStart = s.SnapshotDate,
                        PeriodEnd = s.SnapshotDate.AddMonths(1).AddDays(-1),
                        Students = s.UniqueStudents,
                        Workshops = s.WorkshopsConducted,
                        Sessions = s.SessionsConducted,
                        Feedback = s.FeedbackCount,
                        Rating = s.AverageFeedbackRating,
                        AttendancePercentage = s.AttendancePercentage
                    }
                });
            }
        }

        return historyList.OrderByDescending(h => h.SnapshotDate).ToList();
    }
}
