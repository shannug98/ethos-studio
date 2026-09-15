using Ethos.Api.Application.Storage;
using Ethos.Api.Application.Trainers.DTOs;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Trainers;

public class TrainerApplicationService : ITrainerApplicationService
{
    private readonly AppDbContext _db;
    private readonly ITrainerApplicationVideoStorageService _videoStorage;
    private readonly ITrainerVideoMetadataService _videoMetadata;

    public TrainerApplicationService(
        AppDbContext db,
        ITrainerApplicationVideoStorageService videoStorage,
        ITrainerVideoMetadataService videoMetadata)
    {
        _db = db;
        _videoStorage = videoStorage;
        _videoMetadata = videoMetadata;
    }

    public async Task<TrainerApplicationVideoDto> UploadVideoAsync(
        Guid userId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new InvalidOperationException(
                "Please select a video to upload.");
        }

        const long maxFileSizeBytes = 100 * 1024 * 1024;

        if (file.Length > maxFileSizeBytes)
        {
            throw new InvalidOperationException(
                "Video file size cannot exceed 100 MB.");
        }

        var application = await _db.TrainerApplications
            .Include(x => x.TrainerProfile)
            .Include(x => x.VideoIntroduction)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(
                x => x.TrainerProfile.UserId == userId,
                cancellationToken);

        if (application is null)
        {
            throw new KeyNotFoundException(
                "Trainer application was not found.");
        }

        if (application.Status != TrainerApplicationStatus.Draft &&
            application.Status != TrainerApplicationStatus.ChangesRequested)
        {
            throw new InvalidOperationException(
                "Your trainer application can no longer be edited.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        var allowedExtensions = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ".mp4",
            ".mov",
            ".webm",
            ".m4v"
        };

        if (string.IsNullOrWhiteSpace(extension) ||
            !allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                "Only MP4, MOV, WEBM, and M4V videos are allowed.");
        }

        var allowedContentTypes = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "video/mp4",
            "video/quicktime",
            "video/webm",
            "video/x-m4v"
        };

        if (!string.IsNullOrWhiteSpace(file.ContentType) &&
            !allowedContentTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException(
                "The selected video format is not supported.");
        }

        string? newStoragePath = null;

        try
        {
            await using (var stream = file.OpenReadStream())
            {
                newStoragePath = await _videoStorage.SaveVideoAsync(
                    stream,
                    file.FileName,
                    file.ContentType,
                    application.Id,
                    cancellationToken);
            }

            var physicalPath = _videoStorage.GetPhysicalPath(newStoragePath);

            var metadata = await _videoMetadata.AnalyzeAsync(
                physicalPath,
                cancellationToken);

            if (!metadata.Success || !metadata.HasVideoStream)
            {
                await _videoStorage.DeleteVideoAsync(newStoragePath, cancellationToken);
                newStoragePath = null;
                throw new InvalidOperationException(
                    metadata.ErrorMessage ?? "The uploaded file does not contain a valid video.");
            }

            if (metadata.Duration > TimeSpan.FromSeconds(60))
            {
                await _videoStorage.DeleteVideoAsync(newStoragePath, cancellationToken);
                newStoragePath = null;
                throw new InvalidOperationException(
                    "Your video must be 1 minute or less.");
            }

            var durationSeconds = (int)Math.Ceiling(metadata.Duration.TotalSeconds);

            // Upload mode is now active.
            // Clear any previously saved YouTube URL so the application
            // always has exactly one introduction video source.
            application.TrainerProfile.YouTubeUrl = null;

            var existingVideo = application.VideoIntroduction;

            if (existingVideo is not null)
            {
                await _videoStorage.DeleteVideoAsync(
                    existingVideo.StoragePath,
                    cancellationToken);

                _db.TrainerApplicationVideos.Remove(existingVideo);
            }

            var video = new TrainerApplicationVideo
            {
                Id = Guid.NewGuid(),
                TrainerApplicationId = application.Id,
                FileName = Path.GetFileName(file.FileName),
                StoragePath = newStoragePath,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                DurationSeconds = durationSeconds,
                UploadedAt = DateTime.UtcNow
            };

            _db.TrainerApplicationVideos.Add(video);

            await _db.SaveChangesAsync(cancellationToken);

            newStoragePath = null;

            return new TrainerApplicationVideoDto
            {
                Id = video.Id,
                FileName = video.FileName,
                ContentType = video.ContentType,
                FileSizeBytes = video.FileSizeBytes,
                DurationSeconds = video.DurationSeconds,
                UploadedAt = video.UploadedAt
            };
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(newStoragePath))
            {
                try
                {
                    await _videoStorage.DeleteVideoAsync(newStoragePath, cancellationToken);
                }
                catch
                {
                    // Do not mask original exception
                }
            }

            throw;
        }
    }

    public async Task DeleteVideoAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var application = await _db.TrainerApplications
            .Include(x => x.TrainerProfile)
            .Include(x => x.VideoIntroduction)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(
                x => x.TrainerProfile.UserId == userId,
                cancellationToken);

        if (application is null)
        {
            throw new KeyNotFoundException(
                "Trainer application was not found.");
        }

        if (application.Status != TrainerApplicationStatus.Draft &&
            application.Status != TrainerApplicationStatus.ChangesRequested)
        {
            throw new InvalidOperationException(
                "Your trainer application can no longer be edited.");
        }

        var video = application.VideoIntroduction;

        if (video is null)
        {
            return;
        }

        await _videoStorage.DeleteVideoAsync(
            video.StoragePath,
            cancellationToken);

        _db.TrainerApplicationVideos.Remove(video);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TrainerApplicationVideoDto?> GetVideoAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var video = await _db.TrainerApplicationVideos
            .AsNoTracking()
            .Include(x => x.TrainerApplication)
            .ThenInclude(x => x.TrainerProfile)
            .Where(x =>
                x.TrainerApplication.TrainerProfile.UserId == userId)
            .OrderByDescending(x => x.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (video is null)
        {
            return null;
        }

        return new TrainerApplicationVideoDto
        {
            Id = video.Id,
            FileName = video.FileName,
            ContentType = video.ContentType,
            FileSizeBytes = video.FileSizeBytes,
            DurationSeconds = video.DurationSeconds,
            UploadedAt = video.UploadedAt
        };
    }

    public async Task<(string PhysicalPath, string ContentType)?> GetVideoStreamAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var video = await _db.TrainerApplicationVideos
            .AsNoTracking()
            .Include(x => x.TrainerApplication)
            .ThenInclude(x => x.TrainerProfile)
            .Where(x => x.TrainerApplication.TrainerProfile.UserId == userId)
            .OrderByDescending(x => x.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (video is null)
        {
            return null;
        }

        var physicalPath = _videoStorage.GetPhysicalPath(video.StoragePath);
        if (!System.IO.File.Exists(physicalPath))
        {
            return null;
        }

        return (physicalPath, video.ContentType);
    }


    public async Task<TrainerApplicationResponse> CreateAsync(
        Guid userId,
        CreateTrainerApplicationRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Verify Tier
        var tier = await _db.TrainerTiers
            .FirstOrDefaultAsync(t => t.Id == request.TierId && t.IsActive, cancellationToken);

        if (tier == null)
        {
            throw new ArgumentException("Invalid or inactive trainer tier specified.");
        }

        // 2. Check existing active trainer profile
        var trainerProfile = await _db.TrainerProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (trainerProfile != null && trainerProfile.Status == TrainerStatus.Active)
        {
            throw new InvalidOperationException("User is already an active trainer.");
        }

        if (trainerProfile == null)
        {
            trainerProfile = new TrainerProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TrainerCode = $"TRN{Guid.NewGuid():N}".Substring(0, 10).ToUpper(),
                FullName = request.FullName?.Trim() ?? string.Empty,
                City = request.City?.Trim(),
                ProfilePhotoUrl = request.ProfilePhotoUrl?.Trim(),
                PrimaryDanceStyle = request.PrimaryDanceStyle?.Trim(),
                SecondaryDanceStyles = request.SecondaryDanceStyles?.Trim(),
                ExperienceYears = request.ExperienceYears,
                CurrentStudio = request.CurrentStudio?.Trim(),
                Bio = request.Bio?.Trim(),
                InstagramUrl = request.InstagramUrl?.Trim(),
                YouTubeUrl = request.YouTubeUrl?.Trim(),
                Status = TrainerStatus.Pending,
                CurrentTierId = tier.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.TrainerProfiles.Add(trainerProfile);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.FullName)) trainerProfile.FullName = request.FullName.Trim();
            if (request.City != null) trainerProfile.City = request.City.Trim();
            if (request.ProfilePhotoUrl != null) trainerProfile.ProfilePhotoUrl = request.ProfilePhotoUrl.Trim();
            if (request.PrimaryDanceStyle != null) trainerProfile.PrimaryDanceStyle = request.PrimaryDanceStyle.Trim();
            if (request.SecondaryDanceStyles != null) trainerProfile.SecondaryDanceStyles = request.SecondaryDanceStyles.Trim();
            if (request.ExperienceYears.HasValue) trainerProfile.ExperienceYears = request.ExperienceYears;
            if (request.CurrentStudio != null) trainerProfile.CurrentStudio = request.CurrentStudio.Trim();
            if (request.Bio != null) trainerProfile.Bio = request.Bio.Trim();
            if (request.InstagramUrl != null) trainerProfile.InstagramUrl = request.InstagramUrl.Trim();
            if (request.YouTubeUrl != null) trainerProfile.YouTubeUrl = request.YouTubeUrl.Trim();
            trainerProfile.CurrentTierId = tier.Id;
            trainerProfile.UpdatedAt = DateTime.UtcNow;
        }

        // Check if existing pending/draft application exists
        var existingApp = await _db.TrainerApplications
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(a => a.TrainerProfileId == trainerProfile.Id, cancellationToken);

        if (existingApp != null && (existingApp.Status == TrainerApplicationStatus.Draft || existingApp.Status == TrainerApplicationStatus.PaymentPending))
        {
            existingApp.ApplicationNotes = request.ApplicationNotes?.Trim();
            existingApp.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return await GetByIdAsync(existingApp.Id, cancellationToken);
        }

        var application = new TrainerApplication
        {
            Id = Guid.NewGuid(),
            TrainerProfileId = trainerProfile.Id,
            Status = TrainerApplicationStatus.Draft,
            ApplicationNotes = request.ApplicationNotes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.TrainerApplications.Add(application);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(application.Id, cancellationToken);
    }

    public async Task<TrainerApplicationResponse?> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var application = await _db.TrainerApplications
            .Include(x => x.TrainerProfile)
            .Include(x => x.TrainerProfile.CurrentTier)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(
                x => x.TrainerProfile.UserId == userId,
                cancellationToken);

        if (application == null)
            return null;

        return Map(application);
    }

    public async Task<TrainerApplicationResponse?> UpdateAsync(
        Guid userId,
        UpdateTrainerApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var application = await _db.TrainerApplications
            .Include(x => x.TrainerProfile)
            .Include(x => x.VideoIntroduction)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(
                x => x.TrainerProfile.UserId == userId,
                cancellationToken);

        if (application == null)
            return null;

        if (application.Status != TrainerApplicationStatus.Draft &&
            application.Status != TrainerApplicationStatus.ChangesRequested)
        {
            throw new InvalidOperationException("Application cannot be edited in its current status.");
        }

        var trainer = application.TrainerProfile;

        if (!string.IsNullOrWhiteSpace(request.FullName)) trainer.FullName = request.FullName.Trim();
        if (request.City != null) trainer.City = request.City.Trim();
        if (request.ProfilePhotoUrl != null) trainer.ProfilePhotoUrl = request.ProfilePhotoUrl.Trim();
        if (request.PrimaryDanceStyle != null) trainer.PrimaryDanceStyle = request.PrimaryDanceStyle.Trim();
        if (request.SecondaryDanceStyles != null) trainer.SecondaryDanceStyles = request.SecondaryDanceStyles.Trim();
        if (request.ExperienceYears.HasValue) trainer.ExperienceYears = request.ExperienceYears;
        if (request.CurrentStudio != null) trainer.CurrentStudio = request.CurrentStudio.Trim();
        if (request.Bio != null) trainer.Bio = request.Bio.Trim();
        
        if (request.InstagramUrl != null)
        {
            trainer.InstagramUrl = request.InstagramUrl.Trim();
        }

        if (request.YouTubeUrl != null)
        {
            var youtubeUrl = request.YouTubeUrl.Trim();

            if (!string.IsNullOrWhiteSpace(youtubeUrl))
            {
                // YouTube mode selected.
                // Remove any previously uploaded video so that
                // exactly one video source remains.
                if (application.VideoIntroduction is not null)
                {
                    await _videoStorage.DeleteVideoAsync(
                        application.VideoIntroduction.StoragePath,
                        cancellationToken);

                    _db.TrainerApplicationVideos.Remove(
                        application.VideoIntroduction);

                    application.VideoIntroduction = null;
                }

                trainer.YouTubeUrl = youtubeUrl;
            }
            else
            {
                trainer.YouTubeUrl = null;
            }
        }

        if (request.TierId.HasValue)
        {
            var tierExists = await _db.TrainerTiers.AnyAsync(t => t.Id == request.TierId.Value && t.IsActive, cancellationToken);
            if (!tierExists)
            {
                throw new ArgumentException("Invalid or inactive trainer tier specified.");
            }
            trainer.CurrentTierId = request.TierId.Value;
        }

        application.ApplicationNotes = request.ApplicationNotes?.Trim();
        application.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return await GetMineAsync(userId, cancellationToken);
    }

    public async Task SubmitAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var application = await _db.TrainerApplications
            .Include(x => x.TrainerProfile)
            .Include(x => x.VideoIntroduction)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(
                x => x.TrainerProfile.UserId == userId,
                cancellationToken);

        if (application == null)
        {
            throw new InvalidOperationException(
                "Trainer application does not exist.");
        }

        if (application.Status != TrainerApplicationStatus.Draft &&
            application.Status != TrainerApplicationStatus.ChangesRequested &&
            application.Status != TrainerApplicationStatus.PaymentVerified)
        {
            throw new InvalidOperationException(
                "Application cannot be submitted in its current status.");
        }

        var trainer = application.TrainerProfile;

        if (string.IsNullOrWhiteSpace(trainer.FullName))
        {
            throw new InvalidOperationException(
                "Full name is required before submitting the application.");
        }

        if (string.IsNullOrWhiteSpace(trainer.City))
        {
            throw new InvalidOperationException(
                "City is required before submitting the application.");
        }

        if (string.IsNullOrWhiteSpace(trainer.PrimaryDanceStyle))
        {
            throw new InvalidOperationException(
                "Primary dance style is required before submitting the application.");
        }

        if (!trainer.ExperienceYears.HasValue ||
            trainer.ExperienceYears.Value < 0)
        {
            throw new InvalidOperationException(
                "Valid teaching experience is required before submitting the application.");
        }

        if (string.IsNullOrWhiteSpace(trainer.Bio))
        {
            throw new InvalidOperationException(
                "Trainer bio is required before submitting the application.");
        }

        if (!trainer.CurrentTierId.HasValue)
        {
            throw new InvalidOperationException(
                "Trainer tier selection is required before submitting the application.");
        }

        var tierExists = await _db.TrainerTiers
            .AnyAsync(
                x => x.Id == trainer.CurrentTierId.Value &&
                     x.IsActive,
                cancellationToken);

        if (!tierExists)
        {
            throw new InvalidOperationException(
                "Selected trainer tier is invalid or inactive.");
        }

        var hasYouTubeVideo =
            !string.IsNullOrWhiteSpace(trainer.YouTubeUrl);

        var hasUploadedVideo =
            application.VideoIntroduction is not null;

        if (!hasYouTubeVideo && !hasUploadedVideo)
        {
            throw new InvalidOperationException(
                "A video introduction is required before submitting your application. Please add a YouTube video or upload a video clip of 1 minute or less.");
        }

        if (hasYouTubeVideo && hasUploadedVideo)
        {
            throw new InvalidOperationException(
                "Please provide either a YouTube video or an uploaded video, not both.");
        }

        if (application.Status == TrainerApplicationStatus.PaymentVerified)
        {
            application.Status = TrainerApplicationStatus.UnderReview;
        }
        else
        {
            application.Status = TrainerApplicationStatus.PaymentPending;
        }

        application.SubmittedAt = DateTime.UtcNow;
        application.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<TrainerApplicationResponse> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var application = await _db.TrainerApplications
            .AsNoTracking()
            .Include(x => x.TrainerProfile)
            .ThenInclude(x => x.CurrentTier)
            .FirstAsync(x => x.Id == id, cancellationToken);

        return Map(application);
    }

    private static TrainerApplicationResponse Map(
        TrainerApplication application)
    {
        var profile = application.TrainerProfile;

        return new TrainerApplicationResponse
        {
            Id = application.Id,
            TrainerProfileId = application.TrainerProfileId,
            Tier = profile?.CurrentTier?.Name,
            CurrentTierId = profile?.CurrentTierId,
            Status = application.Status.ToString(),
            FullName = profile?.FullName,
            City = profile?.City,
            ProfilePhotoUrl = profile?.ProfilePhotoUrl,
            PrimaryDanceStyle = profile?.PrimaryDanceStyle,
            SecondaryDanceStyles = profile?.SecondaryDanceStyles,
            ExperienceYears = profile?.ExperienceYears,
            CurrentStudio = profile?.CurrentStudio,
            Bio = profile?.Bio,
            InstagramUrl = profile?.InstagramUrl,
            YouTubeUrl = profile?.YouTubeUrl,
            ApplicationNotes = application.ApplicationNotes,
            AdminNotes = application.AdminNotes,
            RejectionReason = application.RejectionReason,
            PaymentTransactionId = application.PaymentTransactionId,
            SubmittedAt = application.SubmittedAt,
            PaymentVerifiedAt = application.PaymentVerifiedAt,
            ReviewedAt = application.ReviewedAt
        };
    }
}
