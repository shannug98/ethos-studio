using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Application.Storage;

public sealed class R2TrainerApplicationVideoStorageService : ITrainerApplicationVideoStorageService
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4",
            ".mov",
            ".webm",
            ".m4v"
        };

    private static readonly HashSet<string> AllowedMimeTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "video/mp4",
            "video/quicktime",
            "video/webm",
            "video/x-m4v"
        };

    private const long MaxFileSizeBytes = 100 * 1024 * 1024; // 100 MB

    private readonly ICloudflareR2StorageService _r2Storage;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<R2TrainerApplicationVideoStorageService> _logger;

    public R2TrainerApplicationVideoStorageService(
        ICloudflareR2StorageService r2Storage,
        IWebHostEnvironment environment,
        ILogger<R2TrainerApplicationVideoStorageService> logger)
    {
        _r2Storage = r2Storage;
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> SaveVideoAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        if (fileStream is null)
            throw new ArgumentNullException(nameof(fileStream));

        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("Video file name is required.", nameof(originalFileName));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Video content type is required.", nameof(contentType));

        if (fileStream.CanSeek && fileStream.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("Video file size cannot exceed 100 MB.");
        }

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Only MP4, MOV, WEBM, and M4V video files are allowed.");
        }

        if (!AllowedMimeTypes.Contains(contentType))
        {
            throw new InvalidOperationException("The selected video format is not supported.");
        }

        // Ephemeral local cache directory for MediaInfo metadata inspection
        var cacheDirectory = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "uploads",
            "cache",
            "trainer-videos",
            applicationId.ToString("N"));

        Directory.CreateDirectory(cacheDirectory);

        var safeExtension = extension.ToLowerInvariant();
        var generatedFileName = $"{Guid.NewGuid():N}{safeExtension}";
        var ephemeralPath = Path.Combine(cacheDirectory, generatedFileName);

        // Buffer stream if not seekable so we can write to both local cache and R2
        byte[] videoBytes;
        if (fileStream is MemoryStream ms)
        {
            videoBytes = ms.ToArray();
        }
        else
        {
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream, cancellationToken);
            videoBytes = memoryStream.ToArray();
        }

        // Write ephemeral local copy
        await File.WriteAllBytesAsync(ephemeralPath, videoBytes, cancellationToken);

        // Upload to authoritative R2 storage
        using var uploadStream = new MemoryStream(videoBytes);
        var uploadResult = await _r2Storage.UploadAsync(
            uploadStream,
            originalFileName,
            contentType,
            $"trainer-videos/{applicationId}",
            cancellationToken);

        _logger.LogInformation(
            "Trainer application video uploaded to authoritative R2: {ObjectKey} (application {ApplicationId})",
            uploadResult.ObjectKey,
            applicationId);

        // Store authoritative object key
        return uploadResult.ObjectKey;
    }

    public async Task DeleteVideoAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        var objectKey = ExtractObjectKey(filePath);

        // Delete authoritative copy from R2
        try
        {
            await _r2Storage.DeleteAsync(objectKey, cancellationToken);
            _logger.LogInformation("Deleted authoritative trainer application video from R2: {ObjectKey}", objectKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete trainer application video from R2: {ObjectKey}", objectKey);
            if (!_environment.IsDevelopment())
            {
                throw;
            }
        }

        // Clean up ephemeral local cache if it exists
        try
        {
            var fileName = Path.GetFileName(objectKey);
            var cacheBase = Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", "cache", "trainer-videos");
            if (Directory.Exists(cacheBase))
            {
                var matchingFiles = Directory.GetFiles(cacheBase, fileName, SearchOption.AllDirectories);
                foreach (var file in matchingFiles)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ephemeral cache cleanup for {FilePath} encountered non-critical error.", filePath);
        }
    }

    public string GetPhysicalPath(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new ArgumentException("Storage path cannot be empty.", nameof(storagePath));
        }

        var objectKey = ExtractObjectKey(storagePath);
        var fileName = Path.GetFileName(objectKey);

        // 1. Check ephemeral cache
        var cacheBase = Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", "cache", "trainer-videos");
        if (Directory.Exists(cacheBase))
        {
            var matchingFiles = Directory.GetFiles(cacheBase, fileName, SearchOption.AllDirectories);
            if (matchingFiles.Length > 0 && File.Exists(matchingFiles[0]))
            {
                return matchingFiles[0];
            }
        }

        // 2. Check legacy local uploads mirror
        var localMirrorPath = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "uploads",
            storagePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));

        if (File.Exists(localMirrorPath))
        {
            return localMirrorPath;
        }

        // 3. Fallback: on-demand download from authoritative R2 to ephemeral cache
        try
        {
            var r2StreamInfo = _r2Storage.GetObjectStreamAsync(objectKey).GetAwaiter().GetResult();
            if (r2StreamInfo != null)
            {
                var ephemeralDir = Path.Combine(cacheBase, "ondemand");
                Directory.CreateDirectory(ephemeralDir);
                var targetPath = Path.Combine(ephemeralDir, fileName);

                using (var dest = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    r2StreamInfo.Value.Stream.CopyTo(dest);
                }

                _logger.LogInformation("Restored video {ObjectKey} from R2 to ephemeral cache at {Path}", objectKey, targetPath);
                return targetPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve video {ObjectKey} from R2 for ephemeral cache.", objectKey);
        }

        // Return path even if not yet on disk so caller's File.Exists check behaves correctly
        return localMirrorPath;
    }

    private static string ExtractObjectKey(string pathOrUrl)
    {
        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri))
        {
            return uri.AbsolutePath.TrimStart('/');
        }

        if (pathOrUrl.StartsWith("/uploads/"))
        {
            return pathOrUrl["/uploads/".Length..].TrimStart('/');
        }

        if (pathOrUrl.StartsWith("App_Data/uploads/"))
        {
            return pathOrUrl["App_Data/uploads/".Length..].TrimStart('/');
        }

        return pathOrUrl.TrimStart('/');
    }
}
