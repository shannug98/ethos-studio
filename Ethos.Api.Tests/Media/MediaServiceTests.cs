using System.Text;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Media;
using Ethos.Api.Application.Storage;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Media;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ethos.Api.Tests.Media;

public class MediaServiceTests
{
    private class FakeR2StorageService : ICloudflareR2StorageService
    {
        public List<string> DeletedKeys { get; } = new();
        public Dictionary<string, R2ObjectMetadata> ObjectMetadataMap { get; } = new();

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
            ObjectMetadataMap.Remove(objectKey);
            return Task.CompletedTask;
        }

        public string GetPublicUrl(string objectKey)
        {
            return $"https://media.ethosdancestudio.com/{objectKey}";
        }

        public string GeneratePreSignedGetUrl(string objectKey, TimeSpan duration)
        {
            return $"https://media.ethosdancestudio.com/{objectKey}?token=mock-get";
        }

        public string GeneratePreSignedPutUrl(string objectKey, string contentType, TimeSpan duration)
        {
            return $"https://r2.cloudflarestorage.com/ethos-media/{objectKey}?token=mock-presigned-url";
        }

        public Task<R2ObjectMetadata?> GetObjectMetadataAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            if (ObjectMetadataMap.TryGetValue(objectKey, out var meta))
            {
                return Task.FromResult<R2ObjectMetadata?>(meta);
            }
            return Task.FromResult<R2ObjectMetadata?>(null);
        }

        public Task<(Stream Stream, string ContentType)?> GetObjectStreamAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<(Stream Stream, string ContentType)?>(null);
        }

        public Task<R2RangeResult?> GetObjectRangeStreamAsync(string objectKey, long? fromByte, long? toByte, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<R2RangeResult?>(null);
        }
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

    private class FakeAdminAuditService : IAdminAuditService
    {
        public List<(string action, string entityType, Guid entityId)> LoggedActions { get; } = new();

        public void AddAuditLog(Guid adminUserId, string actionType, string entityType, Guid entityId, string? reason, string category = "OPERATIONS", Guid? adminDeviceId = null, Guid? adminSessionId = null, string? traceId = null, string? ipAddress = null, string? userAgent = null, string? metadataJson = null)
        {
            LoggedActions.Add((actionType, entityType, entityId));
        }

        public Task LogActionAsync(Guid adminUserId, string actionType, string category, string entityType, Guid entityId, bool success = true, string? outcomeCode = null, string? reason = null, Guid? adminDeviceId = null, Guid? adminSessionId = null, string? traceId = null, string? requestId = null, string? ipAddress = null, string? userAgent = null, object? metadata = null, CancellationToken cancellationToken = default)
        {
            LoggedActions.Add((actionType, entityType, entityId));
            return Task.CompletedTask;
        }

        public Task LogSecurityEventAsync(string eventType, string severity, string? ipAddress, string? userAgent, Guid? userId = null, Guid? adminDeviceId = null, Guid? adminSessionId = null, string? traceId = null, string? maskedPhone = null, object? details = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PagedResult<AdminAuditLogResponse>> GetAuditLogsAsync(int page, int pageSize, string? category = null, string? actionType = null, string? entityType = null, Guid? entityId = null, Guid? adminUserId = null, string? traceId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AdminAuditLogResponse?> GetAuditLogByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<AdminSecurityEventResponse>> GetSecurityEventsAsync(int page, int pageSize, string? eventType = null, string? severity = null, Guid? userId = null, string? traceId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AdminSecurityEventResponse?> GetSecurityEventByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private class FakeMediaCacheService : IMediaCacheService
    {
        public int InvalidationCount { get; private set; }
        public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) => factory();
        public void InvalidateAllMediaCache() => InvalidationCount++;
        public string NormalizeCacheKey(string? section, string? category, string? mediaType) => $"{section}_{category}_{mediaType}";
    }

    private class TestFormFile : IFormFile
    {
        private readonly byte[] _headerBytes;
        private readonly long _length;

        public TestFormFile(byte[] headerBytes, long length, string fileName, string contentType)
        {
            _headerBytes = headerBytes;
            _length = length;
            FileName = fileName;
            ContentType = contentType;
        }

        public string ContentType { get; }
        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
        public IHeaderDictionary Headers => new HeaderDictionary();
        public long Length => _length;
        public string Name => "file";
        public string FileName { get; }

        public void CopyTo(Stream target)
        {
            target.Write(_headerBytes, 0, _headerBytes.Length);
        }

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            return target.WriteAsync(_headerBytes, 0, _headerBytes.Length, cancellationToken);
        }

        public Stream OpenReadStream()
        {
            var ms = new MemoryStream();
            ms.Write(_headerBytes, 0, _headerBytes.Length);
            ms.Position = 0;
            return ms;
        }
    }

    private static readonly byte[] Mp4Header = new byte[] { 0, 0, 0, 0, 0x66, 0x74, 0x79, 0x70, 0, 0, 0, 0 };
    private static readonly byte[] JpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1 };

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private (MediaService service, AppDbContext db, FakeR2StorageService r2, FakeAdminAuditService audit) CreateTestContext()
    {
        var db = CreateDbContext();
        var r2 = new FakeR2StorageService();
        var audit = new FakeAdminAuditService();
        var cache = new FakeMediaCacheService();
        var fastStart = new FakeMediaFastStartService();
        var logger = NullLogger<MediaService>.Instance;

        var service = new MediaService(db, r2, audit, cache, fastStart, logger);
        return (service, db, r2, audit);
    }

    [Fact]
    public async Task UploadMediaAsync_When20ReelsExist_EvictsOldestReelAndPurgesR2()
    {
        var (service, db, r2, _) = CreateTestContext();
        var adminId = Guid.NewGuid();

        // Seed 20 HomepageReels media items with ascending timestamps
        var seededItems = new List<MediaItem>();
        for (int i = 1; i <= 20; i++)
        {
            var item = new MediaItem
            {
                Id = Guid.NewGuid(),
                Title = $"Reel {i}",
                MediaType = MediaConstants.MediaTypes.Video,
                ObjectKey = $"media/reels/reel_{i}.mp4",
                FileSizeBytes = 10_000_000,
                PublicUrl = $"https://media.ethosdancestudio.com/media/reels/reel_{i}.mp4",
                CreatedAt = DateTime.UtcNow.AddMinutes(-100 + i)
            };
            var placement = new MediaPlacement
            {
                Id = Guid.NewGuid(),
                MediaItemId = item.Id,
                Section = MediaConstants.Sections.HomepageReels,
                DisplayOrder = i,
                IsPublished = true,
                CreatedAt = item.CreatedAt
            };
            item.Placements = new List<MediaPlacement> { placement };
            seededItems.Add(item);
            db.MediaItems.Add(item);
        }
        await db.SaveChangesAsync();

        var oldestItem = seededItems[0];

        // Upload 21st reel (10MB)
        var newFile = new TestFormFile(Mp4Header, 10_000_000, "reel_21.mp4", "video/mp4");
        var response = await service.UploadMediaAsync(
            file: newFile,
            sections: new[] { MediaConstants.Sections.HomepageReels },
            mediaType: MediaConstants.MediaTypes.Video,
            title: "Reel 21",
            caption: "New 21st reel",
            altText: null,
            focalPoint: "center",
            category: "General",
            layoutType: "Square",
            displayOrder: 1,
            isPublished: true,
            isFeatured: false,
            clientDurationSeconds: 15.0,
            adminUserId: adminId
        );

        Assert.NotNull(response);

        // Verify total HomepageReels placements is still capped at 20
        var totalReelPlacements = await db.MediaPlacements
            .CountAsync(p => p.Section == MediaConstants.Sections.HomepageReels);
        Assert.Equal(20, totalReelPlacements);

        // Verify oldest item's placement was evicted and its MediaItem was deleted
        var oldestPlacement = await db.MediaPlacements
            .FirstOrDefaultAsync(p => p.MediaItemId == oldestItem.Id && p.Section == MediaConstants.Sections.HomepageReels);
        Assert.Null(oldestPlacement);

        var oldestDbItem = await db.MediaItems.FirstOrDefaultAsync(m => m.Id == oldestItem.Id);
        Assert.Null(oldestDbItem);

        // Verify R2 object was purged
        Assert.Contains(oldestItem.ObjectKey, r2.DeletedKeys);
    }

    [Fact]
    public async Task UploadMediaAsync_WhenOldestReelIsReferencedElsewhere_EvictsPlacementOnlyAndPreservesMediaItem()
    {
        var (service, db, r2, _) = CreateTestContext();
        var adminId = Guid.NewGuid();

        // Seed 20 HomepageReels media items
        var seededItems = new List<MediaItem>();
        for (int i = 1; i <= 20; i++)
        {
            var item = new MediaItem
            {
                Id = Guid.NewGuid(),
                Title = $"Reel {i}",
                MediaType = MediaConstants.MediaTypes.Video,
                ObjectKey = $"media/reels/reel_{i}.mp4",
                FileSizeBytes = 10_000_000,
                PublicUrl = $"https://media.ethosdancestudio.com/media/reels/reel_{i}.mp4",
                CreatedAt = DateTime.UtcNow.AddMinutes(-100 + i)
            };
            var placement = new MediaPlacement
            {
                Id = Guid.NewGuid(),
                MediaItemId = item.Id,
                Section = MediaConstants.Sections.HomepageReels,
                DisplayOrder = i,
                IsPublished = true,
                CreatedAt = item.CreatedAt
            };
            item.Placements = new List<MediaPlacement> { placement };
            seededItems.Add(item);
            db.MediaItems.Add(item);
        }

        // Oldest item is also placed in GalleryVideos
        var oldest = seededItems[0];
        oldest.Placements.Add(new MediaPlacement
        {
            Id = Guid.NewGuid(),
            MediaItemId = oldest.Id,
            Section = MediaConstants.Sections.GalleryVideos,
            DisplayOrder = 1,
            IsPublished = true,
            CreatedAt = oldest.CreatedAt
        });

        await db.SaveChangesAsync();

        // Upload 21st reel
        var newFile = new TestFormFile(Mp4Header, 10_000_000, "reel_21.mp4", "video/mp4");
        await service.UploadMediaAsync(
            file: newFile,
            sections: new[] { MediaConstants.Sections.HomepageReels },
            mediaType: MediaConstants.MediaTypes.Video,
            title: "Reel 21",
            caption: null,
            altText: null,
            focalPoint: "center",
            category: "General",
            layoutType: "Square",
            displayOrder: 1,
            isPublished: true,
            isFeatured: false,
            clientDurationSeconds: 15.0,
            adminUserId: adminId
        );

        // HomepageReels placement count is 20
        var totalReelPlacements = await db.MediaPlacements
            .CountAsync(p => p.Section == MediaConstants.Sections.HomepageReels);
        Assert.Equal(20, totalReelPlacements);

        // Oldest item HomepageReels placement removed
        var oldestReelPlacement = await db.MediaPlacements
            .FirstOrDefaultAsync(p => p.MediaItemId == oldest.Id && p.Section == MediaConstants.Sections.HomepageReels);
        Assert.Null(oldestReelPlacement);

        // BUT oldest MediaItem STILL EXISTS because of GalleryVideos placement
        var oldestDbItem = await db.MediaItems
            .Include(m => m.Placements)
            .FirstOrDefaultAsync(m => m.Id == oldest.Id);
        Assert.NotNull(oldestDbItem);
        Assert.Single(oldestDbItem.Placements);
        Assert.Equal(MediaConstants.Sections.GalleryVideos, oldestDbItem.Placements.First().Section);

        // R2 object was NOT purged
        Assert.DoesNotContain(oldest.ObjectKey, r2.DeletedKeys);
    }

    [Fact]
    public async Task UploadMediaAsync_ExactReel100MbBoundary_AllowedAndRejected()
    {
        var (service, _, _, _) = CreateTestContext();
        var adminId = Guid.NewGuid();

        // 100 MB exact = 104,857,600 bytes -> should pass
        var valid100Mb = new TestFormFile(Mp4Header, 104_857_600L, "valid_100mb.mp4", "video/mp4");
        var response = await service.UploadMediaAsync(
            file: valid100Mb,
            sections: new[] { MediaConstants.Sections.HomepageReels },
            mediaType: MediaConstants.MediaTypes.Video,
            title: "Reel 100MB",
            caption: null,
            altText: null,
            focalPoint: "center",
            category: "General",
            layoutType: "Square",
            displayOrder: 1,
            isPublished: true,
            isFeatured: false,
            clientDurationSeconds: 10.0,
            adminUserId: adminId
        );
        Assert.NotNull(response);

        // 100 MB + 1 byte = 104,857,601 bytes -> should throw ArgumentException
        var invalid100MbPlus1 = new TestFormFile(Mp4Header, 104_857_601L, "invalid_101mb.mp4", "video/mp4");
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UploadMediaAsync(
            file: invalid100MbPlus1,
            sections: new[] { MediaConstants.Sections.HomepageReels },
            mediaType: MediaConstants.MediaTypes.Video,
            title: "Reel 101MB",
            caption: null,
            altText: null,
            focalPoint: "center",
            category: "General",
            layoutType: "Square",
            displayOrder: 1,
            isPublished: true,
            isFeatured: false,
            clientDurationSeconds: 10.0,
            adminUserId: adminId
        ));

        Assert.Contains("cannot exceed 100 MB", ex.Message);
    }

    [Fact]
    public async Task UploadMediaAsync_ExactImage5MbBoundary_AllowedAndRejected()
    {
        var (service, _, _, _) = CreateTestContext();
        var adminId = Guid.NewGuid();

        // 5 MB exact = 5,242,880 bytes -> should pass
        var valid5Mb = new TestFormFile(JpegHeader, 5_242_880L, "valid_5mb.jpg", "image/jpeg");
        var response = await service.UploadMediaAsync(
            file: valid5Mb,
            sections: new[] { MediaConstants.Sections.GalleryImages },
            mediaType: MediaConstants.MediaTypes.Image,
            title: "Image 5MB",
            caption: null,
            altText: null,
            focalPoint: "center",
            category: "General",
            layoutType: "Square",
            displayOrder: 1,
            isPublished: true,
            isFeatured: false,
            clientDurationSeconds: null,
            adminUserId: adminId
        );
        Assert.NotNull(response);

        // 5 MB + 1 byte = 5,242,881 bytes -> should throw ArgumentException
        var invalid5MbPlus1 = new TestFormFile(JpegHeader, 5_242_881L, "invalid_5mb.jpg", "image/jpeg");
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UploadMediaAsync(
            file: invalid5MbPlus1,
            sections: new[] { MediaConstants.Sections.GalleryImages },
            mediaType: MediaConstants.MediaTypes.Image,
            title: "Image 5.1MB",
            caption: null,
            altText: null,
            focalPoint: "center",
            category: "General",
            layoutType: "Square",
            displayOrder: 1,
            isPublished: true,
            isFeatured: false,
            clientDurationSeconds: null,
            adminUserId: adminId
        ));

        Assert.Contains("exceeds the 5 MB limit", ex.Message);
    }

    [Fact]
    public async Task PresignGalleryVideoUploadAsync_Exact500MbBoundary_AllowedAndRejected()
    {
        var (service, _, _, _) = CreateTestContext();
        var adminId = Guid.NewGuid();

        // 500 MB exact = 524,288,000 bytes -> should pass
        var validReq = new PresignGalleryUploadRequest
        {
            FileName = "dance_performance.mp4",
            FileSizeBytes = 524_288_000L,
            ContentType = "video/mp4"
        };
        var res = await service.PresignGalleryVideoUploadAsync(validReq, adminId);
        Assert.NotNull(res);
        Assert.Contains("media/galleryvideos/pending/", res.ObjectKey);
        Assert.Contains("mock-presigned-url", res.UploadUrl);

        // 500 MB + 1 byte = 524,288,001 bytes -> should throw ArgumentException
        var invalidReq = new PresignGalleryUploadRequest
        {
            FileName = "oversized_performance.mp4",
            FileSizeBytes = 524_288_001L,
            ContentType = "video/mp4"
        };
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.PresignGalleryVideoUploadAsync(invalidReq, adminId));
        Assert.Contains("exceeds the 500 MB limit", ex.Message);
    }

    [Fact]
    public async Task ConfirmGalleryVideoUploadAsync_WhenR2ObjectMissing_ThrowsArgumentException()
    {
        var (service, _, _, _) = CreateTestContext();
        var adminId = Guid.NewGuid();

        var confirmReq = new ConfirmGalleryUploadRequest
        {
            ObjectKey = "media/galleryvideos/pending/nonexistent.mp4",
            Title = "Non Existent Video"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.ConfirmGalleryVideoUploadAsync(confirmReq, adminId));
        Assert.Contains("was not found in Cloudflare R2", ex.Message);
    }

    [Fact]
    public async Task ConfirmGalleryVideoUploadAsync_WhenActualR2SizeExceeds500Mb_RejectsAndPurgesObject()
    {
        var (service, _, r2, _) = CreateTestContext();
        var adminId = Guid.NewGuid();

        var key = "media/galleryvideos/pending/oversized.mp4";
        // Attacker claims valid size during presign but uploads 501 MB to R2
        r2.ObjectMetadataMap[key] = new R2ObjectMetadata
        {
            ContentLength = 524_288_001L,
            ContentType = "video/mp4"
        };

        var confirmReq = new ConfirmGalleryUploadRequest
        {
            ObjectKey = key,
            Title = "Oversized Video Attack"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.ConfirmGalleryVideoUploadAsync(confirmReq, adminId));
        Assert.Contains("exceeds maximum permitted size", ex.Message);
        // Verify R2 object was purged
        Assert.Contains(key, r2.DeletedKeys);
    }

    [Fact]
    public async Task ConfirmGalleryVideoUploadAsync_WhenVerified_CreatesMediaItemAndPlacement()
    {
        var (service, db, r2, audit) = CreateTestContext();
        var adminId = Guid.NewGuid();

        var key = "media/galleryvideos/pending/real_dance_perf.mp4";
        r2.ObjectMetadataMap[key] = new R2ObjectMetadata
        {
            ContentLength = 250_000_000L, // 250 MB
            ContentType = "video/mp4"
        };

        var confirmReq = new ConfirmGalleryUploadRequest
        {
            ObjectKey = key,
            Title = "Annual Recital 2026",
            Caption = "Grand finale showcase performance",
            Category = "HipHop",
            DisplayOrder = 2,
            IsPublished = true,
            IsFeatured = true
        };

        var result = await service.ConfirmGalleryVideoUploadAsync(confirmReq, adminId);

        Assert.NotNull(result);
        Assert.Equal("Annual Recital 2026", result.Title);
        Assert.Equal(key, result.ObjectKey);

        var dbItem = await db.MediaItems
            .Include(m => m.Placements)
            .FirstOrDefaultAsync(m => m.ObjectKey == key);

        Assert.NotNull(dbItem);
        Assert.Equal(250_000_000L, dbItem.FileSizeBytes);
        Assert.Equal("video/mp4", dbItem.MimeType);
        Assert.Single(dbItem.Placements);
        var firstPlacement = dbItem.Placements.First();
        Assert.Equal(MediaConstants.Sections.GalleryVideos, firstPlacement.Section);
        Assert.Equal(2, firstPlacement.DisplayOrder);
        Assert.True(firstPlacement.IsFeatured);

        Assert.Contains(audit.LoggedActions, a => a.action == "MEDIA_UPLOAD_PRESIGNED_CONFIRMED");
    }

    [Fact]
    public async Task MigrateReactAssetsAsync_SeedsAllAssetsAndPreventsDuplicates()
    {
        var (service, db, r2, audit) = CreateTestContext();
        var adminId = Guid.NewGuid();

        var tempWebRoot = Path.Combine(Path.GetTempPath(), "EthosWebMigrationTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var relativePaths = new[]
            {
                "src/assets/hero/hero-01.jpg",
                "src/assets/hero/hero-02.jpg",
                "src/assets/hero/hero-video.mp4",
                "src/assets/hero/hero-03.jpg",
                "src/assets/hero/hero-04.jpg",
                "src/assets/hero/hero-05.jpg",
                "src/assets/hero/hero-06.jpg",
                "src/assets/hero/sample-test-video.mp4",
                "src/assets/gallery/ethos-visual-reel.mp4",
                "src/assets/workshops/workshop-01.jpg",
                "src/assets/workshops/workshop-02.jpg",
                "src/assets/workshops/workshop-03.jpg",
                "src/assets/workshops/workshop-04.jpg",
                "src/assets/about/about-main.jpg",
                "src/assets/about/about-secondary.jpg",
                "src/assets/about/about-community.jpg",
                "src/assets/founders/founders.jpg",
                "src/assets/trainers/trainer-01.jpg",
                "src/assets/trainers/trainer-02.jpg",
                "src/assets/trainers/trainer-03.jpg",
                "src/assets/trainers/trainer-04.jpg",
                "src/assets/shanmuka.jpg"
            };

            foreach (var rel in relativePaths)
            {
                var full = Path.Combine(tempWebRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                await File.WriteAllBytesAsync(full, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 });
            }

            // 1. First run: should migrate all 22 Category A assets
            var result1 = await service.MigrateReactAssetsAsync(tempWebRoot, adminId);

            Assert.Equal(22, result1.TotalFound);
            Assert.Equal(22, result1.MigratedCount);
            Assert.Equal(0, result1.AlreadyExistedCount);
            Assert.Equal(0, result1.ErrorCount);

            var totalItems = await db.MediaItems.CountAsync();
            Assert.Equal(22, totalItems);

            // Verify Hero items exist
            var hero1 = await db.MediaItems.Include(m => m.Placements).FirstOrDefaultAsync(m => m.OriginalFileName == "hero-01.jpg");
            Assert.NotNull(hero1);
            Assert.Equal("In Motion - Contemporary Leap", hero1.Title);
            Assert.Equal(2, hero1.Placements.Count);
            Assert.Contains(hero1.Placements, p => p.Section == MediaConstants.Sections.HomepageScrolling);
            Assert.Contains(hero1.Placements, p => p.Section == MediaConstants.Sections.GalleryImages);

            // 2. Second run: idempotent check, 0 newly migrated, 22 already exist
            var result2 = await service.MigrateReactAssetsAsync(tempWebRoot, adminId);
            Assert.Equal(22, result2.TotalFound);
            Assert.Equal(0, result2.MigratedCount);
            Assert.Equal(22, result2.AlreadyExistedCount);
            Assert.Equal(0, result2.ErrorCount);

            var totalItemsAfter = await db.MediaItems.CountAsync();
            Assert.Equal(22, totalItemsAfter);
        }
        finally
        {
            if (Directory.Exists(tempWebRoot))
            {
                try { Directory.Delete(tempWebRoot, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task DeleteMediaAsync_PermanentDelete_RemovesDbRecordsAndPurgesAllR2Keys()
    {
        var (service, db, r2, audit) = CreateTestContext();
        var adminId = Guid.NewGuid();

        var primaryKey = "media/galleryimages/dance_solo.jpg";
        var thumbKey = "media/thumbnails/dance_solo_thumb.jpg";
        var posterKey = "media/posters/dance_solo_poster.jpg";

        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            Title = "Solo Dance",
            ObjectKey = primaryKey,
            ThumbnailObjectKey = thumbKey,
            PosterObjectKey = posterKey,
            PublicUrl = $"https://media.ethosdancestudio.com/{primaryKey}",
            ThumbnailUrl = $"https://media.ethosdancestudio.com/{thumbKey}",
            Placements = new List<MediaPlacement>
            {
                new() { Id = Guid.NewGuid(), Section = MediaConstants.Sections.GalleryImages, DisplayOrder = 1, IsPublished = true }
            }
        };

        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync();

        // Perform permanent delete
        await service.DeleteMediaAsync(mediaItem.Id, permanent: true, adminUserId: adminId);

        // Assert DB records removed
        Assert.Null(await db.MediaItems.FindAsync(mediaItem.Id));
        Assert.False(await db.MediaPlacements.AnyAsync(p => p.MediaItemId == mediaItem.Id));

        // Assert all physical keys purged from R2
        Assert.Contains(primaryKey, r2.DeletedKeys);
        Assert.Contains(thumbKey, r2.DeletedKeys);
        Assert.Contains(posterKey, r2.DeletedKeys);

        // Assert audit logged
        Assert.Contains(audit.LoggedActions, a => a.action == "MEDIA_PERMANENT_DELETE");
    }

    [Fact]
    public async Task DeleteMediaAsync_SharedObjectKey_DoesNotPurgeR2UntilLastRecordDeleted()
    {
        var (service, db, r2, _) = CreateTestContext();
        var adminId = Guid.NewGuid();

        var sharedKey = "media/shared/studio_common.jpg";

        var item1 = new MediaItem
        {
            Id = Guid.NewGuid(),
            Title = "Studio Angle A",
            ObjectKey = sharedKey,
            PublicUrl = $"https://media.ethosdancestudio.com/{sharedKey}",
            Placements = new List<MediaPlacement>
            {
                new() { Id = Guid.NewGuid(), Section = MediaConstants.Sections.AboutEthos, DisplayOrder = 1 }
            }
        };

        var item2 = new MediaItem
        {
            Id = Guid.NewGuid(),
            Title = "Studio Angle B",
            ObjectKey = sharedKey,
            PublicUrl = $"https://media.ethosdancestudio.com/{sharedKey}",
            Placements = new List<MediaPlacement>
            {
                new() { Id = Guid.NewGuid(), Section = MediaConstants.Sections.HomepageScrolling, DisplayOrder = 10 }
            }
        };

        db.MediaItems.AddRange(item1, item2);
        await db.SaveChangesAsync();

        // 1. Delete item 1: item 2 still references sharedKey, so R2 should NOT delete sharedKey yet
        await service.DeleteMediaAsync(item1.Id, permanent: true, adminUserId: adminId);

        Assert.Null(await db.MediaItems.FindAsync(item1.Id));
        Assert.NotNull(await db.MediaItems.FindAsync(item2.Id));
        Assert.DoesNotContain(sharedKey, r2.DeletedKeys);

        // 2. Delete item 2: no other item references sharedKey, so R2 MUST now delete sharedKey
        await service.DeleteMediaAsync(item2.Id, permanent: true, adminUserId: adminId);

        Assert.Null(await db.MediaItems.FindAsync(item2.Id));
        Assert.Contains(sharedKey, r2.DeletedKeys);
    }

    [Fact]
    public async Task DeleteMediaAsync_WhenReferencedByWorkshopOrTrainer_ThrowsAndBlocksDeletion()
    {
        var (service, db, r2, _) = CreateTestContext();
        var adminId = Guid.NewGuid();

        var mediaUrl = "https://media.ethosdancestudio.com/media/workshop/w1.jpg";
        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            Title = "Workshop 1 Poster",
            ObjectKey = "media/workshop/w1.jpg",
            PublicUrl = mediaUrl
        };
        db.MediaItems.Add(mediaItem);

        // Reference by active workshop
        var workshop = new Ethos.Api.Domain.Entities.Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Live Workshop",
            ImageUrl = mediaUrl,
            Description = "Test",
            DanceStyle = "Hip Hop",
            WorkshopDate = DateTime.UtcNow.AddDays(5),
            StartTime = TimeSpan.FromHours(18),
            EndTime = TimeSpan.FromHours(20),
            Price = 499m
        };
        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();

        var exWorkshop = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteMediaAsync(mediaItem.Id, permanent: true, adminUserId: adminId));
        Assert.Contains("Workshop poster/banner image", exWorkshop.Message);

        // Remove workshop reference and add trainer reference
        workshop.ImageUrl = "https://other.com/image.jpg";
        var trainer = new Ethos.Api.Domain.Entities.TrainerProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TrainerCode = "ETH-TR-001",
            FullName = "Test Trainer",
            ProfilePhotoUrl = mediaUrl
        };
        db.TrainerProfiles.Add(trainer);
        await db.SaveChangesAsync();

        var exTrainer = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteMediaAsync(mediaItem.Id, permanent: true, adminUserId: adminId));
        Assert.Contains("Trainer profile photo", exTrainer.Message);
    }

    [Fact]
    public async Task UploadMediaAsync_FoundersPlacement_CreatesValidPlacementAndReturnsInPublicApi()
    {
        var (service, db, r2, _) = CreateTestContext();
        var adminId = Guid.NewGuid();

        var file = new TestFormFile(JpegHeader, 1_500_000, "founders_portrait.jpg", "image/jpeg");
        var response = await service.UploadMediaAsync(
            file: file,
            sections: new[] { MediaConstants.Sections.Founders },
            mediaType: MediaConstants.MediaTypes.Image,
            title: "Founders Feature Portrait",
            caption: "Co-Founders portrait on homepage",
            altText: "Founders",
            focalPoint: "center",
            category: "Workshop",
            layoutType: "Landscape",
            displayOrder: 1,
            isPublished: true,
            isFeatured: true,
            clientDurationSeconds: null,
            adminUserId: adminId
        );

        Assert.NotNull(response);
        var dbItem = await db.MediaItems.Include(m => m.Placements).FirstOrDefaultAsync(m => m.Id == response.Id);
        Assert.NotNull(dbItem);
        Assert.Single(dbItem.Placements);
        Assert.Equal(MediaConstants.Sections.Founders, dbItem.Placements.First().Section);
        Assert.Equal(1, dbItem.Placements.First().DisplayOrder);

        var publicItems = await service.GetPublicMediaAsync(MediaConstants.Sections.Founders, null, null);
        Assert.Single(publicItems);
        Assert.Equal("Founders Feature Portrait", publicItems[0].Title);
    }

    [Fact]
    public async Task UploadMediaAsync_FixedSlotHeroBanner_AssignsExactDisplayOrder()
    {
        var (service, db, r2, _) = CreateTestContext();
        var adminId = Guid.NewGuid();

        var file = new TestFormFile(JpegHeader, 2_000_000, "hero_slot_02.jpg", "image/jpeg");
        var response = await service.UploadMediaAsync(
            file: file,
            sections: new[] { MediaConstants.Sections.HomepageScrolling },
            mediaType: MediaConstants.MediaTypes.Image,
            title: "Hero Slot 02 Movement",
            caption: "Atmospheric movement visual",
            altText: "Hero 02",
            focalPoint: "center",
            category: "Workshop",
            layoutType: "Landscape",
            displayOrder: 2,
            isPublished: true,
            isFeatured: false,
            clientDurationSeconds: null,
            adminUserId: adminId
        );

        Assert.NotNull(response);
        var dbPlacement = await db.MediaPlacements.FirstOrDefaultAsync(p => p.MediaItemId == response.Id);
        Assert.NotNull(dbPlacement);
        Assert.Equal(MediaConstants.Sections.HomepageScrolling, dbPlacement.Section);
        Assert.Equal(2, dbPlacement.DisplayOrder);
    }

    [Theory]
    [InlineData("Workshop", MediaConstants.Categories.Workshop)]
    [InlineData("In Studio", MediaConstants.Categories.InStudio)]
    [InlineData("Events", MediaConstants.Categories.Events)]
    [InlineData("Performances", MediaConstants.Categories.Performances)]
    [InlineData("Behind the Scenes", MediaConstants.Categories.BehindTheScenes)]
    [InlineData("Other", MediaConstants.Categories.Other)]
    public async Task UploadMediaAsync_OfficialGalleryCategories_NormalizesCorrectly(string inputCategory, string expectedCategory)
    {
        var (service, db, _, _) = CreateTestContext();
        var adminId = Guid.NewGuid();

        var file = new TestFormFile(JpegHeader, 1_000_000, "photo.jpg", "image/jpeg");
        var response = await service.UploadMediaAsync(
            file: file,
            sections: new[] { MediaConstants.Sections.GalleryImages },
            mediaType: MediaConstants.MediaTypes.Image,
            title: $"Category test {inputCategory}",
            caption: null,
            altText: null,
            focalPoint: "center",
            category: inputCategory,
            layoutType: "Square",
            displayOrder: 1,
            isPublished: true,
            isFeatured: false,
            clientDurationSeconds: null,
            adminUserId: adminId
        );

        Assert.NotNull(response);
        var dbItem = await db.MediaItems.FindAsync(response.Id);
        Assert.NotNull(dbItem);
        Assert.Equal(expectedCategory, dbItem.Category);
    }
}

