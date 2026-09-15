using Ethos.Api.Application.Storage;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Trainers;

public class TrainerService : ITrainerService
{
    private readonly AppDbContext _db;
    private readonly ITrainerProfilePhotoStorageService _profilePhotoStorage;
    private readonly ITrainerPermissionService _permissionService;

    public TrainerService(
        AppDbContext db,
        ITrainerProfilePhotoStorageService profilePhotoStorage,
        ITrainerPermissionService permissionService)
    {
        _db = db;
        _profilePhotoStorage = profilePhotoStorage;
        _permissionService = permissionService;
    }

    public async Task<TrainerResponse?> GetMeAsync(
        Guid userId,
        CancellationToken ct)
    {
        var trainer = await _db.TrainerProfiles
            .AsNoTracking()
            .Include(x => x.CurrentTier)
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (trainer == null)
            return null;

        return Map(trainer);
    }

    public async Task<TrainerResponse?> UpdateMeAsync(
        Guid userId,
        UpdateTrainerRequest request,
        CancellationToken ct)
    {
        var trainer = await _db.TrainerProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (trainer == null)
            return null;

        trainer.FullName = request.FullName.Trim();
        trainer.City = request.City?.Trim();
        trainer.ProfilePhotoUrl = request.ProfilePhotoUrl?.Trim();
        trainer.PrimaryDanceStyle = request.PrimaryDanceStyle?.Trim();
        trainer.SecondaryDanceStyles = request.SecondaryDanceStyles?.Trim();
        trainer.ExperienceYears = request.ExperienceYears;
        trainer.CurrentStudio = request.CurrentStudio?.Trim();
        trainer.Bio = request.Bio?.Trim();
        trainer.InstagramUrl = request.InstagramUrl?.Trim();
        trainer.YouTubeUrl = request.YouTubeUrl?.Trim();
        trainer.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetMeAsync(userId, ct);
    }

    public async Task<TrainerResponse?> UploadProfilePhotoAsync(
        Guid userId,
        IFormFile file,
        CancellationToken ct)
    {
        var trainer = await _db.TrainerProfiles
            .FirstOrDefaultAsync(
                x => x.UserId == userId,
                ct);

        if (trainer == null)
        {
            return null;
        }

        var oldPhotoPath = trainer.ProfilePhotoUrl;

        var newPhotoPath =
            await _profilePhotoStorage.SaveAsync(
                userId,
                file,
                ct);

        trainer.ProfilePhotoUrl =
            $"/uploads/trainer-profile-photos/{userId}/{Path.GetFileName(newPhotoPath)}";

        trainer.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(oldPhotoPath) && oldPhotoPath.StartsWith("/uploads/trainer-profile-photos/"))
        {
            try
            {
                var relativeStoragePath = oldPhotoPath.Replace("/uploads/trainer-profile-photos/", "App_Data/uploads/trainer-profile-photos/");
                await _profilePhotoStorage.DeleteAsync(
                    relativeStoragePath,
                    ct);
            }
            catch
            {
                // Do not fail a successful profile update because an old image could not be removed.
            }
        }

        return await GetMeAsync(userId, ct);
    }

    public async Task<TrainerDashboardResponse?> GetDashboardAsync(
        Guid userId,
        CancellationToken ct)
    {
        var trainer = await _db.TrainerProfiles
            .AsNoTracking()
            .Include(x => x.CurrentTier)
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (trainer == null)
            return null;

        var workshops = await _db.Workshops
            .AsNoTracking()
            .Include(w => w.TrainerProfile)
            .Include(w => w.Bookings)
            .Where(w => w.TrainerProfileId == trainer.Id)
            .ToListAsync(ct);

        var upcomingWorkshops = workshops
            .Where(w => w.WorkshopDate >= DateTime.UtcNow && w.Status == Domain.Enums.WorkshopStatus.Approved)
            .OrderBy(w => w.WorkshopDate)
            .Take(3)
            .Select(w => new TrainerWorkshopResponse
            {
                Id = w.Id,
                TrainerProfileId = w.TrainerProfileId ?? trainer.Id,
                TrainerName = w.TrainerProfile?.FullName ?? trainer.FullName,
                Title = w.Title,
                Description = w.Description,
                DanceStyle = w.DanceStyle,
                Level = w.Level,
                WorkshopDate = w.WorkshopDate,
                StartTime = w.StartTime,
                EndTime = w.EndTime,
                Venue = w.Venue,
                TrainerProposedPrice = w.TrainerProposedPrice,
                AdminApprovedPrice = w.AdminApprovedPrice,
                Price = w.AdminApprovedPrice ?? w.TrainerProposedPrice ?? w.Price,
                Capacity = w.Capacity,
                BookedCount = w.Bookings?.Count(b => b.Status == Domain.Enums.WorkshopBookingStatus.Confirmed || b.Status == Domain.Enums.WorkshopBookingStatus.Attended) ?? 0,
                Status = w.Status.ToString(),
                ImageUrl = w.ImageUrl,
                CreatedAt = w.CreatedAt
            })
            .ToList();

        var upcomingCount = workshops.Count(w =>
            w.WorkshopDate >= DateTime.UtcNow &&
            w.Status == Domain.Enums.WorkshopStatus.Approved);

        var workshopIds = workshops
            .Select(w => w.Id)
            .ToList();

        var bookings = await _db.WorkshopBookings
            .AsNoTracking()
            .Where(b =>
                workshopIds.Contains(b.WorkshopId) &&
                (b.Status == Domain.Enums.WorkshopBookingStatus.Confirmed ||
                 b.Status == Domain.Enums.WorkshopBookingStatus.Attended))
            .ToListAsync(ct);

        var studentCount = bookings
            .Select(b => b.StudentProfileId)
            .Distinct()
            .Count();

        var feedbacks = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Where(f => workshopIds.Contains(f.WorkshopId) &&
                        f.IsValid &&
                        f.WorkshopBooking != null &&
                        f.WorkshopBooking.Status == Domain.Enums.WorkshopBookingStatus.Attended)
            .ToListAsync(ct);

        decimal? avgRating =
            feedbacks.Count > 0
                ? (decimal)Math.Round(feedbacks.Average(f => f.Rating), 2)
                : null;

        var totalApprovedCapacity = workshops
            .Where(w => w.Status == Domain.Enums.WorkshopStatus.Approved || w.Status == Domain.Enums.WorkshopStatus.Completed)
            .Sum(w => w.Capacity);

        decimal attendancePercentage = totalApprovedCapacity > 0
            ? Math.Min(100m, Math.Round((decimal)bookings.Count / totalApprovedCapacity * 100m, 2))
            : 0m;

        var upcomingClassCount = await _db.ClassSessions
            .AsNoTracking()
            .CountAsync(cs => cs.SessionDate >= DateTime.UtcNow.Date && cs.Status == Domain.Enums.ClassSessionStatus.Scheduled, ct);

        var pendingUpgradeRequests = await _db.TrainerUpgradeRequests
            .AsNoTracking()
            .CountAsync(r => r.TrainerProfileId == trainer.Id && r.Status == Domain.Enums.TrainerUpgradeRequestStatus.Pending, ct);

        var unreadNotificationCount = await _db.NotificationRecipients
            .AsNoTracking()
            .CountAsync(nr => nr.UserId == userId && !nr.IsRead && nr.DeletedAt == null, ct);

        return new TrainerDashboardResponse
        {
            Trainer = Map(trainer),

            StudentCount = studentCount,

            UpcomingClassCount = upcomingClassCount,

            UpcomingWorkshopCount = upcomingCount,

            TotalWorkshops = workshops.Count,

            AttendancePercentage = attendancePercentage,

            FeedbackCount = feedbacks.Count,

            AverageFeedbackRating = avgRating,

            PendingUpgradeRequests = pendingUpgradeRequests,

            UnreadNotificationCount = unreadNotificationCount,

            UpcomingWorkshops = upcomingWorkshops
        };
    }

    public async Task<List<TrainerTierResponse>> GetTiersAsync(
        CancellationToken ct)
    {
        return await _db.TrainerTiers
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new TrainerTierResponse
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                Description = x.Description,
                DisplayOrder = x.DisplayOrder,
                ApplicationFee = x.ApplicationFee,
                UpgradeFee = x.UpgradeFee,
                IsActive = x.IsActive
            })
            .ToListAsync(ct);
    }

    public async Task<List<TrainerTierHistoryResponse>> GetTierHistoryAsync(
        Guid userId,
        CancellationToken ct)
    {
        var trainerId = await _db.TrainerProfiles
            .Where(x => x.UserId == userId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (trainerId == null)
            return new List<TrainerTierHistoryResponse>();

        return await _db.TrainerTierHistories
            .AsNoTracking()
            .Include(x => x.PreviousTier)
            .Include(x => x.NewTier)
            .Where(x => x.TrainerProfileId == trainerId.Value)
            .OrderByDescending(x => x.ChangedAt)
            .Select(x => new TrainerTierHistoryResponse
            {
                Id = x.Id,
                TierId = x.NewTierId,
                TierName = x.NewTier.Name,
                TierCode = x.NewTier.Code,
                PreviousTier = x.PreviousTier != null ? x.PreviousTier.Name : null,
                NewTier = x.NewTier.Name,
                AssignedAt = x.ChangedAt,
                ChangedAt = x.ChangedAt,
                Reason = x.Reason
            })
            .ToListAsync(ct);
    }

    public async Task<List<TrainerWorkshopResponse>> GetMyWorkshopsAsync(
        Guid userId,
        CancellationToken ct)
    {
        var trainerId = await _db.TrainerProfiles
            .Where(x => x.UserId == userId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (trainerId == null)
            return new List<TrainerWorkshopResponse>();

        var workshops = await _db.Workshops
            .AsNoTracking()
            .Where(x => x.TrainerProfileId == trainerId.Value)
            .OrderByDescending(x => x.WorkshopDate)
            .ToListAsync(ct);

        var workshopIds = workshops
            .Select(x => x.Id)
            .ToList();

        var bookingCounts = await _db.WorkshopBookings
            .AsNoTracking()
            .Where(x =>
                workshopIds.Contains(x.WorkshopId) &&
                x.Status == Domain.Enums.WorkshopBookingStatus.Confirmed)
            .GroupBy(x => x.WorkshopId)
            .Select(x => new
            {
                WorkshopId = x.Key,
                Count = x.Count()
            })
            .ToDictionaryAsync(x => x.WorkshopId, x => x.Count, ct);

        return workshops.Select(workshop => new TrainerWorkshopResponse
        {
            Id = workshop.Id,
            Title = workshop.Title,
            Description = workshop.Description,
            DanceStyle = workshop.DanceStyle,
            Level = workshop.Level,
            WorkshopDate = workshop.WorkshopDate,
            StartTime = workshop.StartTime,
            EndTime = workshop.EndTime,
            Venue = workshop.Venue,
            Capacity = workshop.Capacity,
            Price = workshop.Price,
            Status = workshop.Status.ToString(),
            EnrolledStudentsCount =
                bookingCounts.TryGetValue(workshop.Id, out var count)
                    ? count
                    : 0
        }).ToList();
    }

    public async Task<TrainerWorkshopResponse?> GetWorkshopAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken ct)
    {
        var trainerId = await _db.TrainerProfiles
            .Where(x => x.UserId == userId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (trainerId == null)
            return null;

        var workshop = await _db.Workshops
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.Id == workshopId &&
                    x.TrainerProfileId == trainerId.Value,
                ct);

        if (workshop == null)
            return null;

        var enrolledCount = await _db.WorkshopBookings
            .CountAsync(
                x =>
                    x.WorkshopId == workshopId &&
                    x.Status ==
                    Domain.Enums.WorkshopBookingStatus.Confirmed,
                ct);

        return new TrainerWorkshopResponse
        {
            Id = workshop.Id,
            Title = workshop.Title,
            Description = workshop.Description,
            DanceStyle = workshop.DanceStyle,
            Level = workshop.Level,
            WorkshopDate = workshop.WorkshopDate,
            StartTime = workshop.StartTime,
            EndTime = workshop.EndTime,
            Venue = workshop.Venue,
            Capacity = workshop.Capacity,
            Price = workshop.Price,
            Status = workshop.Status.ToString(),
            EnrolledStudentsCount = enrolledCount
        };
    }

    public async Task<List<TrainerWorkshopStudentResponse>>
        GetWorkshopStudentsAsync(
            Guid userId,
            Guid workshopId,
            CancellationToken ct)
    {
        var trainerOwnsWorkshop = await _db.Workshops
            .AnyAsync(
                x =>
                    x.Id == workshopId &&
                    x.TrainerProfile.UserId == userId,
                ct);

        if (!trainerOwnsWorkshop)
            return new List<TrainerWorkshopStudentResponse>();

        return new List<TrainerWorkshopStudentResponse>();
    }

    public async Task<List<TrainerWorkshopFeedbackResponse>>
        GetWorkshopFeedbackAsync(
            Guid userId,
            Guid workshopId,
            CancellationToken ct)
    {
        var trainerOwnsWorkshop = await _db.Workshops
            .AnyAsync(
                x =>
                    x.Id == workshopId &&
                    x.TrainerProfile.UserId == userId,
                ct);

        if (!trainerOwnsWorkshop)
            return new List<TrainerWorkshopFeedbackResponse>();

        var feedback = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Where(x => x.WorkshopId == workshopId)
            .OrderByDescending(x => x.SubmittedAt)
            .ToListAsync(ct);

        return feedback.Select(x => new TrainerWorkshopFeedbackResponse
        {
            Id = x.Id,
            StudentName = "Student",
            Rating = x.Rating,
            Comment = null,
            SubmittedAt = x.SubmittedAt
        }).ToList();
    }

    public async Task<TrainerPerformanceResponse?> GetPerformanceAsync(
        Guid userId,
        CancellationToken ct)
    {
        var trainer = await _db.TrainerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId,
                ct);

        if (trainer == null)
            return null;

        var workshops = await _db.Workshops
            .AsNoTracking()
            .Where(x => x.TrainerProfileId == trainer.Id)
            .ToListAsync(ct);

        var workshopIds = workshops
            .Select(x => x.Id)
            .ToList();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = monthStart.AddMonths(1);

        if (workshopIds.Count == 0)
        {
            return new TrainerPerformanceResponse
            {
                SnapshotDate = DateTime.UtcNow.Date,
                Overall = new PerformanceMetricResponse(),
                Monthly = new PerformancePeriodResponse
                {
                    PeriodStart = monthStart,
                    PeriodEnd = nextMonthStart.AddDays(-1)
                }
            };
        }

        var completedWorkshops = workshops.Count(
            x => x.Status == Domain.Enums.WorkshopStatus.Completed || (x.WorkshopDate < now && x.Status == Domain.Enums.WorkshopStatus.Approved));

        var bookings = await _db.WorkshopBookings
            .AsNoTracking()
            .Where(x =>
                workshopIds.Contains(x.WorkshopId) &&
                x.Status == Domain.Enums.WorkshopBookingStatus.Confirmed)
            .ToListAsync(ct);

        var uniqueStudents = bookings
            .Select(x => x.StudentProfileId)
            .Distinct()
            .Count();

        var feedback = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Where(x => workshopIds.Contains(x.WorkshopId) &&
                        x.IsValid &&
                        x.WorkshopBooking != null &&
                        x.WorkshopBooking.Status == Domain.Enums.WorkshopBookingStatus.Attended)
            .ToListAsync(ct);

        var overallRating = feedback.Count == 0
            ? (decimal?)null
            : Math.Round(
                (decimal)feedback.Average(x => x.Rating),
                2);

        int totalCap = workshops.Sum(x => x.Capacity);
        var attendanceRate = totalCap > 0 ? Math.Min(100m, Math.Round((decimal)bookings.Count / totalCap * 100m, 2)) : 0m;

        // Current Month
        var monthlyWorkshops = workshops
            .Where(x => x.WorkshopDate >= monthStart && x.WorkshopDate < nextMonthStart)
            .ToList();
        var monthlyWorkshopIds = monthlyWorkshops.Select(x => x.Id).ToList();

        var monthlyBookings = bookings
            .Where(x => monthlyWorkshopIds.Contains(x.WorkshopId))
            .ToList();

        var monthlyFeedbacks = feedback
            .Where(x => (x.SubmittedAt >= monthStart && x.SubmittedAt < nextMonthStart) || monthlyWorkshopIds.Contains(x.WorkshopId))
            .ToList();

        decimal? monthlyRating = monthlyFeedbacks.Count > 0
            ? Math.Round((decimal)monthlyFeedbacks.Average(x => x.Rating), 2)
            : null;

        int monthlyCap = monthlyWorkshops.Sum(x => x.Capacity);
        decimal monthlyAtt = monthlyCap > 0 ? Math.Min(100m, Math.Round((decimal)monthlyBookings.Count / monthlyCap * 100m, 2)) : 0m;

        return new TrainerPerformanceResponse
        {
            SnapshotDate = DateTime.UtcNow.Date,
            AttendanceCount = bookings.Count,
            Overall = new PerformanceMetricResponse
            {
                Students = uniqueStudents,
                Workshops = workshops.Count,
                Sessions = completedWorkshops,
                Feedback = feedback.Count,
                Rating = overallRating,
                AttendancePercentage = attendanceRate
            },
            Monthly = new PerformancePeriodResponse
            {
                PeriodStart = monthStart,
                PeriodEnd = nextMonthStart.AddDays(-1),
                Students = monthlyBookings.Select(x => x.StudentProfileId).Distinct().Count(),
                Workshops = monthlyWorkshops.Count,
                Sessions = monthlyWorkshops.Count(x => x.WorkshopDate < now),
                Feedback = monthlyFeedbacks.Count,
                Rating = monthlyRating,
                AttendancePercentage = monthlyAtt
            }
        };
    }

    public async Task<TrainerUpgradeRequestResponse?> RequestUpgradeAsync(
        Guid userId,
        Guid requestedTierId,
        CancellationToken ct)
    {
        var trainer = await _db.TrainerProfiles
            .Include(x => x.CurrentTier)
            .FirstOrDefaultAsync(
                x => x.UserId == userId,
                ct);

        if (trainer == null)
            return null;

        var hasPerm = await _permissionService.HasPermissionAsync(trainer.Id, "TRAINER_REQUEST_TIER_UPGRADE", ct);
        if (!hasPerm)
        {
            throw new UnauthorizedAccessException("You do not have permission to request a tier upgrade.");
        }

        var requestedTier = await _db.TrainerTiers
            .FirstOrDefaultAsync(
                x =>
                    x.Id == requestedTierId &&
                    x.IsActive,
                ct);

        if (trainer.CurrentTier != null &&
            (string.Equals(trainer.CurrentTier.Code, "PLATINUM", StringComparison.OrdinalIgnoreCase) ||
             !await _db.TrainerTiers.AnyAsync(t => t.DisplayOrder > trainer.CurrentTier.DisplayOrder && t.IsActive, ct)))
        {
            throw new InvalidOperationException(
                "You have reached the highest Ethos trainer tier. No further pathway upgrades are available.");
        }

        if (requestedTier == null)
            throw new InvalidOperationException(
                "Requested trainer tier was not found.");

        if (trainer.CurrentTierId == requestedTier.Id)
            throw new InvalidOperationException(
                "You are already assigned to this tier.");

        var nextTier = await _db.TrainerTiers
            .Where(t => t.IsActive && t.DisplayOrder > (trainer.CurrentTier != null ? trainer.CurrentTier.DisplayOrder : 0))
            .OrderBy(t => t.DisplayOrder)
            .FirstOrDefaultAsync(ct);

        if (nextTier == null || requestedTier.Id != nextTier.Id)
        {
            throw new InvalidOperationException(
                $"You can only request an upgrade to the next immediate tier ({nextTier?.Name ?? "none"}).");
        }

        var existing = await _db.TrainerUpgradeRequests
            .FirstOrDefaultAsync(
                x =>
                    x.TrainerProfileId == trainer.Id &&
                    (x.Status == Domain.Enums.TrainerUpgradeRequestStatus.Pending ||
                     x.Status == Domain.Enums.TrainerUpgradeRequestStatus.PaymentPending ||
                     x.Status == Domain.Enums.TrainerUpgradeRequestStatus.PaymentVerified),
                ct);

        if (existing != null)
            throw new InvalidOperationException(
                "You already have an active upgrade request pending review.");

        var request = new Domain.Entities.TrainerUpgradeRequest
        {
            Id = Guid.NewGuid(),
            TrainerProfileId = trainer.Id,
            CurrentTierId = trainer.CurrentTierId ?? (trainer.CurrentTier != null ? trainer.CurrentTier.Id : Guid.Empty),
            RequestedTierId = requestedTier.Id,
            Status = Domain.Enums.TrainerUpgradeRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.TrainerUpgradeRequests.Add(request);

        await _db.SaveChangesAsync(ct);

        return new TrainerUpgradeRequestResponse
        {
            Id = request.Id,
            CurrentTier = trainer.CurrentTier?.Name,
            RequestedTier = requestedTier.Name,
            RequestedTierId = requestedTier.Id,
            RequestedTierName = requestedTier.Name,
            Status = request.Status.ToString(),
            AdminNotes = request.AdminNotes,
            CreatedAt = request.CreatedAt,
            ReviewedAt = request.ReviewedAt,
            RequestedAt = request.CreatedAt,
            ProcessedAt = request.ReviewedAt
        };
    }

    public async Task<List<TrainerUpgradeRequestResponse>>
        GetUpgradeRequestsAsync(
            Guid userId,
            CancellationToken ct)
    {
        var trainerId = await _db.TrainerProfiles
            .Where(x => x.UserId == userId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (trainerId == null)
            return new List<TrainerUpgradeRequestResponse>();

        return await _db.TrainerUpgradeRequests
            .AsNoTracking()
            .Include(x => x.CurrentTier)
            .Include(x => x.RequestedTier)
            .Where(x => x.TrainerProfileId == trainerId.Value)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new TrainerUpgradeRequestResponse
            {
                Id = x.Id,
                CurrentTier = x.CurrentTier != null ? x.CurrentTier.Name : null,
                RequestedTier = x.RequestedTier.Name,
                RequestedTierId = x.RequestedTierId,
                RequestedTierName = x.RequestedTier.Name,
                Status = x.Status.ToString(),
                AdminNotes = x.AdminNotes,
                CreatedAt = x.CreatedAt,
                ReviewedAt = x.ReviewedAt,
                RequestedAt = x.CreatedAt,
                ProcessedAt = x.ReviewedAt
            })
            .ToListAsync(ct);
    }

    private static TrainerResponse Map(
        Domain.Entities.TrainerProfile trainer)
    {
        return new TrainerResponse
        {
            Id = trainer.Id,
            TrainerCode = trainer.TrainerCode,
            FullName = trainer.FullName,
            City = trainer.City,
            ProfilePhotoUrl = trainer.ProfilePhotoUrl,
            PrimaryDanceStyle = trainer.PrimaryDanceStyle,
            ExperienceYears = trainer.ExperienceYears,
            Bio = trainer.Bio,
            Status = trainer.Status.ToString(),
            Tier = trainer.CurrentTier?.Name
        };
    }
}
