using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Media;
using Ethos.Api.Application.Storage;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Media;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ethos.Api.Tests.Media;

public class PlacementIsolationAuditTests
{
    private class FakeR2StorageService : ICloudflareR2StorageService
    {
        public List<string> DeletedKeys { get; } = new();

        public Task<R2UploadResult> UploadAsync(Stream stream, string originalFileName, string contentType, string section, CancellationToken cancellationToken = default)
        {
            var key = $"media/{section}/{Guid.NewGuid():N}-{originalFileName}";
            return Task.FromResult(new R2UploadResult
            {
                ObjectKey = key,
                PublicUrl = $"https://media.ethosdancestudio.com/{key}",
                ContentType = contentType,
                FileSizeBytes = stream.Length,
                Checksum = "mock-checksum"
            });
        }

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            DeletedKeys.Add(objectKey);
            return Task.CompletedTask;
        }

        public string GetPublicUrl(string objectKey) => $"https://media.ethosdancestudio.com/{objectKey}";
        public string GeneratePreSignedGetUrl(string objectKey, TimeSpan duration) => $"https://media.ethosdancestudio.com/{objectKey}?mock";
        public string GeneratePreSignedPutUrl(string objectKey, string contentType, TimeSpan duration) => $"https://r2.cloudflarestorage.com/mock/{objectKey}";
        public Task<R2ObjectMetadata?> GetObjectMetadataAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult<R2ObjectMetadata?>(null);
        public Task<(Stream Stream, string ContentType)?> GetObjectStreamAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult<(Stream Stream, string ContentType)?>(null);
        public Task<R2RangeResult?> GetObjectRangeStreamAsync(string objectKey, long? fromByte, long? toByte, CancellationToken cancellationToken = default) => Task.FromResult<R2RangeResult?>(null);
    }

    private class FakeMediaFastStartService : IMediaFastStartService
    {
        public bool IsAvailable => false;
        public string? FfmpegExecutablePath => null;
        public Task<FastStartResult> OptimizeAsync(string inputFilePath, string outputFilePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FastStartResult { Success = false });
        public Task<FastStartResult> OptimizeStreamAsync(Stream inputStream, string outputFilePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FastStartResult { Success = false });
    }

    private class FakeMediaCacheService : IMediaCacheService
    {
        public string NormalizeCacheKey(string? section, string? category, string? mediaType) => $"{section}_{category}_{mediaType}";
        public Task<T> GetOrSetAsync<T>(string cacheKey, Func<Task<T>> factory, TimeSpan? expiration = null) => factory();
        public void InvalidateAllMediaCache() { }
        public void InvalidatePublicMediaCache(string? section = null) { }
    }

    private class FakeAdminAuditService : IAdminAuditService
    {
        public void AddAuditLog(Guid adminUserId, string actionType, string entityType, Guid entityId, string? reason, string category = "OPERATIONS", Guid? adminDeviceId = null, Guid? adminSessionId = null, string? traceId = null, string? ipAddress = null, string? userAgent = null, string? metadataJson = null) { }
        public Task LogActionAsync(Guid adminUserId, string actionType, string category, string entityType, Guid entityId, bool success = true, string? outcomeCode = null, string? reason = null, Guid? adminDeviceId = null, Guid? adminSessionId = null, string? traceId = null, string? requestId = null, string? ipAddress = null, string? userAgent = null, object? metadata = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogSecurityEventAsync(string eventType, string severity, string? ipAddress, string? userAgent, Guid? userId = null, Guid? adminDeviceId = null, Guid? adminSessionId = null, string? traceId = null, string? maskedPhone = null, object? details = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PagedResult<AdminAuditLogResponse>> GetAuditLogsAsync(int page, int pageSize, string? category = null, string? actionType = null, string? entityType = null, Guid? entityId = null, Guid? adminUserId = null, string? traceId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AdminAuditLogResponse?> GetAuditLogByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<AdminSecurityEventResponse>> GetSecurityEventsAsync(int page, int pageSize, string? eventType = null, string? severity = null, Guid? userId = null, string? traceId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AdminSecurityEventResponse?> GetSecurityEventByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private (MediaService service, AppDbContext db) CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"IsolationAudit_{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        var r2 = new FakeR2StorageService();
        var cache = new FakeMediaCacheService();
        var audit = new FakeAdminAuditService();
        var fastStart = new FakeMediaFastStartService();
        var logger = NullLogger<MediaService>.Instance;
        var service = new MediaService(db, r2, audit, cache, fastStart, logger);
        return (service, db);
    }

    [Fact]
    public async Task Scenario1_GallerySlideshow_And_GalleryImages_Are_Completely_Isolated_By_Section()
    {
        var (service, db) = CreateContext();

        // Record A: In GallerySlideshow (Slot 1)
        var itemSlideshow = new MediaItem
        {
            Id = Guid.NewGuid(),
            Title = "Slideshow Hero 01",
            ObjectKey = "media/galleryslideshow/slide1.jpg",
            PublicUrl = "https://media.ethosdancestudio.com/slide1.jpg",
            MediaType = MediaConstants.MediaTypes.Image,
            Category = "Workshop",
            LayoutType = MediaConstants.LayoutTypes.Landscape,
            Placements = new List<MediaPlacement>
            {
                new() { Id = Guid.NewGuid(), Section = MediaConstants.Sections.GallerySlideshow, DisplayOrder = 1, IsPublished = true }
            }
        };

        // Record B: In GalleryImages (Categorized photo)
        var itemGalleryPhoto = new MediaItem
        {
            Id = Guid.NewGuid(),
            Title = "Gallery InStudio Photo",
            ObjectKey = "media/galleryimages/photo1.jpg",
            PublicUrl = "https://media.ethosdancestudio.com/photo1.jpg",
            MediaType = MediaConstants.MediaTypes.Image,
            Category = "In Studio",
            LayoutType = MediaConstants.LayoutTypes.Portrait,
            Placements = new List<MediaPlacement>
            {
                new() { Id = Guid.NewGuid(), Section = MediaConstants.Sections.GalleryImages, DisplayOrder = 1, IsPublished = true }
            }
        };

        db.MediaItems.AddRange(itemSlideshow, itemGalleryPhoto);
        await db.SaveChangesAsync();

        // Query GallerySlideshow section
        var slideshowResults = await service.GetPublicMediaAsync(MediaConstants.Sections.GallerySlideshow, null, null);
        Assert.Single(slideshowResults);
        Assert.Equal("Slideshow Hero 01", slideshowResults[0].Title);
        Assert.Equal(MediaConstants.Sections.GallerySlideshow, slideshowResults[0].Section);

        // Query GalleryImages section
        var galleryImagesResults = await service.GetPublicMediaAsync(MediaConstants.Sections.GalleryImages, null, null);
        Assert.Single(galleryImagesResults);
        Assert.Equal("Gallery InStudio Photo", galleryImagesResults[0].Title);
        Assert.Equal(MediaConstants.Sections.GalleryImages, galleryImagesResults[0].Section);

        // Prove: itemSlideshow is NOT in GalleryImages
        Assert.DoesNotContain(galleryImagesResults, m => m.Id == itemSlideshow.Id);
        // Prove: itemGalleryPhoto is NOT in GallerySlideshow
        Assert.DoesNotContain(slideshowResults, m => m.Id == itemGalleryPhoto.Id);
    }

    [Fact]
    public async Task Scenario2_Workshop_Landscape_And_Portrait_Are_Differentiated_By_LayoutType()
    {
        var (service, db) = CreateContext();

        // Record A: Workshop Landscape Banner
        var itemLandscape = new MediaItem
        {
            Id = Guid.NewGuid(),
            Title = "Contemporary Workshop Banner",
            ObjectKey = "media/workshop/banner1.jpg",
            PublicUrl = "https://media.ethosdancestudio.com/banner1.jpg",
            MediaType = MediaConstants.MediaTypes.Image,
            Category = "Workshop",
            LayoutType = MediaConstants.LayoutTypes.Landscape,
            Placements = new List<MediaPlacement>
            {
                new() { Id = Guid.NewGuid(), Section = MediaConstants.Sections.Workshop, DisplayOrder = 1, IsPublished = true }
            }
        };

        // Record B: Workshop Portrait Poster
        var itemPortrait = new MediaItem
        {
            Id = Guid.NewGuid(),
            Title = "Contemporary Workshop Poster",
            ObjectKey = "media/workshop/poster1.jpg",
            PublicUrl = "https://media.ethosdancestudio.com/poster1.jpg",
            MediaType = MediaConstants.MediaTypes.Image,
            Category = "Workshop",
            LayoutType = MediaConstants.LayoutTypes.Portrait,
            Placements = new List<MediaPlacement>
            {
                new() { Id = Guid.NewGuid(), Section = MediaConstants.Sections.Workshop, DisplayOrder = 2, IsPublished = true }
            }
        };

        db.MediaItems.AddRange(itemLandscape, itemPortrait);
        await db.SaveChangesAsync();

        // Query all workshop placements
        var workshopResults = await service.GetPublicMediaAsync(MediaConstants.Sections.Workshop, null, null);
        Assert.Equal(2, workshopResults.Count);

        // Isolate Landscape
        var landscapeOnly = workshopResults.Where(w => w.LayoutType == MediaConstants.LayoutTypes.Landscape).ToList();
        Assert.Single(landscapeOnly);
        Assert.Equal("Contemporary Workshop Banner", landscapeOnly[0].Title);

        // Isolate Portrait
        var portraitOnly = workshopResults.Where(w => w.LayoutType == MediaConstants.LayoutTypes.Portrait).ToList();
        Assert.Single(portraitOnly);
        Assert.Equal("Contemporary Workshop Poster", portraitOnly[0].Title);
    }

    [Fact]
    public async Task Scenario3_DeletePlacement_Leaves_Other_Placements_Of_Same_MediaItem_Intact()
    {
        var (service, db) = CreateContext();
        var adminId = Guid.NewGuid();

        var placementHero = new MediaPlacement
        {
            Id = Guid.NewGuid(),
            Section = MediaConstants.Sections.HomepageScrolling,
            DisplayOrder = 1,
            IsPublished = true
        };
        var placementGallery = new MediaPlacement
        {
            Id = Guid.NewGuid(),
            Section = MediaConstants.Sections.GalleryImages,
            DisplayOrder = 1,
            IsPublished = true
        };

        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            Title = "Multi-Section Shared Photo",
            ObjectKey = "media/shared/photo.jpg",
            PublicUrl = "https://media.ethosdancestudio.com/photo.jpg",
            MediaType = MediaConstants.MediaTypes.Image,
            Placements = new List<MediaPlacement> { placementHero, placementGallery }
        };

        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync();

        // Verify initially both placements exist
        var countBefore = await db.MediaPlacements.CountAsync(p => p.MediaItemId == mediaItem.Id);
        Assert.Equal(2, countBefore);

        // Delete ONLY the Hero placement
        await service.RemovePlacementAsync(placementHero.Id, adminId);

        // Assert: Hero placement is removed
        Assert.Null(await db.MediaPlacements.FindAsync(placementHero.Id));

        // Assert: Gallery placement remains active and untouched
        var galleryPlacementStillExists = await db.MediaPlacements.FindAsync(placementGallery.Id);
        Assert.NotNull(galleryPlacementStillExists);
        Assert.Equal(MediaConstants.Sections.GalleryImages, galleryPlacementStillExists.Section);

        // Assert: MediaItem itself is NOT deleted because it still has an active placement
        var item = await db.MediaItems.FindAsync(mediaItem.Id);
        Assert.NotNull(item);
        Assert.False(item.IsDeleted);
    }
}
