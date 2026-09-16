using Ethos.Api.Application.Storage;
using Ethos.Api.Application.Trainers;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminTrainerService : IAdminTrainerService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;
    private readonly IAdminTrainerPermissionService _permissionService;
    private readonly ITrainerPerformanceService _performanceService;
    private readonly IFileStorageService _fileStorageService;

    public AdminTrainerService(
        AppDbContext db,
        IAdminAuditService auditService,
        IAdminTrainerPermissionService permissionService,
        ITrainerPerformanceService performanceService,
        IFileStorageService fileStorageService)
    {
        _db = db;
        _auditService = auditService;
        _permissionService = permissionService;
        _performanceService = performanceService;
        _fileStorageService = fileStorageService;
    }

    public async Task<IReadOnlyList<TrainerResponse>> GetAllTrainersAsync(
        CancellationToken cancellationToken)
    {
        return await _db.TrainerProfiles
            .AsNoTracking()
            .Include(x => x.CurrentTier)
            .OrderBy(x => x.FullName)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<AdminTrainerListResponse>> GetTrainersAsync(
        int page,
        int pageSize,
        string? search,
        Guid? tierId,
        string? status,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.TrainerProfiles
            .AsNoTracking()
            .Include(t => t.CurrentTier)
            .Include(t => t.User)
            .Where(t => t.User.IsActive && t.User.UserRoles.Any(ur => ur.Role.Code == "TRAINER"))
            .AsQueryable();

        if (tierId.HasValue)
            query = query.Where(t => t.CurrentTierId == tierId.Value);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<TrainerStatus>(status.Trim(), true, out var statusEnum))
            {
                query = query.Where(t => t.Status == statusEnum);
            }
        }
        else
        {
            // By default, show active/approved trainers in the directory
            query = query.Where(t => t.Status != TrainerStatus.Pending);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(t =>
                t.FullName.ToLower().Contains(s) ||
                t.TrainerCode.ToLower().Contains(s) ||
                (t.City != null && t.City.ToLower().Contains(s)) ||
                (t.PrimaryDanceStyle != null && t.PrimaryDanceStyle.ToLower().Contains(s)) ||
                (t.User.Email != null && t.User.Email.ToLower().Contains(s)) ||
                t.User.Phone.Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var trainers = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = trainers.Select(t => new AdminTrainerListResponse
        {
            TrainerId = t.Id,
            UserId = t.UserId,
            TrainerCode = t.TrainerCode,
            FullName = t.FullName,
            Phone = t.User?.Phone ?? "",
            Email = t.User?.Email,
            City = t.City,
            Status = t.Status.ToString(),
            TierName = t.CurrentTier?.Name,
            ProfilePhotoUrl = t.ProfilePhotoUrl,
            PrimaryDanceStyle = t.PrimaryDanceStyle,
            SecondaryDanceStyles = t.SecondaryDanceStyles,
            ApprovedAt = t.ApprovedAt,
            CreatedAt = t.CreatedAt
        }).ToList();

        return new PagedResult<AdminTrainerListResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<AdminTrainerSummaryStatsResponse> GetTrainerStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var trainerProfiles = await _db.TrainerProfiles
            .AsNoTracking()
            .Include(t => t.User)
            .Where(t => t.User.IsActive && t.User.UserRoles.Any(ur => ur.Role.Code == "TRAINER"))
            .Select(t => new
            {
                t.Status,
                UserIsActive = t.User.IsActive
            })
            .ToListAsync(cancellationToken);

        var totalTrainers = trainerProfiles.Count;
        var activeTrainers = trainerProfiles.Count(t => t.Status == TrainerStatus.Active && t.UserIsActive);

        var pendingApps = await _db.TrainerApplications
            .AsNoTracking()
            .CountAsync(a => a.Status == TrainerApplicationStatus.Submitted
                          || a.Status == TrainerApplicationStatus.PaymentPending
                          || a.Status == TrainerApplicationStatus.PaymentVerified
                          || a.Status == TrainerApplicationStatus.UnderReview
                          || a.Status == TrainerApplicationStatus.ChangesRequested,
                        cancellationToken);

        var upgradeRequests = await _db.TrainerUpgradeRequests
            .AsNoTracking()
            .CountAsync(u => u.Status == TrainerUpgradeRequestStatus.Pending
                          || u.Status == TrainerUpgradeRequestStatus.PaymentPending
                          || u.Status == TrainerUpgradeRequestStatus.PaymentVerified,
                        cancellationToken);

        return new AdminTrainerSummaryStatsResponse
        {
            TotalTrainers = totalTrainers,
            ActiveTrainers = activeTrainers,
            PendingApplications = pendingApps,
            UpgradeRequests = upgradeRequests
        };
    }

    public async Task<TrainerResponse?> GetTrainerByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles
            .AsNoTracking()
            .Include(x => x.CurrentTier)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return trainer == null ? null : Map(trainer);
    }

    public async Task<AdminTrainerDetailsResponse?> GetTrainerDetailsByIdAsync(
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        var t = await _db.TrainerProfiles
            .AsNoTracking()
            .Include(x => x.CurrentTier)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == trainerId || x.UserId == trainerId, cancellationToken);

        if (t == null) return null;

        // Latest Application
        var latestApp = await _db.TrainerApplications
            .AsNoTracking()
            .Where(a => a.TrainerProfileId == t.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new TrainerApplicationResponse
            {
                Id = a.Id,
                TrainerProfileId = a.TrainerProfileId,
                Tier = t.CurrentTier != null ? t.CurrentTier.Name : null,
                Status = a.Status.ToString(),
                ApplicationNotes = a.ApplicationNotes,
                AdminNotes = a.AdminNotes,
                RejectionReason = a.RejectionReason,
                PaymentTransactionId = a.PaymentTransactionId,
                SubmittedAt = a.SubmittedAt,
                PaymentVerifiedAt = a.PaymentVerifiedAt,
                ReviewedAt = a.ReviewedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Tier History
        var tierHistory = await _db.TrainerTierHistories
            .AsNoTracking()
            .Include(th => th.PreviousTier)
            .Include(th => th.NewTier)
            .Where(th => th.TrainerProfileId == t.Id)
            .OrderByDescending(th => th.ChangedAt)
            .Select(th => new TrainerTierHistoryResponse
            {
                Id = th.Id,
                PreviousTier = th.PreviousTier != null ? th.PreviousTier.Name : null,
                NewTier = th.NewTier.Name,
                Reason = th.Reason,
                ChangedAt = th.ChangedAt
            })
            .ToListAsync(cancellationToken);

        // Permissions Matrix
        var permissions = await _permissionService.GetTrainerPermissionsAsync(t.Id, cancellationToken);

        // Workshops
        var workshops = await _db.Workshops
            .AsNoTracking()
            .Include(w => w.Bookings)
            .Where(w => w.TrainerProfileId == t.Id)
            .OrderByDescending(w => w.WorkshopDate)
            .Select(w => new TrainerWorkshopResponse
            {
                Id = w.Id,
                TrainerProfileId = w.TrainerProfileId ?? Guid.Empty,
                TrainerName = t.FullName,
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
                BookedCount = w.Bookings.Count(b => b.Status == WorkshopBookingStatus.Confirmed),
                Status = w.Status.ToString(),
                ImageUrl = w.ImageUrl,
                CreatedAt = w.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Performance
        TrainerPerformanceResponse? perf = null;
        try
        {
            perf = await _performanceService.GetMyPerformanceAsync(t.UserId, cancellationToken, checkPermission: false);
        }
        catch
        {
            // Performance snapshot fallback
        }

        // Feedback
        var feedbackList = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Include(f => f.Workshop)
            .Include(f => f.StudentProfile)
                .ThenInclude(s => s.User)
            .Where(f => f.Workshop.TrainerProfileId == t.Id)
            .OrderByDescending(f => f.SubmittedAt)
            .Select(f => new TrainerWorkshopFeedbackResponse
            {
                Id = f.Id,
                StudentName = f.StudentProfile != null && f.StudentProfile.User != null
                    ? f.StudentProfile.User.FullName
                    : "Guest Attendee",
                WorkshopTitle = f.Workshop.Title,
                WorkshopDate = f.Workshop.WorkshopDate,
                BookingReference = f.WorkshopBookingId.HasValue ? f.WorkshopBookingId.Value.ToString().Substring(0, 8).ToUpper() : "DIRECT",
                Rating = f.Rating,
                Comment = f.Comment,
                CreatedAt = f.SubmittedAt
            })
            .ToListAsync(cancellationToken);

        return new AdminTrainerDetailsResponse
        {
            TrainerId = t.Id,
            UserId = t.UserId,
            TrainerCode = t.TrainerCode,
            FullName = t.FullName,
            Phone = t.User?.Phone ?? "",
            City = t.City,
            ProfilePhotoUrl = t.ProfilePhotoUrl,
            PrimaryDanceStyle = t.PrimaryDanceStyle,
            SecondaryDanceStyles = t.SecondaryDanceStyles,
            ExperienceYears = t.ExperienceYears,
            CurrentStudio = t.CurrentStudio,
            Bio = t.Bio,
            InstagramUrl = t.InstagramUrl,
            YouTubeUrl = t.YouTubeUrl,
            Status = t.Status.ToString(),
            TierName = t.CurrentTier?.Name,
            ApprovedAt = t.ApprovedAt,
            LatestApplication = latestApp,
            TierHistory = tierHistory,
            Permissions = permissions.ToList(),
            Workshops = workshops,
            Performance = perf,
            Feedback = feedbackList
        };
    }

    public async Task UpdateTrainerTierAsync(
        Guid trainerId,
        Guid adminUserId,
        AdminUpdateTrainerTierRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("A reason is mandatory for direct administrative tier override.");
        }

        var trainer = await _db.TrainerProfiles
            .Include(x => x.CurrentTier)
            .FirstOrDefaultAsync(x => x.Id == trainerId, cancellationToken);

        if (trainer == null)
            throw new ArgumentException("Trainer profile not found.");

        var newTier = await _db.TrainerTiers
            .FirstOrDefaultAsync(x => x.Id == request.TierId && x.IsActive, cancellationToken);

        if (newTier == null)
            throw new ArgumentException("Specified tier does not exist or is inactive.");

        var previousTierId = trainer.CurrentTierId;

        trainer.CurrentTierId = newTier.Id;
        trainer.UpdatedAt = DateTime.UtcNow;

        _db.TrainerTierHistories.Add(new TrainerTierHistory
        {
            Id = Guid.NewGuid(),
            TrainerProfileId = trainer.Id,
            PreviousTierId = previousTierId,
            NewTierId = newTier.Id,
            Reason = $"[ADMIN OVERRIDE] {request.Reason.Trim()}",
            ChangedByUserId = adminUserId,
            ChangedAt = DateTime.UtcNow
        });

        _auditService.AddAuditLog(
            adminUserId,
            "ADMIN_TIER_OVERRIDE",
            "TrainerProfile",
            trainer.Id,
            $"[ADMIN OVERRIDE] Tier changed to {newTier.Name}. Reason: {request.Reason.Trim()}");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateTrainerStatusAsync(
        Guid trainerId,
        Guid adminUserId,
        TrainerStatus newStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles
            .FirstOrDefaultAsync(x => x.Id == trainerId, cancellationToken);

        if (trainer == null)
            throw new ArgumentException("Trainer profile not found.");

        if (trainer.Status == newStatus) return;

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is mandatory for trainer status updates.");
        }

        trainer.Status = newStatus;
        trainer.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "TRAINER_STATUS_CHANGED",
            "TrainerProfile",
            trainer.Id,
            reason.Trim());

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TrainerDiagnosticReport?> GetTrainerDiagnosticsAsync(
        Guid trainerId,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var t = await _db.TrainerProfiles
            .AsNoTracking()
            .Include(x => x.CurrentTier)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == trainerId || x.UserId == trainerId, cancellationToken);

        if (t == null) return null;

        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);

        var businessIssues = new List<DiagnosticIssue>();
        var technicalFailures = new List<DiagnosticIssue>();

        // 1. Unpriced Workshop Proposals (Business State)
        var unpricedWorkshops = await _db.Workshops
            .AsNoTracking()
            .Where(w => w.TrainerProfileId == t.Id && w.Status == WorkshopStatus.PendingApproval && w.TrainerProposedPrice != null && w.AdminApprovedPrice == null)
            .ToListAsync(cancellationToken);

        if (unpricedWorkshops.Count > 0)
        {
            businessIssues.Add(new DiagnosticIssue
            {
                Code = "UNPRICED_WORKSHOPS",
                Category = "BUSINESS_STATE",
                Severity = "WARNING",
                Title = "Unpriced workshop proposals awaiting pricing approval",
                Description = $"Trainer has {unpricedWorkshops.Count} pending workshop proposal(s) awaiting pricing review.",
                Evidence = new
                {
                    unpricedCount = unpricedWorkshops.Count,
                    workshops = unpricedWorkshops.Select(w => new { w.Id, w.Title, proposedPrice = w.TrainerProposedPrice }).ToList()
                },
                DetectedAt = now,
                TraceId = traceId,
                RecommendedAction = "Review and approve pricing for submitted workshop proposals."
            });
        }

        // 3. Community Feedback Rating (Business State)
        var feedback = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Where(f => f.Workshop.TrainerProfileId == t.Id)
            .ToListAsync(cancellationToken);

        var avgRating = feedback.Count > 0 ? Math.Round(feedback.Average(f => (double)f.Rating), 2) : 5.0;
        if (feedback.Count >= 1 && avgRating < 3.5)
        {
            businessIssues.Add(new DiagnosticIssue
            {
                Code = "LOW_TRAINER_RATING",
                Category = "BUSINESS_STATE",
                Severity = "WARNING",
                Title = "Low community feedback rating",
                Description = $"Trainer community rating ({avgRating:F1} / 5.0) is below the 3.5 quality baseline.",
                Evidence = new
                {
                    rating = avgRating,
                    feedbackRecordsCount = feedback.Count,
                    threshold = 3.5
                },
                DetectedAt = now,
                TraceId = traceId,
                RecommendedAction = "Review student feedback comments with trainer and offer pedagogical support."
            });
        }

        // 4. Upgrade Payment Failures (Technical Failure - 30 days)
        var failedUpgrades = await _db.PaymentTransactions
            .AsNoTracking()
            .Where(p => p.UserId == t.UserId && p.Purpose == PaymentPurpose.TrainerTierUpgrade && p.Status == PaymentStatus.Failed)
            .Where(p => p.CreatedAt >= thirtyDaysAgo)
            .ToListAsync(cancellationToken);

        foreach (var p in failedUpgrades)
        {
            technicalFailures.Add(new DiagnosticIssue
            {
                Code = "UPGRADE_PAYMENT_FAILURE",
                Category = "TECHNICAL_FAILURE",
                Severity = "CRITICAL",
                Title = "Tier upgrade payment failed",
                Description = $"Tier upgrade payment transaction {p.Id} failed at gateway.",
                Evidence = new
                {
                    paymentId = p.Id,
                    orderId = p.RazorpayOrderId,
                    amount = p.Amount,
                    status = "FAILED"
                },
                DetectedAt = p.CreatedAt,
                TraceId = traceId,
                RecommendedAction = "Investigate upgrade payment transaction or assist trainer with re-attempting payment."
            });
        }

        // 5. Profile / Portfolio Incomplete (Business State)
        var missingPortfolio = new List<string>();
        if (string.IsNullOrWhiteSpace(t.Bio)) missingPortfolio.Add("Bio");
        if (!t.ExperienceYears.HasValue) missingPortfolio.Add("ExperienceYears");
        if (string.IsNullOrWhiteSpace(t.PrimaryDanceStyle)) missingPortfolio.Add("PrimaryDanceStyle");

        if (missingPortfolio.Count > 0)
        {
            businessIssues.Add(new DiagnosticIssue
            {
                Code = "PROFILE_INCOMPLETE",
                Category = "BUSINESS_STATE",
                Severity = "INFO",
                Title = "Trainer profile incomplete",
                Description = $"Missing portfolio details: {string.Join(", ", missingPortfolio)}.",
                Evidence = new { missingFields = missingPortfolio },
                DetectedAt = now,
                TraceId = traceId,
                RecommendedAction = "Advise trainer to complete profile bio and portfolio details."
            });
        }

        // Overall Status Synthesis
        var overallStatus = "HEALTHY";
        if (technicalFailures.Any(t => t.Severity == "CRITICAL") || businessIssues.Any(b => b.Severity == "CRITICAL"))
        {
            overallStatus = "CRITICAL_FAILURE";
        }
        else if (technicalFailures.Any(t => t.Severity == "WARNING") || businessIssues.Any(b => b.Severity == "WARNING"))
        {
            overallStatus = "ATTENTION_REQUIRED";
        }

        var allActions = businessIssues.Select(b => b.RecommendedAction)
            .Concat(technicalFailures.Select(t => t.RecommendedAction))
            .Distinct()
            .ToList();

        var activeWorkshopsCount = await _db.Workshops
            .AsNoTracking()
            .CountAsync(w => w.TrainerProfileId == t.Id && w.Status == WorkshopStatus.Approved, cancellationToken);

        return new TrainerDiagnosticReport
        {
            TrainerId = t.Id,
            UserId = t.UserId,
            TrainerCode = t.TrainerCode,
            FullName = t.FullName,
            CurrentTier = t.CurrentTier?.Name,
            OverallStatus = overallStatus,
            BusinessIssues = businessIssues,
            TechnicalFailures = technicalFailures,
            RecommendedActions = allActions,
            AverageRating = avgRating,
            TotalFeedbackCount = feedback.Count,
            ActiveWorkshopsCount = activeWorkshopsCount,
            UnpricedWorkshopsCount = unpricedWorkshops.Count,
            GeneratedAt = now,
            TraceId = traceId
        };
    }

    private static TrainerResponse Map(TrainerProfile trainer)
    {
        return new TrainerResponse
        {
            Id = trainer.Id,
            TrainerCode = trainer.TrainerCode,
            FullName = trainer.FullName,
            City = trainer.City,
            ProfilePhotoUrl = trainer.ProfilePhotoUrl,
            PrimaryDanceStyle = trainer.PrimaryDanceStyle,
            SecondaryDanceStyles = trainer.SecondaryDanceStyles,
            ExperienceYears = trainer.ExperienceYears,
            CurrentStudio = trainer.CurrentStudio,
            Bio = trainer.Bio,
            InstagramUrl = trainer.InstagramUrl,
            YouTubeUrl = trainer.YouTubeUrl,
            Status = trainer.Status.ToString(),
            Tier = trainer.CurrentTier?.Name,
            ApprovedAt = trainer.ApprovedAt
        };
    }

    public async Task<AdminTrainerListResponse> CreateTrainerAsync(
        AdminCreateTrainerRequest request,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ArgumentException("Trainer full name is required.");
        if (string.IsNullOrWhiteSpace(request.Phone))
            throw new ArgumentException("Trainer phone number is required.");

        var phoneClean = request.Phone.Trim();
        var existingUser = await _db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Phone == phoneClean, cancellationToken);
        Guid userId;

        if (existingUser != null)
        {
            var alreadyTrainer = await _db.TrainerProfiles.AnyAsync(tp => tp.UserId == existingUser.Id, cancellationToken);
            if (alreadyTrainer)
                throw new InvalidOperationException("A trainer profile already exists for this phone number.");

            userId = existingUser.Id;
            var trainerRole = await _db.Roles.FirstOrDefaultAsync(r => r.Code == "TRAINER", cancellationToken);
            if (trainerRole != null && !existingUser.UserRoles.Any(ur => ur.RoleId == trainerRole.Id))
            {
                existingUser.UserRoles.Add(new UserRole { UserId = existingUser.Id, RoleId = trainerRole.Id });
            }
        }
        else
        {
            var trainerRole = await _db.Roles.FirstOrDefaultAsync(r => r.Code == "TRAINER", cancellationToken);
            if (trainerRole == null)
                throw new InvalidOperationException("Trainer role not configured in system.");

            var customerCode = $"ETH-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                CustomerCode = customerCode,
                FullName = request.FullName.Trim(),
                Phone = phoneClean,
                Email = request.Email?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            newUser.UserRoles.Add(new UserRole { UserId = newUser.Id, RoleId = trainerRole.Id });
            _db.Users.Add(newUser);
            userId = newUser.Id;
        }

        var count = await _db.TrainerProfiles.CountAsync(cancellationToken) + 1;
        var trainerCode = $"TRN-{DateTime.UtcNow.Year}-{count:D4}";

        Guid? tierId = request.TierId;
        if (!tierId.HasValue)
        {
            var defaultTier = await _db.TrainerTiers.OrderBy(t => t.DisplayOrder).FirstOrDefaultAsync(cancellationToken);
            tierId = defaultTier?.Id;
        }

        var trainer = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TrainerCode = trainerCode,
            FullName = request.FullName.Trim(),
            City = request.City?.Trim() ?? "Hyderabad",
            PrimaryDanceStyle = request.PrimaryDanceStyle?.Trim(),
            SecondaryDanceStyles = request.SecondaryDanceStyles?.Trim(),
            ExperienceYears = request.ExperienceYears,
            Bio = request.Bio?.Trim(),
            CurrentTierId = tierId,
            Status = TrainerStatus.Active,
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.TrainerProfiles.Add(trainer);
        await _db.SaveChangesAsync(cancellationToken);

        string? tierName = null;
        if (tierId.HasValue)
        {
            var tObj = await _db.TrainerTiers.FindAsync(new object[] { tierId.Value }, cancellationToken);
            tierName = tObj?.Name;
        }

        return new AdminTrainerListResponse
        {
            TrainerId = trainer.Id,
            UserId = trainer.UserId,
            TrainerCode = trainer.TrainerCode,
            FullName = trainer.FullName,
            Phone = phoneClean,
            Email = request.Email?.Trim(),
            City = trainer.City,
            Status = trainer.Status.ToString(),
            TierName = tierName,
            ProfilePhotoUrl = trainer.ProfilePhotoUrl,
            PrimaryDanceStyle = trainer.PrimaryDanceStyle,
            SecondaryDanceStyles = trainer.SecondaryDanceStyles,
            ApprovedAt = trainer.ApprovedAt,
            CreatedAt = trainer.CreatedAt
        };
    }
}
