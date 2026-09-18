using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Application.Storage;

public class R2TrainerProfilePhotoStorageService : ITrainerProfilePhotoStorageService
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
    private readonly ILogger<R2TrainerProfilePhotoStorageService> _logger;

    public R2TrainerProfilePhotoStorageService(
        ICloudflareR2StorageService r2Storage,
        ILogger<R2TrainerProfilePhotoStorageService> logger)
    {
        _r2Storage = r2Storage;
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
        Guid userId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Please select a profile photo.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException("Profile photo cannot exceed 10 MB.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException("Profile photo must be JPG, PNG or WebP.");
        }

        using (var stream = file.OpenReadStream())
        {
            if (!HasValidImageSignature(stream, extension))
            {
                throw new ArgumentException("The file content does not match a supported image format.");
            }
        }

        using var uploadStream = file.OpenReadStream();
        var result = await _r2Storage.UploadAsync(
            uploadStream,
            file.FileName,
            file.ContentType,
            $"profile-photos/trainers/{userId}",
            cancellationToken);

        _logger.LogInformation("Trainer profile photo uploaded to R2: {ObjectKey}", result.ObjectKey);
        return result.PublicUrl;
    }

    public async Task DeleteAsync(
        string? storagePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return;
        }

        try
        {
            var objectKey = ExtractObjectKey(storagePath);
            await _r2Storage.DeleteAsync(objectKey, cancellationToken);
            _logger.LogInformation("Deleted trainer profile photo: {ObjectKey}", objectKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete trainer profile photo: {Path}", storagePath);
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
