using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Application.Storage;

public sealed class R2TrainerGalleryStorageService : ITrainerGalleryStorageService
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private readonly ICloudflareR2StorageService _r2Storage;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<R2TrainerGalleryStorageService> _logger;

    public R2TrainerGalleryStorageService(
        ICloudflareR2StorageService r2Storage,
        IWebHostEnvironment environment,
        ILogger<R2TrainerGalleryStorageService> logger)
    {
        _r2Storage = r2Storage;
        _environment = environment;
        _logger = logger;
    }

    private static bool HasValidImageSignature(Stream stream, string extension)
    {
        stream.Position = 0;
        var header = new byte[12];
        var bytesRead = stream.Read(header, 0, header.Length);
        stream.Position = 0;

        if (bytesRead < 4) return false;

        switch (extension.ToLowerInvariant())
        {
            case ".jpg":
            case ".jpeg":
                return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

            case ".png":
                return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;

            case ".webp":
                return header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                       header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

            default:
                return false;
        }
    }

    public async Task<string> SaveAsync(
        Guid trainerProfileId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new InvalidOperationException("Please select an image to upload.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("Gallery images cannot exceed 10 MB.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Only JPG, PNG and WEBP images are supported.");
        }

        using (var stream = file.OpenReadStream())
        {
            if (!HasValidImageSignature(stream, extension))
            {
                throw new InvalidOperationException("The file content does not match a supported image format.");
            }
        }

        // Upload to authoritative Cloudflare R2
        using var uploadStream = file.OpenReadStream();
        var uploadResult = await _r2Storage.UploadAsync(
            uploadStream,
            file.FileName,
            file.ContentType,
            $"trainer-gallery/{trainerProfileId}",
            cancellationToken);

        _logger.LogInformation(
            "Trainer gallery image uploaded to authoritative R2: {ObjectKey} (profile {ProfileId})",
            uploadResult.ObjectKey,
            trainerProfileId);

        // Also write ephemeral local cache for immediate local serving
        try
        {
            var cacheDir = Path.Combine(
                _environment.ContentRootPath,
                "App_Data",
                "uploads",
                "cache",
                "trainer-gallery",
                trainerProfileId.ToString());

            Directory.CreateDirectory(cacheDir);
            var cacheFile = Path.Combine(cacheDir, Path.GetFileName(uploadResult.ObjectKey));

            using var cacheStream = file.OpenReadStream();
            using var fileOutput = new FileStream(cacheFile, FileMode.Create, FileAccess.Write, FileShare.None);
            await cacheStream.CopyToAsync(fileOutput, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Writing ephemeral gallery cache for {ObjectKey} skipped/non-critical.", uploadResult.ObjectKey);
        }

        return uploadResult.ObjectKey;
    }

    public async Task DeleteAsync(
        string storagePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return;
        }

        var objectKey = ExtractObjectKey(storagePath);

        // Delete authoritative copy from R2
        try
        {
            await _r2Storage.DeleteAsync(objectKey, cancellationToken);
            _logger.LogInformation("Deleted authoritative trainer gallery image from R2: {ObjectKey}", objectKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete trainer gallery image from R2: {ObjectKey}", objectKey);
            if (!_environment.IsDevelopment())
            {
                throw;
            }
        }

        // Clean up ephemeral local cache
        try
        {
            var fileName = Path.GetFileName(objectKey);
            var cacheBase = Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", "cache", "trainer-gallery");
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
            _logger.LogDebug(ex, "Ephemeral gallery cache cleanup for {Path} encountered non-critical error.", storagePath);
        }
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
