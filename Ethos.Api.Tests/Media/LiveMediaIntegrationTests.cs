using System.Text;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Media;
using Ethos.Api.Application.Storage;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Ethos.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Media;

public class LiveMediaIntegrationTests
{
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

        public void CopyTo(Stream target) => target.Write(_headerBytes, 0, _headerBytes.Length);
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
            target.WriteAsync(_headerBytes, 0, _headerBytes.Length, cancellationToken);

        public Stream OpenReadStream()
        {
            var ms = new MemoryStream();
            ms.Write(_headerBytes, 0, _headerBytes.Length);
            ms.Position = 0;
            return ms;
        }
    }

    private static readonly byte[] JpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1 };
    private static readonly byte[] Mp4Header = new byte[] { 0, 0, 0, 0, 0x66, 0x74, 0x79, 0x70, 0, 0, 0, 0 };

    private (MediaService service, AppDbContext db, ICloudflareR2StorageService r2, Guid adminId) CreateTestContext()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new AppDbContext(dbOptions);

        // Build config from user-secrets / appsettings
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .Build();

        ICloudflareR2StorageService r2Service = new FakeR2StorageService();

        var audit = new FakeAdminAuditService();
        var cache = new FakeMediaCacheService();
        var fastStart = new FakeMediaFastStartService();
        var service = new MediaService(db, r2Service, audit, cache, fastStart, NullLogger<MediaService>.Instance);
        var adminId = Guid.NewGuid();

        return (service, db, r2Service, adminId);
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

    private class FakeR2StorageService : ICloudflareR2StorageService
    {
        public List<string> StoredKeys { get; } = new();
        public List<string> DeletedKeys { get; } = new();

        public Task<R2UploadResult> UploadAsync(Stream stream, string originalFileName, string contentType, string section, CancellationToken cancellationToken = default)
        {
            var key = $"media/{section.ToLowerInvariant()}/{Guid.NewGuid():N}-{originalFileName}";
            StoredKeys.Add(key);
            return Task.FromResult(new R2UploadResult
            {
                ObjectKey = key,
                PublicUrl = $"https://media.ethosdancestudio.com/{key}",
                ContentType = contentType,
                FileSizeBytes = stream.Length,
                Checksum = "mock-chk"
            });
        }

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            DeletedKeys.Add(objectKey);
            StoredKeys.Remove(objectKey);
            return Task.CompletedTask;
        }

        public string GetPublicUrl(string objectKey) => $"https://media.ethosdancestudio.com/{objectKey}";
        public string GeneratePreSignedGetUrl(string objectKey, TimeSpan duration) => $"https://media.ethosdancestudio.com/{objectKey}";
        public string GeneratePreSignedPutUrl(string objectKey, string contentType, TimeSpan duration) => $"https://r2.cloudflarestorage.com/{objectKey}";
        public Task<R2ObjectMetadata?> GetObjectMetadataAsync(string objectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<R2ObjectMetadata?>(new R2ObjectMetadata { ContentLength = 1000, ContentType = "image/jpeg" });
        public Task<(Stream Stream, string ContentType)?> GetObjectStreamAsync(string objectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<(Stream Stream, string ContentType)?>(null);
        public Task<R2RangeResult?> GetObjectRangeStreamAsync(string objectKey, long? fromByte, long? toByte, CancellationToken cancellationToken = default) =>
            Task.FromResult<R2RangeResult?>(null);
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

    private class FakeMediaCacheService : IMediaCacheService
    {
        public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) => factory();
        public void InvalidateAllMediaCache() { }
        public string NormalizeCacheKey(string? section, string? category, string? mediaType) => $"{section}:{category}:{mediaType}";
    }

    [Fact]
    public async Task CompletePDFVerificationWorkflow_FixedSlot_Replacement_Clear_And_Collections()
    {
        var (service, db, r2, adminId) = CreateTestContext();

        // ── 1. Upload small test image into Hero Slot 02 (fixed slot) ─────────
        var testFile = new TestFormFile(JpegHeader, 500_000, "hero_slot_02_test.jpg", "image/jpeg");
        var uploadRes = await service.UploadMediaAsync(
            file: testFile,
            sections: new[] { MediaConstants.Sections.HomepageScrolling },
            mediaType: MediaConstants.MediaTypes.Image,
            title: "Hero Slot 02 Contemporary Flow",
            caption: "Choreography in motion",
            altText: "Hero Slot 02",
            focalPoint: "center",
            category: MediaConstants.Categories.Workshop,
            layoutType: MediaConstants.LayoutTypes.Landscape,
            displayOrder: 2,
            isPublished: true,
            isFeatured: false,
            clientDurationSeconds: null,
            adminUserId: adminId
        );

        Assert.NotNull(uploadRes);
        Assert.NotEqual(Guid.Empty, uploadRes.Id);

        // ── 2. Verify R2 object key exists ────────────────────────────────────
        Assert.False(string.IsNullOrWhiteSpace(uploadRes.ObjectKey));
        Assert.StartsWith("media/homepagescrolling/", uploadRes.ObjectKey);

        // ── 3. Verify Database MediaItem & MediaPlacement record exists ────────
        var dbItem = await db.MediaItems.Include(m => m.Placements).FirstOrDefaultAsync(m => m.Id == uploadRes.Id);
        Assert.NotNull(dbItem);
        Assert.Equal("Hero Slot 02 Contemporary Flow", dbItem.Title);
        Assert.Single(dbItem.Placements);

        var dbPlacement = dbItem.Placements.First();
        Assert.Equal(MediaConstants.Sections.HomepageScrolling, dbPlacement.Section);
        Assert.Equal(2, dbPlacement.DisplayOrder);
        Assert.True(dbPlacement.IsPublished);

        // ── 4. Refresh Admin Media Library & confirm uploaded asset remains assigned
        var adminPaged = await service.GetAdminMediaPagedAsync(
            page: 1, pageSize: 50, section: MediaConstants.Sections.HomepageScrolling,
            mediaType: null, category: null, isPublished: null, isArchived: null);

        Assert.Contains(adminPaged.Items, m => m.Id == uploadRes.Id && m.Placements.Any(p => p.DisplayOrder == 2));

        // ── 5. Open corresponding public feed & verify image appears in correct location
        var publicFeed = await service.GetPublicMediaAsync(MediaConstants.Sections.HomepageScrolling, null, null);
        var publicHero02 = publicFeed.FirstOrDefault(p => p.DisplayOrder == 2);
        Assert.NotNull(publicHero02);
        Assert.Equal(uploadRes.Id, publicHero02.Id);
        Assert.Equal("Hero Slot 02 Contemporary Flow", publicHero02.Title);

        // ── 6. Test Replacement Behavior ──────────────────────────────────────
        var replaceFile = new TestFormFile(JpegHeader, 600_000, "hero_slot_02_replacement.jpg", "image/jpeg");
        var replaceRes = await service.UploadMediaAsync(
            file: replaceFile,
            sections: new[] { MediaConstants.Sections.HomepageScrolling },
            mediaType: MediaConstants.MediaTypes.Image,
            title: "Hero Slot 02 Replaced Visual",
            caption: "New choreography",
            altText: "Hero Slot 02 New",
            focalPoint: "center",
            category: MediaConstants.Categories.InStudio,
            layoutType: MediaConstants.LayoutTypes.Landscape,
            displayOrder: 2,
            isPublished: true,
            isFeatured: false,
            clientDurationSeconds: null,
            adminUserId: adminId
        );

        // Atomic supersession in UploadMediaAsync automatically superseded old placement at slot 2
        var oldPlacementStillExists = await db.MediaPlacements.AnyAsync(p => p.Id == dbPlacement.Id);
        Assert.False(oldPlacementStillExists);

        var updatedPublicFeed = await service.GetPublicMediaAsync(MediaConstants.Sections.HomepageScrolling, null, null);
        var updatedHero02 = updatedPublicFeed.FirstOrDefault(p => p.DisplayOrder == 2);
        Assert.NotNull(updatedHero02);
        Assert.Equal(replaceRes.Id, updatedHero02.Id);
        Assert.Equal("Hero Slot 02 Replaced Visual", updatedHero02.Title);

        // ── 7. Test Empty-Slot Behavior (Clear slot) ─────────────────────────
        var newPlacement = await db.MediaPlacements.FirstOrDefaultAsync(p => p.MediaItemId == replaceRes.Id);
        Assert.NotNull(newPlacement);
        await service.RemovePlacementAsync(newPlacement.Id, adminId);

        var feedAfterClear = await service.GetPublicMediaAsync(MediaConstants.Sections.HomepageScrolling, null, null);
        Assert.DoesNotContain(feedAfterClear, p => p.DisplayOrder == 2);

        // ── 8. Test Collection Upload & Limit (HomepageReels max 20) ──────────
        for (int i = 1; i <= 3; i++)
        {
            var reelFile = new TestFormFile(Mp4Header, 1_000_000, $"reel_{i}.mp4", "video/mp4");
            await service.UploadMediaAsync(
                file: reelFile,
                sections: new[] { MediaConstants.Sections.HomepageReels },
                mediaType: MediaConstants.MediaTypes.Video,
                title: $"Collection Reel {i}",
                caption: null,
                altText: null,
                focalPoint: "center",
                category: MediaConstants.Categories.Performances,
                layoutType: MediaConstants.LayoutTypes.Portrait,
                displayOrder: i,
                isPublished: true,
                isFeatured: false,
                clientDurationSeconds: 20.0,
                adminUserId: adminId
            );
        }

        var reelsFeed = await service.GetPublicMediaAsync(MediaConstants.Sections.HomepageReels, null, null);
        Assert.Equal(3, reelsFeed.Count);
        Assert.Equal(1, reelsFeed[0].DisplayOrder);
        Assert.Equal(2, reelsFeed[1].DisplayOrder);
        Assert.Equal(3, reelsFeed[2].DisplayOrder);
    }
}
