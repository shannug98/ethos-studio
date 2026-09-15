using Ethos.Api.Application.Auth;
using Ethos.Api.Application.Storage;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Ethos.Api.Application.Admin;

public class AdminTrainerApplicationService : IAdminTrainerApplicationService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;
    private readonly IPasswordService _passwordService;
    private readonly ITrainerApplicationVideoStorageService _videoStorage;

    public AdminTrainerApplicationService(
        AppDbContext db,
        IAdminAuditService auditService,
        IPasswordService passwordService,
        ITrainerApplicationVideoStorageService videoStorage)
    {
        _db = db;
        _auditService = auditService;
        _passwordService = passwordService;
        _videoStorage = videoStorage;
    }

    public async Task<IReadOnlyList<TrainerApplicationResponse>> GetAllApplicationsAsync(
        CancellationToken cancellationToken)
    {
        var apps = await _db.TrainerApplications
            .AsNoTracking()
            .Include(x => x.TrainerProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.TrainerProfile)
                .ThenInclude(x => x.CurrentTier)
            .Include(x => x.PaymentTransaction)
            .Include(x => x.VideoIntroduction)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return apps.Select(x => Map(x)).ToList();
    }

    public async Task<PagedResult<TrainerApplicationResponse>> GetApplicationsAsync(
        int page,
        int pageSize,
        TrainerApplicationStatus? status,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.TrainerApplications
            .AsNoTracking()
            .Include(x => x.TrainerProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.TrainerProfile)
                .ThenInclude(x => x.CurrentTier)
            .Include(x => x.PaymentTransaction)
            .Include(x => x.VideoIntroduction)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var applications = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = applications.Select(x => Map(x)).ToList();

        return new PagedResult<TrainerApplicationResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<TrainerApplicationResponse?> GetApplicationByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var app = await _db.TrainerApplications
            .AsNoTracking()
            .Include(x => x.TrainerProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.TrainerProfile)
                .ThenInclude(x => x.CurrentTier)
            .Include(x => x.PaymentTransaction)
            .Include(x => x.VideoIntroduction)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return app == null ? null : Map(app);
    }

    public async Task ApproveApplicationAsync(
        Guid applicationId,
        Guid adminUserId,
        AdminApproveApplicationRequest request,
        CancellationToken cancellationToken)
    {
        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var application = await _db.TrainerApplications
            .Include(x => x.TrainerProfile)
            .Include(x => x.PaymentTransaction)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken);

        if (application == null)
            throw new ArgumentException("Trainer application not found.");

        if (application.Status != TrainerApplicationStatus.PaymentVerified &&
            (application.PaymentTransaction == null || application.PaymentTransaction.Status != PaymentStatus.Paid))
        {
            throw new InvalidOperationException("Trainer application payment must be verified before admin approval.");
        }

        var trainer = application.TrainerProfile;

        // Ensure TRAINER role is assigned without removing existing roles
        var trainerRole = await _db.Roles
            .FirstOrDefaultAsync(r => r.Code == "TRAINER", cancellationToken);

        if (trainerRole != null)
        {
            var hasTrainerRole = await _db.UserRoles
                .AnyAsync(ur => ur.UserId == trainer.UserId && ur.RoleId == trainerRole.Id, cancellationToken);

            if (!hasTrainerRole)
            {
                _db.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = trainer.UserId,
                    RoleId = trainerRole.Id,
                    AssignedAt = DateTime.UtcNow,
                    AssignedBy = adminUserId
                });
            }
        }

        application.Status = TrainerApplicationStatus.Approved;
        application.AdminNotes = request.AdminNotes?.Trim();
        application.ReviewedAt = DateTime.UtcNow;
        application.ReviewedByUserId = adminUserId;
        application.UpdatedAt = DateTime.UtcNow;

        trainer.Status = TrainerStatus.Active;
        trainer.ApprovedAt = DateTime.UtcNow;
        trainer.UpdatedAt = DateTime.UtcNow;

        // Tier History
        if (trainer.CurrentTierId.HasValue)
        {
            _db.TrainerTierHistories.Add(new TrainerTierHistory
            {
                Id = Guid.NewGuid(),
                TrainerProfileId = trainer.Id,
                NewTierId = trainer.CurrentTierId.Value,
                Reason = "Initial application approved by admin.",
                ChangedByUserId = adminUserId,
                ChangedAt = DateTime.UtcNow
            });
        }

        // Notification
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Title = "Trainer Application Approved!",
            Message = "🎉 Your ETHOS Trainer application has been approved. Your Trainer Portal is now active. Log in using your registered mobile number and OTP. Welcome to ETHOS.",
            Type = NotificationType.System,
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(notification);

        _db.NotificationRecipients.Add(new NotificationRecipient
        {
            Id = Guid.NewGuid(),
            NotificationId = notification.Id,
            UserId = trainer.UserId,
            IsRead = false
        });

        _auditService.AddAuditLog(
            adminUserId,
            "TRAINER_APPLICATION_APPROVED",
            "TrainerApplication",
            application.Id,
            request.AdminNotes);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task RejectApplicationAsync(
        Guid applicationId,
        Guid adminUserId,
        AdminRejectApplicationRequest request,
        CancellationToken cancellationToken)
    {
        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var application = await _db.TrainerApplications
            .Include(x => x.TrainerProfile)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken);

        if (application == null)
            throw new ArgumentException("Trainer application not found.");

        if (string.IsNullOrWhiteSpace(request.RejectionReason))
            throw new ArgumentException("Rejection reason is required.");

        application.Status = TrainerApplicationStatus.Rejected;
        application.RejectionReason = request.RejectionReason.Trim();
        application.AdminNotes = request.AdminNotes?.Trim();
        application.ReviewedAt = DateTime.UtcNow;
        application.ReviewedByUserId = adminUserId;
        application.UpdatedAt = DateTime.UtcNow;

        application.TrainerProfile.Status = TrainerStatus.Inactive;
        application.TrainerProfile.UpdatedAt = DateTime.UtcNow;

        // Notification
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Title = "Trainer Application Update",
            Message = $"Your Ethos Trainer Application was rejected. Reason: {request.RejectionReason}",
            Type = NotificationType.System,
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(notification);

        _db.NotificationRecipients.Add(new NotificationRecipient
        {
            Id = Guid.NewGuid(),
            NotificationId = notification.Id,
            UserId = application.TrainerProfile.UserId,
            IsRead = false
        });

        _auditService.AddAuditLog(
            adminUserId,
            "TRAINER_APPLICATION_REJECTED",
            "TrainerApplication",
            application.Id,
            request.RejectionReason);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task RequestChangesAsync(
        Guid applicationId,
        Guid adminUserId,
        AdminRequestChangesRequest request,
        CancellationToken cancellationToken)
    {
        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var application = await _db.TrainerApplications
            .Include(x => x.TrainerProfile)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken);

        if (application == null)
            throw new ArgumentException("Trainer application not found.");

        if (string.IsNullOrWhiteSpace(request.Notes))
            throw new ArgumentException("Request changes reason is required.");

        application.Status = TrainerApplicationStatus.ChangesRequested;
        application.AdminNotes = request.Notes.Trim();
        application.ReviewedAt = DateTime.UtcNow;
        application.ReviewedByUserId = adminUserId;
        application.UpdatedAt = DateTime.UtcNow;

        // Notification
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Title = "Trainer Application Changes Requested",
            Message = $"Admin requested changes to your application: {request.Notes}",
            Type = NotificationType.System,
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(notification);

        _db.NotificationRecipients.Add(new NotificationRecipient
        {
            Id = Guid.NewGuid(),
            NotificationId = notification.Id,
            UserId = application.TrainerProfile.UserId,
            IsRead = false
        });

        _auditService.AddAuditLog(
            adminUserId,
            "TRAINER_APPLICATION_CHANGES_REQUESTED",
            "TrainerApplication",
            application.Id,
            request.Notes);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task<(string PhysicalPath, string ContentType)?> GetApplicationVideoStreamAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var video = await _db.TrainerApplicationVideos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TrainerApplicationId == applicationId, cancellationToken);

        if (video == null) return null;

        var physicalPath = _videoStorage.GetPhysicalPath(video.StoragePath);
        if (!System.IO.File.Exists(physicalPath)) return null;

        return (physicalPath, video.ContentType);
    }

    private static TrainerApplicationResponse Map(TrainerApplication application)
    {
        var profile = application.TrainerProfile;
        var user = profile?.User;
        var tier = profile?.CurrentTier;
        var payment = application.PaymentTransaction;
        var video = application.VideoIntroduction;

        return new TrainerApplicationResponse
        {
            Id = application.Id,
            TrainerProfileId = application.TrainerProfileId,
            TrainerCode = profile?.TrainerCode,
            FullName = profile?.FullName ?? user?.FullName,
            Phone = user?.Phone,
            Email = user?.Email,
            City = profile?.City,
            ProfilePhotoUrl = profile?.ProfilePhotoUrl,
            PrimaryDanceStyle = profile?.PrimaryDanceStyle,
            SecondaryDanceStyles = profile?.SecondaryDanceStyles,
            ExperienceYears = profile?.ExperienceYears,
            CurrentStudio = profile?.CurrentStudio,
            Bio = profile?.Bio,
            InstagramUrl = profile?.InstagramUrl,
            YouTubeUrl = profile?.YouTubeUrl,
            Tier = tier?.Name,
            CurrentTierId = profile?.CurrentTierId,
            TierFee = tier?.ApplicationFee,
            Status = application.Status.ToString(),
            ApplicationNotes = application.ApplicationNotes,
            AdminNotes = application.AdminNotes,
            RejectionReason = application.RejectionReason,
            PaymentTransactionId = application.PaymentTransactionId,
            PaymentAmount = payment?.Amount,
            PaymentStatus = payment?.Status.ToString(),
            PaymentReference = payment?.RazorpayPaymentId ?? payment?.RazorpayOrderId,
            PaymentDate = payment?.PaidAt ?? payment?.CreatedAt,
            SubmittedAt = application.SubmittedAt,
            PaymentVerifiedAt = application.PaymentVerifiedAt,
            ReviewedAt = application.ReviewedAt,
            CreatedAt = application.CreatedAt,
            HasVideo = video != null,
            VideoFileName = video?.FileName,
            VideoDurationSeconds = video?.DurationSeconds,
            VideoSizeBytes = video?.FileSizeBytes,
            VideoContentType = video?.ContentType,
            VideoUploadedAt = video?.UploadedAt
        };
    }
}
