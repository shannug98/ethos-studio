using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Application.Storage;

public class LocalTrainerProfilePhotoStorageService
    : ITrainerProfilePhotoStorageService
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private readonly IWebHostEnvironment _environment;

    public LocalTrainerProfilePhotoStorageService(
        IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveAsync(
        Guid userId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            throw new InvalidOperationException(
                "Please select a profile photo.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                "Profile photo cannot exceed 10 MB.");
        }

        var extension =
            Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                "Profile photo must be JPG, PNG or WebP.");
        }

        var uploadsRoot = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "uploads",
            "trainer-profile-photos",
            userId.ToString());

        Directory.CreateDirectory(uploadsRoot);

        var fileName =
            $"{Guid.NewGuid():N}{extension}";

        var physicalPath =
            Path.Combine(uploadsRoot, fileName);

        await using var stream =
            new FileStream(
                physicalPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);

        await file.CopyToAsync(
            stream,
            cancellationToken);

        return Path.Combine(
                "App_Data",
                "uploads",
                "trainer-profile-photos",
                userId.ToString(),
                fileName)
            .Replace("\\", "/");
    }

    public Task DeleteAsync(
        string? storagePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return Task.CompletedTask;
        }

        var physicalPath = Path.Combine(
            _environment.ContentRootPath,
            storagePath.Replace(
                "/",
                Path.DirectorySeparatorChar.ToString()));

        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        return Task.CompletedTask;
    }
}
