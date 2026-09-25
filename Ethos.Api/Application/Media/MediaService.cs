using System.Text;
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
using Microsoft.EntityFrameworkCore.Storage;

namespace Ethos.Api.Application.Media;

public class MediaService : IMediaService
{
    private readonly AppDbContext _db;
    private readonly ICloudflareR2StorageService _r2Service;
    private readonly IAdminAuditService _auditService;
    private readonly IMediaCacheService _cacheService;
    private readonly ILogger<MediaService> _logger;

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm", ".mov" };

    private static readonly HashSet<string> FixedSlotSections = new(StringComparer.OrdinalIgnoreCase)
    {
        MediaConstants.Sections.HomepageScrolling,
        MediaConstants.Sections.AboutEthos,
        MediaConstants.Sections.Founders,
        MediaConstants.Sections.Trainers,
        MediaConstants.Sections.Events,
        MediaConstants.Sections.HomepageReels,
        MediaConstants.Sections.GallerySlideshow,
        MediaConstants.Sections.GalleryVideos
    };

    private static bool IsFixedSlotSection(string section) => FixedSlotSections.Contains(section);

    // Exact binary byte limits: 1 MiB = 1,048,576 bytes
    private const long MaxImageSizeBytes          = 5L   * 1024 * 1024;  // 5,242,880 bytes (5 MB)
    private const long MaxVideoSizeBytes          = 100L * 1024 * 1024;  // 104,857,600 bytes (100 MB)
    private const long MaxScrollingVideoSizeBytes = 100L * 1024 * 1024;  // 104,857,600 bytes (100 MB)
    private const long MaxReelsSizeBytes          = 100L * 1024 * 1024;  // 104,857,600 bytes (100 MB)
    private const long MaxGalleryVideoSizeBytes   = 500L * 1024 * 1024;  // 524,288,000 bytes (500 MB)
    private const long MaxTrainerImageSizeBytes   = 5L   * 1024 * 1024;  // 5,242,880 bytes (5 MB)
    private const double MaxScrollingVideoDurationSeconds = 60.0;
    private const double MaxReelsDurationSeconds          = 60.0;

    private readonly IMediaFastStartService _fastStartService;

    public MediaService(
        AppDbContext db,
        ICloudflareR2StorageService r2Service,
        IAdminAuditService auditService,
        IMediaCacheService cacheService,
        IMediaFastStartService fastStartService,
        ILogger<MediaService> logger)
    {
        _db = db;
        _r2Service = r2Service;
        _auditService = auditService;
        _cacheService = cacheService;
        _fastStartService = fastStartService;
        _logger = logger;
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

        bool isStreamingSection = normalizedSections.Any(s =>
            s.Equals(MediaConstants.Sections.HomepageScrolling, StringComparison.OrdinalIgnoreCase) ||
            s.Equals(MediaConstants.Sections.HomepageReels, StringComparison.OrdinalIgnoreCase));

        R2UploadResult uploadResult;
        string? tempOptimizedPath = null;

        try
        {
            Stream uploadStream;
            if (isStreamingSection && finalMediaType == MediaConstants.MediaTypes.Video && _fastStartService.IsAvailable && (rawExtension == ".mp4" || rawExtension == ".mov"))
            {
                tempOptimizedPath = Path.Combine(Path.GetTempPath(), $"ethos_faststart_{Guid.NewGuid():N}.mp4");
                using var sourceStream = file.OpenReadStream();
                var fastResult = await _fastStartService.OptimizeStreamAsync(sourceStream, tempOptimizedPath, cancellationToken);
                if (fastResult.Success && File.Exists(tempOptimizedPath))
                {
                    uploadStream = new FileStream(tempOptimizedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    _logger.LogInformation("[MediaService] Video upload successfully optimized with FastStart ({Size:N0} bytes).", fastResult.FileSizeBytes);
                }
                else
                {
                    _logger.LogWarning("[MediaService] FastStart optimization failed: {Error}. Falling back safely to validated original upload.", fastResult.ErrorMessage);
                    uploadStream = file.OpenReadStream();
                }
            }
            else
            {
                uploadStream = file.OpenReadStream();
            }

            using (uploadStream)
            {
                uploadResult = await _r2Service.UploadAsync(uploadStream, safeObjectKey, contentType, primarySection.ToLowerInvariant(), cancellationToken);
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempOptimizedPath) && File.Exists(tempOptimizedPath))
            {
                try { File.Delete(tempOptimizedPath); } catch { }
            }
        }

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

        var isRelational = _db.Database.IsRelational();
        IDbContextTransaction? tx = null;
        var orphanedR2KeysToDelete = new List<string>();

        try
        {
            if (isRelational)
            {
                tx = await _db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    // Advisory lock on PostgreSQL for concurrent HomepageReels uploads
                    await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('HomepageReels'));", cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Advisory transaction lock skipped (unsupported database provider).");
                }
            }

            // Atomic slot supersession for fixed-slot / fixed-limit sections
            foreach (var placement in placements)
            {
                if (IsFixedSlotSection(placement.Section))
                {
                    var existingPlacement = await _db.MediaPlacements
                        .Include(p => p.MediaItem)
                        .FirstOrDefaultAsync(p => p.Section == placement.Section && p.DisplayOrder == placement.DisplayOrder, cancellationToken);

                    if (existingPlacement != null)
                    {
                        _db.MediaPlacements.Remove(existingPlacement);

                        var mId = existingPlacement.MediaItemId;
                        var hasOtherPlacements = await _db.MediaPlacements
                            .AnyAsync(p => p.MediaItemId == mId && p.Id != existingPlacement.Id, cancellationToken);

                        var isReferencedByWorkshop = await _db.Workshops
                            .AnyAsync(w => w.ImageUrl == existingPlacement.MediaItem.PublicUrl || w.LandscapeImageUrl == existingPlacement.MediaItem.PublicUrl, cancellationToken);

                        var isReferencedByTrainer = await _db.TrainerProfiles
                            .AnyAsync(tp => tp.ProfilePhotoUrl == existingPlacement.MediaItem.PublicUrl, cancellationToken);

                        if (!hasOtherPlacements && !isReferencedByWorkshop && !isReferencedByTrainer)
                        {
                            _db.MediaItems.Remove(existingPlacement.MediaItem);
                            if (!string.IsNullOrWhiteSpace(existingPlacement.MediaItem.ObjectKey))
                                orphanedR2KeysToDelete.Add(existingPlacement.MediaItem.ObjectKey);
                            if (!string.IsNullOrWhiteSpace(existingPlacement.MediaItem.ThumbnailObjectKey))
                                orphanedR2KeysToDelete.Add(existingPlacement.MediaItem.ThumbnailObjectKey);
                            if (!string.IsNullOrWhiteSpace(existingPlacement.MediaItem.PosterObjectKey))
                                orphanedR2KeysToDelete.Add(existingPlacement.MediaItem.PosterObjectKey);
                        }
                    }
                }
            }

            _db.MediaItems.Add(mediaItem);
            await _db.SaveChangesAsync(cancellationToken);

            if (tx != null)
            {
                await tx.CommitAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            if (tx != null) await tx.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to persist media metadata for {ObjectKey}. Purging R2 file.", uploadResult.ObjectKey);
            try { await _r2Service.DeleteAsync(uploadResult.ObjectKey, cancellationToken); } catch (Exception r2Ex) { _logger.LogError(r2Ex, "Failed to delete orphaned R2 file {ObjectKey}", uploadResult.ObjectKey); }
            throw;
        }

        // Clean up orphaned R2 objects outside transaction
        foreach (var key in orphanedR2KeysToDelete)
        {
            try
            {
                await _r2Service.DeleteAsync(key, cancellationToken);
                _logger.LogInformation("Deleted evicted R2 reel object: {Key}", key);
            }
            catch (Exception r2Ex)
            {
                _logger.LogWarning(r2Ex, "Failed to delete evicted R2 object: {Key}", key);
            }
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
            // 1. Check active foreign entity references
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

            // 2. Check whether the same physical object is referenced by another MediaItem
            var isObjectShared = !string.IsNullOrWhiteSpace(media.ObjectKey) &&
                await _db.MediaItems.AnyAsync(m => m.Id != media.Id &&
                    (m.ObjectKey == media.ObjectKey || m.ThumbnailObjectKey == media.ObjectKey || m.PosterObjectKey == media.ObjectKey),
                    cancellationToken);

            var keysPurged = new List<string>();

            if (!isObjectShared)
            {
                // Collect ALL associated R2 Object Keys (Original, Thumbnail, Poster/Preview)
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
                        keysPurged.Add(key);
                        _logger.LogInformation("Successfully purged R2 object {Key} for media {MediaId}", key, media.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete R2 object {Key} for media {MediaId}", key, media.Id);
                    }
                }
            }
            else
            {
                _logger.LogInformation("Physical R2 object {Key} is referenced by other media item(s). Preserving R2 storage file.", media.ObjectKey);
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
                reason: $"Permanently deleted media '{media.Title}' (ID: {media.Id}, Size: {media.FileSizeBytes} bytes, R2 Keys Purged: [{string.Join(", ", keysPurged)}])");
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
            if (mediaType != MediaConstants.MediaTypes.Video) throw new ArgumentException("HomepageReels only accepts Video files (MP4, WebM, MOV, 9:16 portrait, max 100MB).");
            if (fileSizeBytes > MaxReelsSizeBytes) throw new ArgumentException($"HomepageReels video cannot exceed {MaxReelsSizeBytes / (1024 * 1024)} MB. (Received: {fileSizeBytes / (1024.0 * 1024.0):F2} MB)");
            if (durationSeconds.HasValue && durationSeconds.Value > MaxReelsDurationSeconds) throw new ArgumentException($"HomepageReels videos must be {MaxReelsDurationSeconds}s or shorter (received: {durationSeconds.Value:F1}s).");
        }
        if (section == MediaConstants.Sections.HomepageScrolling && mediaType == MediaConstants.MediaTypes.Video)
        {
            if (fileSizeBytes > MaxScrollingVideoSizeBytes) throw new ArgumentException($"HomepageScrolling video cannot exceed {MaxScrollingVideoSizeBytes / (1024 * 1024)} MB.");
            if (durationSeconds.HasValue && durationSeconds.Value > MaxScrollingVideoDurationSeconds) throw new ArgumentException($"HomepageScrolling video duration cannot exceed {MaxScrollingVideoDurationSeconds}s (received: {durationSeconds.Value:F1}s).");
        }
        if (section == MediaConstants.Sections.GalleryImages && mediaType != MediaConstants.MediaTypes.Image)
            throw new ArgumentException("GalleryImages only accepts image files.");
        if (section == MediaConstants.Sections.GalleryVideos)
        {
            if (mediaType != MediaConstants.MediaTypes.Video) throw new ArgumentException("GalleryVideos only accepts video files.");
            if (fileSizeBytes > MaxGalleryVideoSizeBytes) throw new ArgumentException($"GalleryVideos video cannot exceed {MaxGalleryVideoSizeBytes / (1024 * 1024)} MB. (Received: {fileSizeBytes / (1024.0 * 1024.0):F2} MB)");
        }
        if (section == MediaConstants.Sections.Trainers && mediaType == MediaConstants.MediaTypes.Image && fileSizeBytes > MaxTrainerImageSizeBytes)
            throw new ArgumentException($"Trainer photos cannot exceed {MaxTrainerImageSizeBytes / (1024 * 1024)} MB.");
        if (mediaType == MediaConstants.MediaTypes.Image && fileSizeBytes > MaxImageSizeBytes)
            throw new ArgumentException($"Image file size ({fileSizeBytes / (1024.0 * 1024.0):F2} MB) exceeds the {MaxImageSizeBytes / (1024 * 1024)} MB limit.");
        if (mediaType == MediaConstants.MediaTypes.Video && section != MediaConstants.Sections.HomepageReels && section != MediaConstants.Sections.HomepageScrolling && section != MediaConstants.Sections.GalleryVideos && fileSizeBytes > MaxVideoSizeBytes)
            throw new ArgumentException($"Video file size ({fileSizeBytes / (1024.0 * 1024.0):F2} MB) exceeds the {MaxVideoSizeBytes / (1024 * 1024)} MB limit.");
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

    public Task<PresignGalleryUploadResponse> PresignGalleryVideoUploadAsync(
        PresignGalleryUploadRequest request,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.FileName))
            throw new ArgumentException("File name is required.");

        if (request.FileSizeBytes <= 0)
            throw new ArgumentException("File size must be greater than 0 bytes.");

        if (request.FileSizeBytes > MaxGalleryVideoSizeBytes)
            throw new ArgumentException($"File size ({request.FileSizeBytes / (1024 * 1024)} MB) exceeds the 500 MB limit for Gallery videos.");

        var ext = Path.GetExtension(request.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".mp4", ".webm", ".mov", ".m4v" };
        if (!allowedExts.Contains(ext))
            throw new ArgumentException("Only video formats (.mp4, .webm, .mov) are allowed for Gallery videos.");

        var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "video/mp4" : request.ContentType.ToLowerInvariant();
        if (!contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            contentType = "video/mp4";

        var sanitizedStem = Regex.Replace(Path.GetFileNameWithoutExtension(request.FileName), @"[^a-zA-Z0-9_-]", "_");
        if (sanitizedStem.Length > 40) sanitizedStem = sanitizedStem[..40];

        var objectKey = $"media/galleryvideos/pending/{Guid.NewGuid():N}-{sanitizedStem}{ext}";
        var uploadToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{objectKey}:{DateTime.UtcNow.Ticks}"));
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(30);

        var presignedUrl = _r2Service.GeneratePreSignedPutUrl(objectKey, contentType, TimeSpan.FromMinutes(30));

        _logger.LogInformation("Generated presigned R2 PUT URL for gallery video: {ObjectKey}", objectKey);

        return Task.FromResult(new PresignGalleryUploadResponse
        {
            UploadUrl = presignedUrl,
            ObjectKey = objectKey,
            UploadToken = uploadToken,
            ExpiresAtUtc = expiresAtUtc
        });
    }

    public async Task<MediaUploadResponse> ConfirmGalleryVideoUploadAsync(
        ConfirmGalleryUploadRequest request,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ObjectKey))
            throw new ArgumentException("ObjectKey is required for confirmation.");

        // Security requirement 4: Independently verify R2 object exists and validate actual size / content type!
        var metadata = await _r2Service.GetObjectMetadataAsync(request.ObjectKey, cancellationToken);
        if (metadata == null)
        {
            throw new ArgumentException($"Uploaded object '{request.ObjectKey}' was not found in Cloudflare R2.");
        }

        if (metadata.ContentLength > MaxGalleryVideoSizeBytes)
        {
            try { await _r2Service.DeleteAsync(request.ObjectKey, cancellationToken); } catch { }
            throw new ArgumentException($"Verified file size ({metadata.ContentLength} bytes) exceeds maximum permitted size of {MaxGalleryVideoSizeBytes} bytes (500 MB).");
        }

        if (metadata.ContentLength == 0)
        {
            try { await _r2Service.DeleteAsync(request.ObjectKey, cancellationToken); } catch { }
            throw new ArgumentException("Uploaded file is empty (0 bytes).");
        }

        var publicUrl = _r2Service.GetPublicUrl(request.ObjectKey);
        var title = string.IsNullOrWhiteSpace(request.Title) ? Path.GetFileName(request.ObjectKey) : request.Title.Trim();
        var category = MediaConstants.TryNormalizeCategory(request.Category, out var cat) ? cat : "General";

        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Caption = request.Caption?.Trim() ?? string.Empty,
            AltText = request.AltText?.Trim() ?? title,
            FocalPoint = "center",
            Category = category,
            LayoutType = MediaConstants.LayoutTypes.Featured,
            IsArchived = false,
            ObjectKey = request.ObjectKey,
            OriginalFileName = Path.GetFileName(request.ObjectKey),
            MediaType = MediaConstants.MediaTypes.Video,
            MimeType = string.IsNullOrWhiteSpace(metadata.ContentType) ? "video/mp4" : metadata.ContentType,
            FileSizeBytes = metadata.ContentLength,
            PublicUrl = publicUrl,
            ThumbnailUrl = publicUrl,
            Visibility = "Public",
            ApprovalStatus = "Approved",
            UploadedByUserId = adminUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var targetOrder = request.DisplayOrder ?? 1;
        var existingPlacement = await _db.MediaPlacements
            .Include(p => p.MediaItem)
            .FirstOrDefaultAsync(p => p.Section == MediaConstants.Sections.GalleryVideos && p.DisplayOrder == targetOrder, cancellationToken);

        string? oldObjectKeyToDelete = null;
        if (existingPlacement != null)
        {
            _db.MediaPlacements.Remove(existingPlacement);
            var mId = existingPlacement.MediaItemId;
            var hasOther = await _db.MediaPlacements.AnyAsync(p => p.MediaItemId == mId && p.Id != existingPlacement.Id, cancellationToken);
            if (!hasOther)
            {
                _db.MediaItems.Remove(existingPlacement.MediaItem);
                if (!string.IsNullOrWhiteSpace(existingPlacement.MediaItem.ObjectKey))
                {
                    oldObjectKeyToDelete = existingPlacement.MediaItem.ObjectKey;
                }
            }
        }

        var placement = new MediaPlacement
        {
            Id = Guid.NewGuid(),
            MediaItemId = mediaItem.Id,
            Section = MediaConstants.Sections.GalleryVideos,
            DisplayOrder = targetOrder,
            IsPublished = request.IsPublished ?? true,
            IsFeatured = request.IsFeatured ?? false,
            CreatedAt = DateTime.UtcNow
        };

        mediaItem.Placements = new List<MediaPlacement> { placement };

        _db.MediaItems.Add(mediaItem);
        await _db.SaveChangesAsync(cancellationToken);
        _cacheService.InvalidateAllMediaCache();

        if (!string.IsNullOrWhiteSpace(oldObjectKeyToDelete))
        {
            try { await _r2Service.DeleteAsync(oldObjectKeyToDelete, cancellationToken); } catch { }
        }

        await _auditService.LogActionAsync(
            adminUserId,
            "MEDIA_UPLOAD_PRESIGNED_CONFIRMED",
            "MEDIA",
            "MediaItem",
            mediaItem.Id,
            reason: $"Confirmed 500MB Gallery Video upload: '{mediaItem.Title}' ({metadata.ContentLength} bytes, key: {mediaItem.ObjectKey})");

        return new MediaUploadResponse
        {
            Id = mediaItem.Id,
            Title = mediaItem.Title,
            ObjectKey = mediaItem.ObjectKey,
            PublicUrl = mediaItem.PublicUrl,
            ThumbnailUrl = mediaItem.ThumbnailUrl,
            MediaType = mediaItem.MediaType,
            Category = mediaItem.Category,
            LayoutType = mediaItem.LayoutType,
            FileSizeBytes = mediaItem.FileSizeBytes,
            Checksum = string.Empty,
            Placements = new List<AdminPlacementResponse> { MapPlacement(placement) }
        };
    }

    private record ReactAssetMigrationDefinition(
        string RelativePath,
        string Title,
        string Caption,
        string AltText,
        string Category,
        string MediaType,
        string LayoutType,
        string CurrentUsage,
        (string Section, int DisplayOrder, bool IsFeatured)[] Placements);

    private static readonly ReactAssetMigrationDefinition[] CategoryAMigrationDefinitions = new[]
    {
        // 1. Hero images & videos
        new ReactAssetMigrationDefinition(
            "src/assets/hero/hero-01.jpg",
            "In Motion - Contemporary Leap",
            "Ethos master dancers showcasing precision and flight.",
            "Contemporary dancer performing dynamic leap",
            MediaConstants.Categories.Performances,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Landscape,
            "Hero.jsx (Slide 1 fallback), Gallery.jsx (hero-01)",
            new[] { (MediaConstants.Sections.HomepageScrolling, 1, false), (MediaConstants.Sections.GalleryImages, 2, false) }),

        new ReactAssetMigrationDefinition(
            "src/assets/hero/hero-02.jpg",
            "Find Your Rhythm",
            "Synchronized studio session exploring rhythm and tempo.",
            "Dancers training together at Ethos",
            MediaConstants.Categories.Performances,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Square,
            "Hero.jsx (Slide 2 fallback), Gallery.jsx (hero-02)",
            new[] { (MediaConstants.Sections.HomepageScrolling, 2, false), (MediaConstants.Sections.GalleryImages, 8, false) }),

        new ReactAssetMigrationDefinition(
            "src/assets/hero/hero-video.mp4",
            "Urban Showcase Reel",
            "High energy routines, footwork and student combinations.",
            "Dance performance showcase at Ethos Dance Studio",
            MediaConstants.Categories.Performances,
            MediaConstants.MediaTypes.Video,
            MediaConstants.LayoutTypes.Portrait,
            "Hero.jsx (Slide 3 fallback), ShortDanceVideos.jsx (Reel 2)",
            new[] { (MediaConstants.Sections.HomepageScrolling, 3, false), (MediaConstants.Sections.HomepageReels, 2, false) }),

        new ReactAssetMigrationDefinition(
            "src/assets/hero/hero-03.jpg",
            "Move With Purpose",
            "Floor work and contemporary flow routines.",
            "Dance class routine at Ethos Dance Studio",
            MediaConstants.Categories.Performances,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Landscape,
            "Hero.jsx (Slide 4 fallback), Gallery.jsx (hero-03)",
            new[] { (MediaConstants.Sections.HomepageScrolling, 4, false), (MediaConstants.Sections.GalleryImages, 12, false) }),

        new ReactAssetMigrationDefinition(
            "src/assets/hero/hero-04.jpg",
            "Performance Mastery",
            "Advanced choreography and expression.",
            "Dance performance at Ethos Dance Studio",
            MediaConstants.Categories.Performances,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Landscape,
            "Hero.jsx (Slide 5 fallback)",
            new[] { (MediaConstants.Sections.HomepageScrolling, 5, false) }),

        new ReactAssetMigrationDefinition(
            "src/assets/hero/hero-05.jpg",
            "Expressive Formation",
            "Precision staging and group dynamics.",
            "Ethos company dancers in staging formation",
            MediaConstants.Categories.Performances,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Square,
            "Hero archive / studio gallery",
            new[] { (MediaConstants.Sections.HomepageScrolling, 6, false) }),

        new ReactAssetMigrationDefinition(
            "src/assets/hero/hero-06.jpg",
            "Ensemble Flow",
            "The complete studio company in full harmony.",
            "Ethos Dance Studio ensemble performance",
            MediaConstants.Categories.Performances,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Featured,
            "Hero.jsx (Slide 6 fallback)",
            new[] { (MediaConstants.Sections.HomepageScrolling, 7, true) }),

        new ReactAssetMigrationDefinition(
            "src/assets/hero/sample-test-video.mp4",
            "Sample Routine Clip",
            "Short training preview clip.",
            "Training routine sample video",
            MediaConstants.Categories.BehindTheScenes,
            MediaConstants.MediaTypes.Video,
            MediaConstants.LayoutTypes.Landscape,
            "Admin upload testing / preview sample",
            new[] { (MediaConstants.Sections.Draft, 1, false) }),

        // 2. Gallery videos
        new ReactAssetMigrationDefinition(
            "src/assets/gallery/ethos-visual-reel.mp4",
            "Contemporary Routine Reel",
            "Flow, control and musicality in our advanced routine session.",
            "Ethos visual dance showcase reel",
            MediaConstants.Categories.Performances,
            MediaConstants.MediaTypes.Video,
            MediaConstants.LayoutTypes.Portrait,
            "Gallery.jsx (visual reel), ShortDanceVideos.jsx (Reel 1)",
            new[] { (MediaConstants.Sections.HomepageReels, 1, true), (MediaConstants.Sections.GalleryVideos, 1, true) }),

        // 3. Workshops
        new ReactAssetMigrationDefinition(
            "src/assets/workshops/workshop-01.jpg",
            "Workshop Sessions - Contemporary Choreography",
            "Intensive technique and movement exploration.",
            "Dancer in contemporary workshop",
            MediaConstants.Categories.Workshops,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Portrait,
            "Workshops.jsx (Card 1 fallback), Gallery.jsx (workshop-01)",
            new[] { (MediaConstants.Sections.Workshop, 1, true), (MediaConstants.Sections.GalleryImages, 1, true) }),

        new ReactAssetMigrationDefinition(
            "src/assets/workshops/workshop-02.jpg",
            "Movement & Energy",
            "Fast-paced urban choreography and stage presence.",
            "Hip hop workshop session",
            MediaConstants.Categories.Workshops,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Square,
            "Workshops.jsx (Card 2 fallback), Gallery.jsx (workshop-02)",
            new[] { (MediaConstants.Sections.Workshop, 2, false), (MediaConstants.Sections.GalleryImages, 4, false) }),

        new ReactAssetMigrationDefinition(
            "src/assets/workshops/workshop-03.jpg",
            "Learn. Move. Grow.",
            "Foundation training for all styles and backgrounds.",
            "Technique workshop group practice",
            MediaConstants.Categories.Workshops,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Landscape,
            "Workshops.jsx (Card 3 fallback), Gallery.jsx (workshop-03)",
            new[] { (MediaConstants.Sections.Workshop, 3, false), (MediaConstants.Sections.GalleryImages, 7, false) }),

        new ReactAssetMigrationDefinition(
            "src/assets/workshops/workshop-04.jpg",
            "Workshop Energy",
            "High energy partner work and rhythmic footwork.",
            "Dynamic workshop choreography class",
            MediaConstants.Categories.Workshops,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Square,
            "Workshops.jsx (Card 4 fallback), Gallery.jsx (workshop-04)",
            new[] { (MediaConstants.Sections.Workshop, 4, false), (MediaConstants.Sections.GalleryImages, 11, false) }),

        // 4. About & Founders
        new ReactAssetMigrationDefinition(
            "src/assets/about/about-main.jpg",
            "Inside Ethos - The Beginning",
            "Dance is connection. Built for dancers of every background.",
            "Dancers performing at Ethos Dance Studio",
            MediaConstants.Categories.Studio,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Portrait,
            "About.jsx (The Beginning), Classes.jsx, ClassesComingSoon.jsx, Gallery.jsx (about-main)",
            new[] { (MediaConstants.Sections.AboutEthos, 1, true), (MediaConstants.Sections.GalleryImages, 5, false) }),

        new ReactAssetMigrationDefinition(
            "src/assets/about/about-secondary.jpg",
            "The Ethos Space - Find Your Rhythm",
            "Modern dance studio floor designed for freedom of movement.",
            "Dancers training together at Ethos space",
            MediaConstants.Categories.Studio,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Square,
            "About.jsx (The Experience), Gallery.jsx (about-secondary)",
            new[] { (MediaConstants.Sections.AboutEthos, 2, false), (MediaConstants.Sections.GalleryImages, 9, false) }),

        new ReactAssetMigrationDefinition(
            "src/assets/about/about-community.jpg",
            "The Ethos Community",
            "You don't have to move alone. The people around you become family.",
            "Ethos dance community gathering",
            MediaConstants.Categories.Community,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Square,
            "About.jsx (The Community), Gallery.jsx (about-community)",
            new[] { (MediaConstants.Sections.AboutEthos, 3, false), (MediaConstants.Sections.GalleryImages, 13, false) }),

        new ReactAssetMigrationDefinition(
            "src/assets/founders/founders.jpg",
            "The People Behind Ethos - Founders",
            "Creative visionaries shaping the ethos and culture of movement.",
            "Ethos Dance Studio co-founders",
            MediaConstants.Categories.Community,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Square,
            "Founders.jsx (Stage interactive image), Events.jsx, Gallery.jsx (founders)",
            new[] { (MediaConstants.Sections.AboutEthos, 4, true), (MediaConstants.Sections.GalleryImages, 3, false) }),

        // 5. Trainers
        new ReactAssetMigrationDefinition(
            "src/assets/trainers/trainer-01.jpg",
            "Sujith Kumar - Co-Founder & Lead Choreographer",
            "Movement-driven choreographer focused on powerful performances.",
            "Sujith Kumar faculty portrait",
            MediaConstants.Categories.Community,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Square,
            "Trainers.jsx (Sujith Kumar), Gallery.jsx (trainer-01)",
            new[] { (MediaConstants.Sections.Trainers, 1, true), (MediaConstants.Sections.GalleryImages, 6, false) }),

        new ReactAssetMigrationDefinition(
            "src/assets/trainers/trainer-02.jpg",
            "Tejaswini - Co-Founder & Executive Director",
            "Creative director creating an environment to grow and belong.",
            "Tejaswini faculty portrait",
            MediaConstants.Categories.Community,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Portrait,
            "Trainers.jsx (Tejaswini), Gallery.jsx (trainer-02)",
            new[] { (MediaConstants.Sections.Trainers, 2, true), (MediaConstants.Sections.GalleryImages, 10, false) }),

        new ReactAssetMigrationDefinition(
            "src/assets/trainers/trainer-03.jpg",
            "Ethos Crew Lead - Assistant Choreographer",
            "Passionate performer bringing discipline and individuality.",
            "Ethos crew lead portrait",
            MediaConstants.Categories.Community,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Square,
            "Trainers.jsx (Ethos Crew Lead), Gallery.jsx (trainer-03)",
            new[] { (MediaConstants.Sections.Trainers, 3, false), (MediaConstants.Sections.GalleryImages, 14, false) }),

        new ReactAssetMigrationDefinition(
            "src/assets/trainers/trainer-04.jpg",
            "Senior Resident Faculty - Classical & Contemporary",
            "Expressive movement blending classical foundation with modern flow.",
            "Senior resident faculty portrait",
            MediaConstants.Categories.Community,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Portrait,
            "Trainers.jsx (Senior Resident Faculty), Gallery.jsx (trainer-04)",
            new[] { (MediaConstants.Sections.Trainers, 4, false), (MediaConstants.Sections.GalleryImages, 15, false) }),

        new ReactAssetMigrationDefinition(
            "src/assets/shanmuka.jpg",
            "Shanmuka - Movement Faculty",
            "Choreography and technique instruction.",
            "Shanmuka faculty portrait",
            MediaConstants.Categories.Community,
            MediaConstants.MediaTypes.Image,
            MediaConstants.LayoutTypes.Portrait,
            "Trainers / Guest faculty profile asset",
            new[] { (MediaConstants.Sections.Trainers, 5, false) })
    };

    public async Task<ReactAssetMigrationResponse> MigrateReactAssetsAsync(
        string? webRootPath,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        string? resolvedWebRoot = webRootPath;
        if (string.IsNullOrWhiteSpace(resolvedWebRoot) || !Directory.Exists(resolvedWebRoot))
        {
            var candidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "..", "Ethos.Web"),
                Path.Combine(Directory.GetCurrentDirectory(), "Ethos.Web"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Ethos.Web"),
                @"d:\ETHOS DANCE studio\Ethos.Web"
            };

            foreach (var cand in candidates)
            {
                var full = Path.GetFullPath(cand);
                if (Directory.Exists(full) && Directory.Exists(Path.Combine(full, "src", "assets")))
                {
                    resolvedWebRoot = full;
                    break;
                }
            }
        }

        var response = new ReactAssetMigrationResponse
        {
            TotalFound = CategoryAMigrationDefinitions.Length
        };

        if (string.IsNullOrWhiteSpace(resolvedWebRoot) || !Directory.Exists(resolvedWebRoot))
        {
            _logger.LogError("Could not resolve Ethos.Web root path for media migration.");
            response.ErrorCount = CategoryAMigrationDefinitions.Length;
            return response;
        }

        foreach (var def in CategoryAMigrationDefinitions)
        {
            var filePath = Path.Combine(resolvedWebRoot, def.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Migration asset file not found: {Path}", filePath);
                response.Items.Add(new ReactAssetMigrationItemResult
                {
                    OriginalReactAsset = def.RelativePath,
                    CurrentUsage = def.CurrentUsage,
                    MediaSection = string.Join(", ", def.Placements.Select(p => p.Section)),
                    FrontendUpdated = true,
                    SafeToRemoveLocalCopy = false,
                    Status = "Error",
                    Error = $"File not found: {filePath}"
                });
                response.ErrorCount++;
                continue;
            }

            try
            {
                string checksum;
                long fileLength;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                using (var fs = File.OpenRead(filePath))
                {
                    var hash = await sha.ComputeHashAsync(fs, cancellationToken);
                    checksum = Convert.ToHexString(hash).ToLowerInvariant();
                    fileLength = fs.Length;
                }

                var fileName = Path.GetFileName(filePath);
                var existingItem = await _db.MediaItems
                    .Include(m => m.Placements)
                    .FirstOrDefaultAsync(m => m.Title == def.Title || (m.OriginalFileName == fileName && m.Placements.Any(p => p.Section == def.Placements[0].Section)), cancellationToken);

                if (existingItem != null)
                {
                    // Existing item found: ensure all required placements are attached
                    bool placementsAdded = false;
                    foreach (var (sec, order, feat) in def.Placements)
                    {
                        if (!existingItem.Placements.Any(p => p.Section.Equals(sec, StringComparison.OrdinalIgnoreCase)))
                        {
                            var newPlacement = new MediaPlacement
                            {
                                Id = Guid.NewGuid(),
                                MediaItemId = existingItem.Id,
                                Section = sec,
                                DisplayOrder = order,
                                IsFeatured = feat,
                                IsPublished = true,
                                CreatedAt = DateTime.UtcNow
                            };
                            existingItem.Placements.Add(newPlacement);
                            _db.MediaPlacements.Add(newPlacement);
                            placementsAdded = true;
                        }
                    }

                    if (placementsAdded)
                    {
                        await _db.SaveChangesAsync(cancellationToken);
                    }

                    response.Items.Add(new ReactAssetMigrationItemResult
                    {
                        OriginalReactAsset = def.RelativePath,
                        CurrentUsage = def.CurrentUsage,
                        R2Key = existingItem.ObjectKey,
                        MediaSection = string.Join(", ", existingItem.Placements.Select(p => p.Section)),
                        DbRecord = existingItem.Id.ToString(),
                        FrontendUpdated = true,
                        SafeToRemoveLocalCopy = false,
                        Status = placementsAdded ? "UpdatedWithPlacements" : "AlreadyExists"
                    });
                    response.AlreadyExistedCount++;
                    continue;
                }

                // Check if another MediaItem already uploaded identical bytes to R2
                var existingByChecksum = await _db.MediaItems
                    .FirstOrDefaultAsync(m => m.Checksum == checksum && !string.IsNullOrEmpty(m.ObjectKey), cancellationToken);

                string objectKey;
                string publicUrl;
                string contentType;

                if (existingByChecksum != null)
                {
                    // Reuse existing R2 object key without duplicate upload
                    objectKey = existingByChecksum.ObjectKey;
                    publicUrl = existingByChecksum.PublicUrl;
                    contentType = existingByChecksum.MimeType;
                    _logger.LogInformation("Reusing existing R2 key {ObjectKey} for asset {Asset}", objectKey, def.RelativePath);
                }
                else
                {
                    var ext = Path.GetExtension(filePath).ToLowerInvariant();
                    contentType = ext switch
                    {
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".png" => "image/png",
                        ".webp" => "image/webp",
                        ".mp4" => "video/mp4",
                        ".webm" => "video/webm",
                        _ => def.MediaType == MediaConstants.MediaTypes.Video ? "video/mp4" : "image/jpeg"
                    };

                    var primarySection = def.Placements.FirstOrDefault().Section ?? MediaConstants.Sections.HomepageScrolling;

                    R2UploadResult uploadResult;
                    using (var fs = File.OpenRead(filePath))
                    {
                        uploadResult = await _r2Service.UploadAsync(fs, fileName, contentType, primarySection, cancellationToken);
                    }

                    objectKey = uploadResult.ObjectKey;
                    publicUrl = uploadResult.PublicUrl;
                }

                var mediaItem = new MediaItem
                {
                    Id = Guid.NewGuid(),
                    Title = def.Title,
                    Caption = def.Caption,
                    AltText = def.AltText,
                    FocalPoint = "center",
                    Category = def.Category,
                    LayoutType = def.LayoutType,
                    IsArchived = false,
                    ObjectKey = objectKey,
                    OriginalFileName = fileName,
                    MediaType = def.MediaType,
                    MimeType = contentType,
                    FileSizeBytes = fileLength,
                    PublicUrl = publicUrl,
                    ThumbnailUrl = publicUrl,
                    DurationSeconds = def.MediaType == MediaConstants.MediaTypes.Video ? 15.0 : null,
                    Visibility = "Public",
                    ApprovalStatus = "Approved",
                    UploadedByUserId = adminUserId,
                    Checksum = checksum,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                mediaItem.Placements = def.Placements.Select(p => new MediaPlacement
                {
                    Id = Guid.NewGuid(),
                    MediaItemId = mediaItem.Id,
                    Section = p.Section,
                    DisplayOrder = p.DisplayOrder,
                    IsFeatured = p.IsFeatured,
                    IsPublished = true,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                _db.MediaItems.Add(mediaItem);
                await _db.SaveChangesAsync(cancellationToken);

                response.Items.Add(new ReactAssetMigrationItemResult
                {
                    OriginalReactAsset = def.RelativePath,
                    CurrentUsage = def.CurrentUsage,
                    R2Key = mediaItem.ObjectKey,
                    MediaSection = string.Join(", ", mediaItem.Placements.Select(p => p.Section)),
                    DbRecord = mediaItem.Id.ToString(),
                    FrontendUpdated = true,
                    SafeToRemoveLocalCopy = false,
                    Status = "Migrated"
                });
                response.MigratedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate React asset: {Path}", def.RelativePath);
                response.Items.Add(new ReactAssetMigrationItemResult
                {
                    OriginalReactAsset = def.RelativePath,
                    CurrentUsage = def.CurrentUsage,
                    MediaSection = string.Join(", ", def.Placements.Select(p => p.Section)),
                    FrontendUpdated = true,
                    SafeToRemoveLocalCopy = false,
                    Status = "Error",
                    Error = ex.Message
                });
                response.ErrorCount++;
            }
        }

        _cacheService.InvalidateAllMediaCache();

        await _auditService.LogActionAsync(
            adminUserId,
            "MEDIA_REACT_ASSETS_MIGRATED",
            "MEDIA",
            "MediaItem",
            Guid.Empty,
            reason: $"Migrated React assets to R2: {response.MigratedCount} new, {response.AlreadyExistedCount} existing, {response.ErrorCount} errors out of {response.TotalFound} total.");

        return response;
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
