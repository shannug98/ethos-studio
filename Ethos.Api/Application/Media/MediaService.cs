using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Storage;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Media;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Media;

public class MediaService : IMediaService
{
    private readonly AppDbContext _db;
    private readonly ICloudflareR2StorageService _r2Service;
    private readonly IAdminAuditService _auditService;
    private readonly ILogger<MediaService> _logger;

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "video/mp4", "video/webm"
    };

    private const long MaxImageSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const long MaxVideoSizeBytes = 100 * 1024 * 1024; // 100 MB

    public MediaService(
        AppDbContext db,
        ICloudflareR2StorageService r2Service,
        IAdminAuditService auditService,
        ILogger<MediaService> logger)
    {
        _db = db;
        _r2Service = r2Service;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<MediaUploadResponse> UploadMediaAsync(
        IFormFile file,
        string section,
        Guid? adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("No file was uploaded or file is empty.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var contentType = file.ContentType.ToLowerInvariant();

        bool isImage = AllowedImageExtensions.Contains(extension);
        bool isVideo = AllowedVideoExtensions.Contains(extension);

        if (!isImage && !isVideo)
        {
            throw new ArgumentException($"Unsupported file format '{extension}'. Allowed formats: JPG, JPEG, PNG, WEBP, MP4, WEBM.");
        }

        if (!AllowedMimeTypes.Contains(contentType))
        {
            // If browser sent generic octet-stream or mismatched, align with extension if valid
            if (isImage && extension == ".webp") contentType = "image/webp";
            else if (isImage && (extension == ".jpg" || extension == ".jpeg")) contentType = "image/jpeg";
            else if (isImage && extension == ".png") contentType = "image/png";
            else if (isVideo && extension == ".mp4") contentType = "video/mp4";
            else if (isVideo && extension == ".webm") contentType = "video/webm";
            else throw new ArgumentException($"Invalid MIME type: {contentType}");
        }

        long maxSize = isVideo ? MaxVideoSizeBytes : MaxImageSizeBytes;
        if (file.Length > maxSize)
        {
            throw new ArgumentException($"File size ({file.Length / (1024 * 1024)}MB) exceeds maximum allowed limit of {maxSize / (1024 * 1024)}MB.");
        }

        // Validate file signature (magic bytes)
        using (var checkStream = file.OpenReadStream())
        {
            var header = new byte[12];
            int bytesRead = await checkStream.ReadAsync(header, 0, header.Length, cancellationToken);
            if (bytesRead >= 3)
            {
                if (isImage)
                {
                    bool isJpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
                    bool isPng = bytesRead >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
                    bool isRiffWebp = bytesRead >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                                      header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

                    if (!isJpeg && !isPng && !isRiffWebp)
                    {
                        throw new ArgumentException("Uploaded file does not match a valid image signature.");
                    }
                }
            }
        }

        // Upload to Cloudflare R2
        R2UploadResult uploadResult;
        using (var stream = file.OpenReadStream())
        {
            uploadResult = await _r2Service.UploadAsync(
                stream,
                file.FileName,
                contentType,
                section,
                cancellationToken);
        }

        // Persist metadata in database
        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            ObjectKey = uploadResult.ObjectKey,
            OriginalFileName = Path.GetFileName(file.FileName),
            MediaType = isVideo ? "video" : "image",
            Section = string.IsNullOrWhiteSpace(section) ? "general" : section.Trim().ToLowerInvariant(),
            MimeType = uploadResult.ContentType,
            FileSizeBytes = uploadResult.FileSizeBytes,
            PublicUrl = uploadResult.PublicUrl,
            Visibility = "Public",
            ApprovalStatus = "Approved",
            UploadedByUserId = adminUserId,
            Checksum = uploadResult.Checksum,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.MediaItems.Add(mediaItem);
        await _db.SaveChangesAsync(cancellationToken);

        if (adminUserId.HasValue)
        {
            _auditService.AddAuditLog(
                adminUserId.Value,
                "MEDIA_UPLOAD",
                "MediaItem",
                mediaItem.Id,
                $"Uploaded {mediaItem.MediaType} to {mediaItem.Section}: {mediaItem.OriginalFileName} ({uploadResult.ObjectKey})");
        }

        return new MediaUploadResponse
        {
            Id = mediaItem.Id,
            ObjectKey = mediaItem.ObjectKey,
            PublicUrl = mediaItem.PublicUrl,
            MediaType = mediaItem.MediaType,
            Section = mediaItem.Section,
            FileSizeBytes = mediaItem.FileSizeBytes,
            Checksum = mediaItem.Checksum ?? string.Empty
        };
    }

    public async Task<IReadOnlyList<MediaItemResponse>> GetPublicMediaBySectionAsync(
        string section,
        CancellationToken cancellationToken = default)
    {
        var sanitizedSection = (section ?? "").Trim().ToLowerInvariant();

        var query = _db.MediaItems.AsNoTracking()
            .Where(m => !m.IsDeleted && m.Visibility == "Public" && m.ApprovalStatus == "Approved");

        if (!string.IsNullOrWhiteSpace(sanitizedSection) && sanitizedSection != "all")
        {
            query = query.Where(m => m.Section == sanitizedSection);
        }

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MediaItemResponse
            {
                Id = m.Id,
                ObjectKey = m.ObjectKey,
                OriginalFileName = m.OriginalFileName,
                MediaType = m.MediaType,
                Section = m.Section,
                MimeType = m.MimeType,
                FileSizeBytes = m.FileSizeBytes,
                Width = m.Width,
                Height = m.Height,
                DurationSeconds = m.DurationSeconds,
                PublicUrl = m.PublicUrl,
                Visibility = m.Visibility,
                ApprovalStatus = m.ApprovalStatus,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return items;
    }

    public async Task<PagedResult<MediaItemResponse>> GetAdminMediaPagedAsync(
        int page,
        int pageSize,
        string? section,
        string? mediaType,
        CancellationToken cancellationToken = default)
    {
        var query = _db.MediaItems.AsNoTracking()
            .Where(m => !m.IsDeleted);

        if (!string.IsNullOrWhiteSpace(section))
        {
            var s = section.Trim().ToLowerInvariant();
            query = query.Where(m => m.Section == s);
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            var t = mediaType.Trim().ToLowerInvariant();
            query = query.Where(m => m.MediaType == t);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MediaItemResponse
            {
                Id = m.Id,
                ObjectKey = m.ObjectKey,
                OriginalFileName = m.OriginalFileName,
                MediaType = m.MediaType,
                Section = m.Section,
                MimeType = m.MimeType,
                FileSizeBytes = m.FileSizeBytes,
                Width = m.Width,
                Height = m.Height,
                DurationSeconds = m.DurationSeconds,
                PublicUrl = m.PublicUrl,
                Visibility = m.Visibility,
                ApprovalStatus = m.ApprovalStatus,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<MediaItemResponse>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task DeleteMediaAsync(
        Guid id,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var media = await _db.MediaItems.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (media == null)
        {
            throw new ArgumentException("Media item not found.");
        }

        media.IsDeleted = true;
        media.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // Delete from R2 asynchronously
        try
        {
            await _r2Service.DeleteAsync(media.ObjectKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete R2 object {ObjectKey} during media item deletion.", media.ObjectKey);
        }

        _auditService.AddAuditLog(
            adminUserId,
            "MEDIA_DELETE",
            "MediaItem",
            media.Id,
            $"Deleted media item: {media.OriginalFileName} ({media.ObjectKey})");
    }
}
