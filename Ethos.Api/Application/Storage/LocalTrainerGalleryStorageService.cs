using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Application.Storage;

public sealed class LocalTrainerGalleryStorageService
    : ITrainerGalleryStorageService
{
    private readonly IWebHostEnvironment _environment;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public LocalTrainerGalleryStorageService(
        IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveAsync(
        Guid trainerProfileId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new InvalidOperationException(
                "Please select an image to upload.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                "Gallery images cannot exceed 10 MB.");
        }

        var extension =
            Path.GetExtension(file.FileName);

        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                "Only JPG, PNG and WEBP images are supported.");
        }

        var safeExtension =
            extension.ToLowerInvariant();

        var folder = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "uploads",
            "trainer-gallery",
            trainerProfileId.ToString());

        Directory.CreateDirectory(folder);

        var fileName =
            $"{Guid.NewGuid():N}{safeExtension}";

        var fullPath =
            Path.Combine(folder, fileName);

        await using var stream =
            new FileStream(
                fullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);

        await file.CopyToAsync(
            stream,
            cancellationToken);

        return Path.Combine(
                "trainer-gallery",
                trainerProfileId.ToString(),
                fileName)
            .Replace('\\', '/');
    }

    public Task DeleteAsync(
        string storagePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return Task.CompletedTask;
        }

        var fullPath = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "uploads",
            storagePath.Replace(
                '/',
                Path.DirectorySeparatorChar));

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }
}
