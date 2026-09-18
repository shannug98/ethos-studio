using System.Text.RegularExpressions;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Storage;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Media;
using Ethos.Api.Domain.Constants;
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
    private readonly IMediaCacheService _cacheService;
    private readonly ILogger<MediaService> _logger;

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm" };

    private const long MaxImageSizeBytes          = 35L  * 1024 * 1024;
    private const long MaxVideoSizeBytes          = 100L * 1024 * 1024;
    private const long MaxScrollingVideoSizeBytes = 35L  * 1024 * 1024;
    private const long MaxReelsSizeBytes          = 35L  * 1024 * 1024;
    private const long MaxTrainerImageSizeBytes   = 35L  * 1024 * 1024;
    private const double MaxScrollingVideoDurationSeconds = 40.0;
    private const double MaxReelsDurationSeconds          = 40.0;

    public MediaService(AppDbContext db, ICloudflareR2StorageService r2Service, IAdminAuditService auditService, IMediaCacheService cacheService, ILogger<MediaService> logger)
    {
        _db = db; _r2Service = r2Service; _auditService = auditService; _cacheService = cacheService; _logger = logger;
    }

    public async Task<MediaUploadResponse> UploadMediaAsync(
        IFormFile file, IEnumerable<string> sections, string? mediaType,
        string? title, string? caption, string? altText, string? focalPoint,
        string? category, string? layoutType, int? displayOrder,
        bool? isPublished, bool? isFeatured, double? clientDurationSeconds,
        Guid? adminUserId, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("No file was uploaded or file is empty.");

        var requestedSections = (sections ?? Enumerable.Empty<string>())
            .Select(s => s?.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (requestedSections.Count == 0) requestedSections.Add(MediaConstants.Sections.Draft);

        var normalizedSections = new List<string>();
        foreach (var raw in requestedSections)
        {
            if (!MediaConstants.TryNormalizeSection(raw, out var ns))
                throw new ArgumentException($"Invalid section '{raw}'. Allowed: {string.Join(", ", MediaConstants.Sections.All)}");
            if (!normalizedSections.Contains(ns, StringComparer.OrdinalIgnoreCase)) normalizedSections.Add(ns);
        }

        if (!MediaConstants.TryNormalizeLayoutType(layoutType, out var normalizedLayout))
            throw new ArgumentException($"Invalid layout type '{layoutType}'.");
        if (!MediaConstants.TryNormalizeCategory(category, out var normalizedCategory))
            throw new ArgumentException($"Invalid category '{category}'.");

        var rawExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(rawExtension) || (!AllowedImageExtensions.Contains(rawExtension) && !AllowedVideoExtensions.Contains(rawExtension)))
            throw new ArgumentException($"Unsupported file extension '{rawExtension}'. Allowed: JPG, PNG, WEBP, MP4, WEBM.");

        var detectedType = DetectFileSignature(file) ?? throw new ArgumentException("File signature verification failed.");

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            if (!MediaConstants.TryNormalizeMediaType(mediaType, out var normalizedMediaType))
                throw new ArgumentException($"Invalid media type '{mediaType}'.");
            if (normalizedMediaType != detectedType)
                throw new ArgumentException($"Selected media type '{normalizedMediaType}' does not match file signature (detected: {detectedType}).");
        }

        var finalMediaType = detectedType;

        foreach (var section in normalizedSections)
            ValidateSectionConstraints(section, finalMediaType, file.Length, clientDurationSeconds, normalizedLayout);

        if (normalizedSections.Contains(MediaConstants.Sections.HomepageReels, StringComparer.OrdinalIgnoreCase))
            normalizedLayout = MediaConstants.LayoutTypes.Portrait;

        var sanitizedStem = Regex.Replace(Path.GetFileNameWithoutExtension(file.FileName), @"[^a-zA-Z0-9_-]", "_");
        if (sanitizedStem.Length > 40) sanitizedStem = sanitizedStem[..40];
        var primarySection = normalizedSections[0];
        var safeObjectKey  = $"media/{primarySection.ToLowerInvariant()}/{Guid.NewGuid():N}-{sanitizedStem}{rawExtension}";

        var contentType = finalMediaType == MediaConstants.MediaTypes.Video
            ? (rawExtension == ".webm" ? "video/webm" : "video/mp4")
            : (rawExtension == ".webp" ? "image/webp" : (rawExtension == ".png" ? "image/png" : "image/jpeg"));

        R2UploadResult uploadResult;
        using (var stream = file.OpenReadStream())
            uploadResult = await _r2Service.UploadAsync(stream, safeObjectKey, contentType, primarySection.ToLowerInvariant(), cancellationToken);

        var itemTitle = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title.Trim();
        var itemAlt   = string.IsNullOrWhiteSpace(altText) ? itemTitle : altText.Trim();
        var itemFocal = string.IsNullOrWhiteSpace(focalPoint) ? "center" : focalPoint.Trim();

        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(), Title = itemTitle, Caption = caption?.Trim() ?? string.Empty,
            AltText = itemAlt, FocalPoint = itemFocal, Category = normalizedCategory, LayoutType = normalizedLayout,
            IsArchived = false, ObjectKey = uploadResult.ObjectKey, OriginalFileName = Path.GetFileName(file.FileName),
            MediaType = finalMediaType, MimeType = uploadResult.ContentType, FileSizeBytes = uploadResult.FileSizeBytes,
            PublicUrl = uploadResult.PublicUrl, ThumbnailUrl = uploadResult.PublicUrl, DurationSeconds = clientDurationSeconds,
            Visibility = "Public", ApprovalStatus = "Approved", UploadedByUserId = adminUserId,
            Checksum = uploadResult.Checksum, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };

        var baseOrder = displayOrder ?? 1;
        var placements = normalizedSections.Select((sec, i) => new MediaPlacement
        {
            Id = Guid.NewGuid(), MediaItemId = mediaItem.Id, Section = sec,
            DisplayOrder = baseOrder + i, IsPublished = isPublished ?? true, IsFeatured = isFeatured ?? false, CreatedAt = DateTime.UtcNow,
        }).ToList();
        mediaItem.Placements = placements;

        try { _db.MediaItems.Add(mediaItem); await _db.SaveChangesAsync(cancellationToken); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist media metadata for {ObjectKey}. Purging R2 file.", uploadResult.ObjectKey);
            try { await _r2Service.DeleteAsync(uploadResult.ObjectKey, cancellationToken); } catch (Exception r2Ex) { _logger.LogError(r2Ex, "Failed to delete orphaned R2 file {ObjectKey}", uploadResult.ObjectKey); }
            throw;
        }

        _cacheService.InvalidateAllMediaCache();
        if (adminUserId.HasValue)
            await _auditService.LogActionAsync(adminUserId.Value, "MEDIA_UPLOAD", "MEDIA", "MediaItem", mediaItem.Id,
                reason: $"Uploaded {finalMediaType} to [{string.Join(", ", normalizedSections)}]: '{itemTitle}'");

        return new MediaUploadResponse
        {
            Id = mediaItem.Id, Title = mediaItem.Title, ObjectKey = mediaItem.ObjectKey, PublicUrl = mediaItem.PublicUrl,
            ThumbnailUrl = mediaItem.ThumbnailUrl, MediaType = mediaItem.MediaType, Category = mediaItem.Category,
            LayoutType = mediaItem.LayoutType, FileSizeBytes = mediaItem.FileSizeBytes, Checksum = mediaItem.Checksum ?? string.Empty,
            Placements = placements.Select(MapPlacement).ToList(),
        };
    }

    public Task<IReadOnlyList<PublicMediaResponse>> GetPublicMediaAsync(string? section, string? category, string? mediaType, CancellationToken cancellationToken = default)
    {
        var cacheKey = _cacheService.NormalizeCacheKey(section, category, mediaType);
        return _cacheService.GetOrSetAsync(cacheKey, async () =>
        {
            var now = DateTime.UtcNow;
            var query = _db.MediaPlacements.AsNoTracking()
                .Where(p => p.IsPublished && (p.VisibleFromUtc == null || p.VisibleFromUtc <= now) && (p.VisibleUntilUtc == null || p.VisibleUntilUtc >= now))
                .Join(_db.MediaItems.AsNoTracking().Where(m => !m.IsDeleted && !m.IsArchived && m.Visibility == "Public" && m.ApprovalStatus == "Approved"),
                    p => p.MediaItemId, m => m.Id, (p, m) => new { Placement = p, Item = m });

            if (!string.IsNullOrWhiteSpace(section) && !section.Equals("all", StringComparison.OrdinalIgnoreCase))
                if (MediaConstants.TryNormalizeSection(section, out var ns)) query = query.Where(x => x.Placement.Section == ns);

            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
                if (MediaConstants.TryNormalizeCategory(category, out var nc)) query = query.Where(x => x.Item.Category == nc);

            if (!string.IsNullOrWhiteSpace(mediaType) && !mediaType.Equals("all", StringComparison.OrdinalIgnoreCase))
                if (MediaConstants.TryNormalizeMediaType(mediaType, out var nt)) query = query.Where(x => x.Item.MediaType == nt);

            var items = await query.OrderBy(x => x.Placement.DisplayOrder).ThenByDescending(x => x.Item.CreatedAt)
                .Select(x => new PublicMediaResponse
                {
                    Id = x.Item.Id, PlacementId = x.Placement.Id, Title = x.Item.Title, Caption = x.Item.Caption,
                    AltText = string.IsNullOrWhiteSpace(x.Item.AltText) ? x.Item.Title : x.Item.AltText,
                    Category = x.Item.Category, LayoutType = x.Item.LayoutType, DisplayOrder = x.Placement.DisplayOrder,
                    IsFeatured = x.Placement.IsFeatured, PublicUrl = x.Item.PublicUrl,
                    OptimizedUrl = x.Item.OptimizedUrl ?? x.Item.PublicUrl, ThumbnailUrl = x.Item.ThumbnailUrl ?? x.Item.PublicUrl,
                    MediaType = x.Item.MediaType, Section = x.Placement.Section, FocalPoint = x.Item.FocalPoint,
                    TargetUrl = x.Item.TargetUrl, Width = x.Item.Width, Height = x.Item.Height, DurationSeconds = x.Item.DurationSeconds,
                }).ToListAsync(cancellationToken);
            return (IReadOnlyList<PublicMediaResponse>)items;
        });
    }

    public async Task<PagedResult<MediaItemResponse>> GetAdminMediaPagedAsync(int page, int pageSize, string? section, string? mediaType, string? category, bool? isPublished, bool? isArchived, CancellationToken cancellationToken = default)
    {
        var query = _db.MediaItems.AsNoTracking().Include(m => m.Placements).Where(m => !m.IsDeleted);
        if (isArchived.HasValue) query = query.Where(m => m.IsArchived == isArchived.Value);
        if (!string.IsNullOrWhiteSpace(mediaType) && !mediaType.Equals("all", StringComparison.OrdinalIgnoreCase))
            if (MediaConstants.TryNormalizeMediaType(mediaType, out var t)) query = query.Where(m => m.MediaType == t);
        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
            if (MediaConstants.TryNormalizeCategory(category, out var c)) query = query.Where(m => m.Category == c);
        if (!string.IsNullOrWhiteSpace(section) && !section.Equals("all", StringComparison.OrdinalIgnoreCase))
            if (MediaConstants.TryNormalizeSection(section, out var s)) query = query.Where(m => m.Placements.Any(p => p.Section == s));
        if (isPublished.HasValue) query = query.Where(m => m.Placements.Any(p => p.IsPublished == isPublished.Value));

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(m => m.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<MediaItemResponse> { Items = items.Select(MapToResponse).ToList(), TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<MediaItemResponse> UpdateMediaAsync(Guid id, AdminUpdateMediaRequest request, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        var media = await _db.MediaItems.Include(m => m.Placements).FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, cancellationToken)
            ?? throw new ArgumentException("Media item not found.");

        if (request.Title    != null) media.Title    = request.Title.Trim();
        if (request.Caption  != null) media.Caption  = request.Caption.Trim();
        if (request.AltText  != null) media.AltText  = request.AltText.Trim();
        if (request.FocalPoint != null) media.FocalPoint = request.FocalPoint.Trim();
        if (request.TargetUrl  != null) media.TargetUrl  = request.TargetUrl.Trim();
        if (request.WorkshopId.HasValue) media.WorkshopId = request.WorkshopId.Value;
        if (request.Category != null) { if (MediaConstants.TryNormalizeCategory(request.Category, out var cat)) media.Category = cat; else throw new ArgumentException($"Invalid category '{request.Category}'."); }
        if (request.LayoutType != null) { if (MediaConstants.TryNormalizeLayoutType(request.LayoutType, out var lay)) media.LayoutType = lay; else throw new ArgumentException($"Invalid layout '{request.LayoutType}'."); }

        media.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        _cacheService.InvalidateAllMediaCache();
        await _auditService.LogActionAsync(adminUserId, "MEDIA_UPDATE", "MEDIA", "MediaItem", media.Id, reason: $"Updated metadata for '{media.Title}'");
        return MapToResponse(media);
    }

    public async Task<AdminPlacementResponse> AddPlacementAsync(Guid mediaItemId, AdminPlacementRequest request, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        var media = await _db.MediaItems.Include(m => m.Placements).FirstOrDefaultAsync(m => m.Id == mediaItemId && !m.IsDeleted, cancellationToken)
            ?? throw new ArgumentException("Media item not found.");
        if (!MediaConstants.TryNormalizeSection(request.Section, out var section))
            throw new ArgumentException($"Invalid section '{request.Section}'.");
        ValidateSectionConstraints(section, media.MediaType, media.FileSizeBytes, media.DurationSeconds, media.LayoutType);
        if (media.Placements.Any(p => p.Section.Equals(section, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"This asset already has a placement in '{section}'.");

        var placement = new MediaPlacement { Id = Guid.NewGuid(), MediaItemId = mediaItemId, Section = section,
            DisplayOrder = request.DisplayOrder, IsPublished = request.IsPublished, IsFeatured = request.IsFeatured,
            VisibleFromUtc = request.VisibleFromUtc, VisibleUntilUtc = request.VisibleUntilUtc, CreatedAt = DateTime.UtcNow };
        _db.MediaPlacements.Add(placement);
        await _db.SaveChangesAsync(cancellationToken);
        _cacheService.InvalidateAllMediaCache();
        await _auditService.LogActionAsync(adminUserId, "MEDIA_PLACEMENT_ADD", "MEDIA", "MediaPlacement", placement.Id, reason: $"Added placement '{section}' for '{media.Title}'");
        return MapPlacement(placement);
    }

    public async Task<AdminPlacementResponse> UpdatePlacementAsync(Guid placementId, AdminPlacementRequest request, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        var placement = await _db.MediaPlacements.Include(p => p.MediaItem).FirstOrDefaultAsync(p => p.Id == placementId, cancellationToken)
            ?? throw new ArgumentException("Placement not found.");
        if (!MediaConstants.TryNormalizeSection(request.Section, out var section))
            throw new ArgumentException($"Invalid section '{request.Section}'.");
        if (!section.Equals(placement.Section, StringComparison.OrdinalIgnoreCase))
        {
            ValidateSectionConstraints(section, placement.MediaItem.MediaType, placement.MediaItem.FileSizeBytes, placement.MediaItem.DurationSeconds, placement.MediaItem.LayoutType);
            var hasDup = await _db.MediaPlacements.AnyAsync(p => p.MediaItemId == placement.MediaItemId && p.Section == section && p.Id != placementId, cancellationToken);
            if (hasDup) throw new InvalidOperationException($"This asset already has a placement in '{section}'.");
            placement.Section = section;
        }
        placement.DisplayOrder = request.DisplayOrder; placement.IsPublished = request.IsPublished;
        placement.IsFeatured = request.IsFeatured; placement.VisibleFromUtc = request.VisibleFromUtc; placement.VisibleUntilUtc = request.VisibleUntilUtc;
        await _db.SaveChangesAsync(cancellationToken);
        _cacheService.InvalidateAllMediaCache();
        await _auditService.LogActionAsync(adminUserId, "MEDIA_PLACEMENT_UPDATE", "MEDIA", "MediaPlacement", placement.Id, reason: $"Updated placement '{placement.Section}' for '{placement.MediaItem.Title}'");
        return MapPlacement(placement);
    }

    public async Task RemovePlacementAsync(Guid placementId, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        var placement = await _db.MediaPlacements.Include(p => p.MediaItem).FirstOrDefaultAsync(p => p.Id == placementId, cancellationToken)
            ?? throw new ArgumentException("Placement not found.");
        var section = placement.Section; var title = placement.MediaItem?.Title ?? "(unknown)";
        _db.MediaPlacements.Remove(placement);
        await _db.SaveChangesAsync(cancellationToken);
        _cacheService.InvalidateAllMediaCache();
        await _auditService.LogActionAsync(adminUserId, "MEDIA_PLACEMENT_REMOVE", "MEDIA", "MediaPlacement", placementId, reason: $"Removed placement '{section}' from '{title}'");
    }

    public async Task<AdminPlacementResponse> TogglePlacementPublishAsync(Guid placementId, bool isPublished, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        var placement = await _db.MediaPlacements.Include(p => p.MediaItem).FirstOrDefaultAsync(p => p.Id == placementId, cancellationToken)
            ?? throw new ArgumentException("Placement not found.");
        placement.IsPublished = isPublished;
        await _db.SaveChangesAsync(cancellationToken);
        _cacheService.InvalidateAllMediaCache();
        await _auditService.LogActionAsync(adminUserId, isPublished ? "MEDIA_PUBLISH" : "MEDIA_UNPUBLISH", "MEDIA", "MediaPlacement", placementId,
            reason: $"Toggled publish={isPublished} for '{placement.MediaItem?.Title}' in section '{placement.Section}'");
        return MapPlacement(placement);
    }

    public async Task<MediaItemResponse> ArchiveMediaAsync(Guid id, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        var media = await _db.MediaItems.Include(m => m.Placements).FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, cancellationToken) ?? throw new ArgumentException("Media item not found.");
        media.IsArchived = true; media.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken); _cacheService.InvalidateAllMediaCache();
        await _auditService.LogActionAsync(adminUserId, "MEDIA_ARCHIVE", "MEDIA", "MediaItem", media.Id, reason: $"Archived media item: '{media.Title}'");
        return MapToResponse(media);
    }

    public async Task<MediaItemResponse> RestoreMediaAsync(Guid id, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        var media = await _db.MediaItems.Include(m => m.Placements).FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, cancellationToken) ?? throw new ArgumentException("Media item not found.");
        media.IsArchived = false; media.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken); _cacheService.InvalidateAllMediaCache();
        await _auditService.LogActionAsync(adminUserId, "MEDIA_RESTORE", "MEDIA", "MediaItem", media.Id, reason: $"Restored media item: '{media.Title}'");
        return MapToResponse(media);
    }

    public async Task ReorderMediaAsync(AdminReorderMediaRequest request, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        if (request?.Items == null || request.Items.Count == 0) return;
        var ids = request.Items.Select(i => i.PlacementId).ToList();
        var placements = await _db.MediaPlacements.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);
        foreach (var item in request.Items) { var t = placements.FirstOrDefault(p => p.Id == item.PlacementId); if (t != null) t.DisplayOrder = item.DisplayOrder; }
        await _db.SaveChangesAsync(cancellationToken); _cacheService.InvalidateAllMediaCache();
        await _auditService.LogActionAsync(adminUserId, "MEDIA_REORDER", "MEDIA", "MediaPlacement", Guid.Empty, reason: $"Reordered {request.Items.Count} placements in '{request.Section}'");
    }

    public async Task DeleteMediaAsync(Guid id, bool permanent, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        var media = await _db.MediaItems.Include(m => m.Placements).FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new ArgumentException("Media item not found.");

        if (permanent)
        {
            // 1. Dependency & Reference Protection (Option B: Block permanent delete if published or referenced)
            var publishedPlacements = media.Placements.Where(p => p.IsPublished).ToList();
            if (publishedPlacements.Count > 0)
            {
                var secNames = string.Join(", ", publishedPlacements.Select(p => p.Section).Distinct());
                throw new InvalidOperationException($"Cannot permanently delete '{media.Title}' because it is currently published in section(s): [{secNames}]. Please hide or unpublish the placement first.");
            }

            bool isWorkshopRef = await _db.Workshops.AnyAsync(w => w.ImageUrl == media.PublicUrl || w.LandscapeImageUrl == media.PublicUrl, cancellationToken);
            if (isWorkshopRef)
            {
                throw new InvalidOperationException($"Cannot permanently delete '{media.Title}' because it is currently used as a Workshop poster/banner image. Please unassign or replace the workshop poster reference first.");
            }

            bool isTrainerRef = await _db.TrainerProfiles.AnyAsync(tp => tp.ProfilePhotoUrl == media.PublicUrl, cancellationToken);
            if (isTrainerRef)
            {
                throw new InvalidOperationException($"Cannot permanently delete '{media.Title}' because it is currently used as a Trainer profile photo. Please unassign or replace the trainer photo reference first.");
            }

            // 2. Collect ALL associated R2 Object Keys (Original, Thumbnail, Poster/Preview)
            var keysToDelete = new List<string>();
            if (!string.IsNullOrWhiteSpace(media.ObjectKey))
            {
                keysToDelete.Add(media.ObjectKey);
            }
            if (!string.IsNullOrWhiteSpace(media.ThumbnailObjectKey) && !keysToDelete.Contains(media.ThumbnailObjectKey))
            {
                keysToDelete.Add(media.ThumbnailObjectKey);
            }
            if (!string.IsNullOrWhiteSpace(media.PosterObjectKey) && !keysToDelete.Contains(media.PosterObjectKey))
            {
                keysToDelete.Add(media.PosterObjectKey);
            }

            // 3. Purge objects from Cloudflare R2
            foreach (var key in keysToDelete)
            {
                try
                {
                    await _r2Service.DeleteAsync(key, cancellationToken);
                    _logger.LogInformation("Successfully purged R2 object {Key} for media {MediaId}", key, media.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete R2 object {Key} for media {MediaId}", key, media.Id);
                }
            }

            // 4. Remove Placements and MediaItem metadata record from DB
            _db.MediaPlacements.RemoveRange(media.Placements);
            _db.MediaItems.Remove(media);
            await _db.SaveChangesAsync(cancellationToken);

            _cacheService.InvalidateAllMediaCache();

            // 5. Audit Log
            await _auditService.LogActionAsync(
                adminUserId,
                "MEDIA_PERMANENT_DELETE",
                "MEDIA",
                "MediaItem",
                media.Id,
                reason: $"Permanently deleted media '{media.Title}' (ID: {media.Id}, Size: {media.FileSizeBytes} bytes, R2 Keys Purged: [{string.Join(", ", keysToDelete)}])");
        }
        else
        {
            media.IsDeleted = true;
            media.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            _cacheService.InvalidateAllMediaCache();
            await _auditService.LogActionAsync(adminUserId, "MEDIA_DELETE", "MEDIA", "MediaItem", media.Id, reason: $"Soft-deleted: '{media.Title}'");
        }
    }

    private static void ValidateSectionConstraints(string section, string mediaType, long fileSizeBytes, double? durationSeconds, string layoutType)
    {
        if (section == MediaConstants.Sections.HomepageReels)
        {
            if (mediaType != MediaConstants.MediaTypes.Video) throw new ArgumentException("HomepageReels only accepts Video files (MP4, 9:16 portrait, max 30s, max 25MB).");
            if (fileSizeBytes > MaxReelsSizeBytes) throw new ArgumentException($"HomepageReels video cannot exceed {MaxReelsSizeBytes / (1024 * 1024)} MB.");
            if (durationSeconds.HasValue && durationSeconds.Value > MaxReelsDurationSeconds) throw new ArgumentException($"HomepageReels videos must be {MaxReelsDurationSeconds}s or shorter (received: {durationSeconds.Value:F1}s).");
        }
        if (section == MediaConstants.Sections.HomepageScrolling && mediaType == MediaConstants.MediaTypes.Video)
        {
            if (fileSizeBytes > MaxScrollingVideoSizeBytes) throw new ArgumentException($"HomepageScrolling video cannot exceed {MaxScrollingVideoSizeBytes / (1024 * 1024)} MB.");
            if (durationSeconds.HasValue && durationSeconds.Value > MaxScrollingVideoDurationSeconds) throw new ArgumentException($"HomepageScrolling video duration cannot exceed {MaxScrollingVideoDurationSeconds}s (received: {durationSeconds.Value:F1}s).");
        }
        if (section == MediaConstants.Sections.GalleryImages && mediaType != MediaConstants.MediaTypes.Image)
            throw new ArgumentException("GalleryImages only accepts image files.");
        if (section == MediaConstants.Sections.GalleryVideos && mediaType != MediaConstants.MediaTypes.Video)
            throw new ArgumentException("GalleryVideos only accepts video files.");
        if (section == MediaConstants.Sections.Trainers && mediaType == MediaConstants.MediaTypes.Image && fileSizeBytes > MaxTrainerImageSizeBytes)
            throw new ArgumentException($"Trainer photos cannot exceed {MaxTrainerImageSizeBytes / (1024 * 1024)} MB.");
        if (mediaType == MediaConstants.MediaTypes.Image && section != MediaConstants.Sections.Trainers && fileSizeBytes > MaxImageSizeBytes)
            throw new ArgumentException($"Image file size ({fileSizeBytes / (1024 * 1024)} MB) exceeds {MaxImageSizeBytes / (1024 * 1024)} MB limit.");
        if (mediaType == MediaConstants.MediaTypes.Video && section != MediaConstants.Sections.HomepageReels && section != MediaConstants.Sections.HomepageScrolling && fileSizeBytes > MaxVideoSizeBytes)
            throw new ArgumentException($"Video file size ({fileSizeBytes / (1024 * 1024)} MB) exceeds {MaxVideoSizeBytes / (1024 * 1024)} MB limit.");
    }

    private static string? DetectFileSignature(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        var header = new byte[12]; int read = stream.Read(header, 0, header.Length);
        if (read < 4) return null;
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return MediaConstants.MediaTypes.Image;
        if (read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return MediaConstants.MediaTypes.Image;
        if (read >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50) return MediaConstants.MediaTypes.Image;
        if (read >= 8 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70) return MediaConstants.MediaTypes.Video;
        if (header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3) return MediaConstants.MediaTypes.Video;
        return null;
    }

    private static AdminPlacementResponse MapPlacement(MediaPlacement p) => new()
    {
        Id = p.Id, Section = p.Section, DisplayOrder = p.DisplayOrder, IsPublished = p.IsPublished,
        IsFeatured = p.IsFeatured, VisibleFromUtc = p.VisibleFromUtc, VisibleUntilUtc = p.VisibleUntilUtc, CreatedAt = p.CreatedAt,
    };

    private static MediaItemResponse MapToResponse(MediaItem m) => new()
    {
        Id = m.Id, Title = m.Title, Caption = m.Caption, AltText = m.AltText, FocalPoint = m.FocalPoint,
        Category = m.Category, LayoutType = m.LayoutType, IsArchived = m.IsArchived, TargetUrl = m.TargetUrl,
        WorkshopId = m.WorkshopId, ObjectKey = m.ObjectKey, OriginalFileName = m.OriginalFileName,
        MediaType = m.MediaType, MimeType = m.MimeType, FileSizeBytes = m.FileSizeBytes,
        Width = m.Width, Height = m.Height, DurationSeconds = m.DurationSeconds,
        PublicUrl = m.PublicUrl, OptimizedUrl = m.OptimizedUrl, ThumbnailUrl = m.ThumbnailUrl,
        Visibility = m.Visibility, ApprovalStatus = m.ApprovalStatus, CreatedAt = m.CreatedAt,
        Placements = m.Placements.Select(MapPlacement).ToList(),
    };
}
