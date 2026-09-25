using Ethos.Api.Application.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ethos.Api.Tests.Storage;

public class R2TrainerStorageTests
{
    private class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Ethos.Api";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private class FakeCloudflareR2Storage : ICloudflareR2StorageService
    {
        public int UploadCount { get; private set; }
        public int DeleteCount { get; private set; }
        public string LastUploadedSection { get; private set; } = string.Empty;
        public string LastUploadedFileName { get; private set; } = string.Empty;
        public string LastDeletedKey { get; private set; } = string.Empty;

        public Task<R2UploadResult> UploadAsync(
            Stream stream,
            string originalFileName,
            string contentType,
            string section,
            CancellationToken cancellationToken = default)
        {
            UploadCount++;
            LastUploadedSection = section;
            LastUploadedFileName = originalFileName;
            var objectKey = $"{section}/{originalFileName}";
            return Task.FromResult(new R2UploadResult
            {
                ObjectKey = objectKey,
                PublicUrl = $"https://media.ethosdancestudio.com/{objectKey}",
                ContentType = contentType,
                FileSizeBytes = stream.Length
            });
        }

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            DeleteCount++;
            LastDeletedKey = objectKey;
            return Task.CompletedTask;
        }

        public string GetPublicUrl(string objectKey) => $"https://media.ethosdancestudio.com/{objectKey}";
        public string GeneratePreSignedGetUrl(string objectKey, TimeSpan duration) => $"https://media.ethosdancestudio.com/{objectKey}?token=fake";
        public string GeneratePreSignedPutUrl(string objectKey, string contentType, TimeSpan duration) => $"https://media.ethosdancestudio.com/{objectKey}?token=fake_put";
        public Task<R2ObjectMetadata?> GetObjectMetadataAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult<R2ObjectMetadata?>(null);
        public Task<(Stream Stream, string ContentType)?> GetObjectStreamAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult<(Stream Stream, string ContentType)?>(null);
        public Task<R2RangeResult?> GetObjectRangeStreamAsync(string objectKey, long? fromByte, long? toByte, CancellationToken cancellationToken = default) => Task.FromResult<R2RangeResult?>(null);
    }

    private class FakeFormFile : IFormFile
    {
        private readonly byte[] _content;

        public FakeFormFile(byte[] content, string fileName, string contentType = "image/jpeg")
        {
            _content = content;
            FileName = fileName;
            ContentType = contentType;
        }

        public string ContentType { get; }
        public string ContentDisposition => "form-data";
        public IHeaderDictionary Headers => new HeaderDictionary();
        public long Length => _content.Length;
        public string Name => "file";
        public string FileName { get; }

        public void CopyTo(Stream target) => target.Write(_content, 0, _content.Length);
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => target.WriteAsync(_content, 0, _content.Length, cancellationToken);
        public Stream OpenReadStream() => new MemoryStream(_content);
    }

    [Fact]
    public async Task VideoStorage_SaveVideoAsync_RejectsInvalidExtension()
    {
        var fakeR2 = new FakeCloudflareR2Storage();
        var fakeEnv = new FakeWebHostEnvironment();
        var service = new R2TrainerApplicationVideoStorageService(fakeR2, fakeEnv, NullLogger<R2TrainerApplicationVideoStorageService>.Instance);

        using var ms = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveVideoAsync(ms, "danger.exe", "video/mp4", Guid.NewGuid(), CancellationToken.None));

        Assert.Contains("Only MP4, MOV, WEBM, and M4V", ex.Message);
    }

    [Fact]
    public async Task VideoStorage_SaveVideoAsync_UploadsToAuthoritativeR2()
    {
        var fakeR2 = new FakeCloudflareR2Storage();
        var fakeEnv = new FakeWebHostEnvironment();
        var service = new R2TrainerApplicationVideoStorageService(fakeR2, fakeEnv, NullLogger<R2TrainerApplicationVideoStorageService>.Instance);

        var appId = Guid.NewGuid();
        using var ms = new MemoryStream(new byte[] { 0, 0, 0, 20, 102, 116, 121, 112 }); // fake mp4 header

        var resultKey = await service.SaveVideoAsync(ms, "audition.mp4", "video/mp4", appId, CancellationToken.None);

        Assert.Equal($"trainer-videos/{appId}/audition.mp4", resultKey);
        Assert.Equal(1, fakeR2.UploadCount);
        Assert.Equal($"trainer-videos/{appId}", fakeR2.LastUploadedSection);
    }

    [Fact]
    public async Task VideoStorage_DeleteVideoAsync_DeletesFromAuthoritativeR2()
    {
        var fakeR2 = new FakeCloudflareR2Storage();
        var fakeEnv = new FakeWebHostEnvironment();
        var service = new R2TrainerApplicationVideoStorageService(fakeR2, fakeEnv, NullLogger<R2TrainerApplicationVideoStorageService>.Instance);

        var storageKey = "trainer-videos/app123/sample.mp4";
        await service.DeleteVideoAsync(storageKey, CancellationToken.None);

        Assert.Equal(1, fakeR2.DeleteCount);
        Assert.Equal(storageKey, fakeR2.LastDeletedKey);
    }

    [Fact]
    public async Task GalleryStorage_SaveAsync_RejectsInvalidImageSignature()
    {
        var fakeR2 = new FakeCloudflareR2Storage();
        var fakeEnv = new FakeWebHostEnvironment();
        var service = new R2TrainerGalleryStorageService(fakeR2, fakeEnv, NullLogger<R2TrainerGalleryStorageService>.Instance);

        var invalidFile = new FakeFormFile(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, "photo.jpg");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAsync(Guid.NewGuid(), invalidFile, CancellationToken.None));

        Assert.Contains("image format", ex.Message);
    }

    [Fact]
    public async Task GalleryStorage_SaveAsync_UploadsValidJpgToR2()
    {
        var fakeR2 = new FakeCloudflareR2Storage();
        var fakeEnv = new FakeWebHostEnvironment();
        var service = new R2TrainerGalleryStorageService(fakeR2, fakeEnv, NullLogger<R2TrainerGalleryStorageService>.Instance);

        var profileId = Guid.NewGuid();
        // Valid JPEG SOI marker 0xFF, 0xD8, 0xFF
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 };
        var validFile = new FakeFormFile(jpegBytes, "dance_pose.jpg");

        var result = await service.SaveAsync(profileId, validFile, CancellationToken.None);

        Assert.Equal($"trainer-gallery/{profileId}/dance_pose.jpg", result);
        Assert.Equal(1, fakeR2.UploadCount);
        Assert.Equal($"trainer-gallery/{profileId}", fakeR2.LastUploadedSection);
    }

    [Fact]
    public async Task GalleryStorage_DeleteAsync_DeletesFromAuthoritativeR2()
    {
        var fakeR2 = new FakeCloudflareR2Storage();
        var fakeEnv = new FakeWebHostEnvironment();
        var service = new R2TrainerGalleryStorageService(fakeR2, fakeEnv, NullLogger<R2TrainerGalleryStorageService>.Instance);

        var storageKey = "trainer-gallery/profile123/image1.jpg";
        await service.DeleteAsync(storageKey, CancellationToken.None);

        Assert.Equal(1, fakeR2.DeleteCount);
        Assert.Equal(storageKey, fakeR2.LastDeletedKey);
    }
}
