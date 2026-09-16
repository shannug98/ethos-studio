using Ethos.Api.Application.Storage;
using Ethos.Api.Contracts.Videos;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Application.Videos;

public class VideoService : IVideoService
{
    private readonly AppDbContext _db;
    private readonly ICloudflareR2StorageService _r2Service;
    private readonly ILogger<VideoService> _logger;

    private const long ShortVideosMaxSizeBytes = 25 * 1024 * 1024; // 25 MB
    private const long GalleryMaxSizeBytes = 100 * 1024 * 1024;     // 100 MB

    public VideoService(
        AppDbContext db,
        ICloudflareR2StorageService r2Service,
        ILogger<VideoService> logger)
    {
        _db = db;
        _r2Service = r2Service;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StudioVideoResponse>> GetPublicVideosAsync(
        string section,
        CancellationToken cancellationToken = default)
    {
        var normalizedSection = NormalizeSection(section);

        var videos = await _db.StudioVideos
            .AsNoTracking()
            .Where(v => v.Section == normalizedSection && v.IsActive)
            .OrderBy(v => v.DisplayOrder)
            .ThenByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);

        return videos.Select(MapToResponse).ToList();
    }

    public async Task<IReadOnlyList<StudioVideoResponse>> GetAdminVideosAsync(
        string? section = null,
        bool? includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        var query = _db.StudioVideos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(section))
        {
            var normalizedSection = NormalizeSection(section);
            query = query.Where(v => v.Section == normalizedSection);
        }

        if (includeInactive == false)
        {
            query = query.Where(v => v.IsActive);
        }

        var videos = await query
            .OrderBy(v => v.Section)
            .ThenBy(v => v.DisplayOrder)
            .ThenByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);

        return videos.Select(MapToResponse).ToList();
    }

    public async Task<StudioVideoResponse> GetVideoByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var video = await _db.StudioVideos
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Video with ID '{id}' was not found.");

        return MapToResponse(video);
    }

    public async Task<StudioVideoResponse> UploadVideoAsync(
        IFormFile file,
        string section,
        string title,
        string? description,
        int? displayOrder,
        Guid? uploadedByUserId,
        CancellationToken cancellationToken = default)
    {
        ValidateVideoFile(file, section);

        var normalizedSection = NormalizeSection(section);
        var sectionFolder = normalizedSection == "ShortVideos" ? "short-videos" : "gallery";

        // Upload to Cloudflare R2
        using var stream = file.OpenReadStream();
        var uploadResult = await _r2Service.UploadAsync(
            stream,
            file.FileName,
            file.ContentType ?? "video/mp4",
            $"ethos-videos/{sectionFolder}",
            cancellationToken);

        // Auto-assign display order if not specified
        var order = displayOrder ?? 0;
        if (!displayOrder.HasValue)
        {
            var maxOrder = await _db.StudioVideos
                .Where(v => v.Section == normalizedSection)
                .Select(v => (int?)v.DisplayOrder)
                .MaxAsync(cancellationToken);
            order = (maxOrder ?? 0) + 1;
        }

        var video = new StudioVideo
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Section = normalizedSection,
            ObjectKey = uploadResult.ObjectKey,
            PublicUrl = uploadResult.PublicUrl,
            DisplayOrder = order,
            IsActive = true,
            FileSizeBytes = uploadResult.FileSizeBytes,
            MimeType = file.ContentType ?? "video/mp4",
            UploadedByUserId = uploadedByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.StudioVideos.Add(video);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "New {Section} video uploaded: '{Title}' (ID: {VideoId}, ObjectKey: {ObjectKey})",
            video.Section, video.Title, video.Id, video.ObjectKey);

        return MapToResponse(video);
    }

    public async Task<StudioVideoResponse> ReplaceVideoFileAsync(
        Guid id,
        IFormFile newFile,
        Guid? updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        var video = await _db.StudioVideos
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Video with ID '{id}' was not found.");

        ValidateVideoFile(newFile, video.Section);

        var sectionFolder = video.Section == "ShortVideos" ? "short-videos" : "gallery";

        // 1. Delete old object from Cloudflare R2
        if (!string.IsNullOrWhiteSpace(video.ObjectKey))
        {
            try
            {
                await _r2Service.DeleteAsync(video.ObjectKey, cancellationToken);
                _logger.LogInformation("Deleted previous video object from R2: {OldKey}", video.ObjectKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old object '{OldKey}' from R2 during replacement.", video.ObjectKey);
            }
        }

        // 2. Upload replacement file to Cloudflare R2
        using var stream = newFile.OpenReadStream();
        var uploadResult = await _r2Service.UploadAsync(
            stream,
            newFile.FileName,
            newFile.ContentType ?? "video/mp4",
            $"ethos-videos/{sectionFolder}",
            cancellationToken);

        // 3. Update database record
        video.ObjectKey = uploadResult.ObjectKey;
        video.PublicUrl = uploadResult.PublicUrl;
        video.FileSizeBytes = uploadResult.FileSizeBytes;
        video.MimeType = newFile.ContentType ?? "video/mp4";
        video.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Replaced video file for ID {VideoId} with new object {NewKey}", video.Id, video.ObjectKey);
        return MapToResponse(video);
    }

    public async Task<StudioVideoResponse> UpdateVideoMetadataAsync(
        Guid id,
        VideoUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var video = await _db.StudioVideos
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Video with ID '{id}' was not found.");

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            video.Title = request.Title.Trim();
        }

        video.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (request.DisplayOrder.HasValue)
        {
            video.DisplayOrder = request.DisplayOrder.Value;
        }

        if (request.IsActive.HasValue)
        {
            video.IsActive = request.IsActive.Value;
        }

        video.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return MapToResponse(video);
    }

    public async Task<StudioVideoResponse> ToggleActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var video = await _db.StudioVideos
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Video with ID '{id}' was not found.");

        video.IsActive = isActive;
        video.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Toggled active status of video {VideoId} to {IsActive}", video.Id, isActive);

        return MapToResponse(video);
    }

    public async Task ReorderVideosAsync(
        List<VideoOrderItemDto> items,
        CancellationToken cancellationToken = default)
    {
        if (items == null || items.Count == 0) return;

        var ids = items.Select(i => i.Id).ToList();
        var videos = await _db.StudioVideos
            .Where(v => ids.Contains(v.Id))
            .ToListAsync(cancellationToken);

        var orderMap = items.ToDictionary(i => i.Id, i => i.DisplayOrder);

        foreach (var video in videos)
        {
            if (orderMap.TryGetValue(video.Id, out var newOrder))
            {
                video.DisplayOrder = newOrder;
                video.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteVideoPermanentlyAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var video = await _db.StudioVideos
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Video with ID '{id}' was not found.");

        // 1. Permanently delete from Cloudflare R2 storage
        if (!string.IsNullOrWhiteSpace(video.ObjectKey))
        {
            try
            {
                await _r2Service.DeleteAsync(video.ObjectKey, cancellationToken);
                _logger.LogInformation("Permanently removed video from Cloudflare R2: {ObjectKey}", video.ObjectKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete video object from R2: {ObjectKey}", video.ObjectKey);
            }
        }

        // 2. Delete metadata record from database
        _db.StudioVideos.Remove(video);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permanently deleted video record {VideoId} ('{Title}')", video.Id, video.Title);
    }

    private static void ValidateVideoFile(IFormFile file, string section)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("A valid video file must be provided.");
        }

        var normalizedSection = NormalizeSection(section);
        var maxBytes = normalizedSection == "ShortVideos" ? ShortVideosMaxSizeBytes : GalleryMaxSizeBytes;
        var maxMb = maxBytes / (1024 * 1024);

        if (file.Length > maxBytes)
        {
            throw new ArgumentException(
                $"File exceeds the maximum permitted size of {maxMb} MB for {normalizedSection}. (Actual size: {file.Length / (1024.0 * 1024.0):F1} MB)");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".mp4", ".mov", ".webm", ".m4v" };
        if (!allowedExtensions.Contains(ext))
        {
            throw new ArgumentException("Invalid file format. Supported video formats: MP4 (.mp4), WebM (.webm), MOV (.mov).");
        }
    }

    private static string NormalizeSection(string section)
    {
        if (string.IsNullOrWhiteSpace(section)) return "ShortVideos";

        var s = section.Trim().ToLowerInvariant();
        if (s.Contains("gallery")) return "Gallery";
        return "ShortVideos";
    }

    private static StudioVideoResponse MapToResponse(StudioVideo v)
    {
        return new StudioVideoResponse
        {
            Id = v.Id,
            Title = v.Title,
            Description = v.Description,
            Section = v.Section,
            ObjectKey = v.ObjectKey,
            PublicUrl = v.PublicUrl,
            ThumbnailUrl = v.ThumbnailUrl,
            DisplayOrder = v.DisplayOrder,
            IsActive = v.IsActive,
            DurationSeconds = v.DurationSeconds,
            FileSizeBytes = v.FileSizeBytes,
            MimeType = v.MimeType,
            CreatedAt = v.CreatedAt,
            UpdatedAt = v.UpdatedAt
        };
    }
}
