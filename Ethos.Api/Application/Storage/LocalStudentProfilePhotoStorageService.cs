using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Application.Storage;

public class LocalStudentProfilePhotoStorageService : IStudentProfilePhotoStorageService
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

    // Magic number signatures
    // JPEG: FF D8 FF
    // PNG: 89 50 4E 47 0D 0A 1A 0A
    // WEBP: 52 49 46 46 (RIFF) ... 57 45 42 50 (WEBP)
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

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private readonly IWebHostEnvironment _environment;

    public LocalStudentProfilePhotoStorageService(
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

        var uploadsRoot = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "uploads",
            "student-profile-photos",
            userId.ToString());

        Directory.CreateDirectory(uploadsRoot);

        // Server-generated unique filename: never trust user supplied filename
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(uploadsRoot, fileName);

        await using (var targetStream = new FileStream(
            physicalPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            await file.CopyToAsync(targetStream, cancellationToken);
        }

        return Path.Combine(
                "App_Data",
                "uploads",
                "student-profile-photos",
                userId.ToString(),
                fileName)
            .Replace("\\", "/");
    }

    public Task<(Stream Stream, string ContentType)?> GetPhotoStreamAsync(
        string? storagePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return Task.FromResult<(Stream Stream, string ContentType)?>(null);
        }

        var baseRoot = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "uploads",
            "student-profile-photos"));

        var physicalPath = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            storagePath.Replace("/", Path.DirectorySeparatorChar.ToString())));

        // Canonical boundary enforcement to prevent path escape
        if (!physicalPath.StartsWith(baseRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<(Stream Stream, string ContentType)?>(null);
        }

        if (!File.Exists(physicalPath))
        {
            return Task.FromResult<(Stream Stream, string ContentType)?>(null);
        }

        var ext = Path.GetExtension(physicalPath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

        Stream fileStream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<(Stream Stream, string ContentType)?>((fileStream, contentType));
    }

    public Task DeleteAsync(
        string? storagePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return Task.CompletedTask;
        }

        var baseRoot = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "uploads",
            "student-profile-photos"));

        var physicalPath = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            storagePath.Replace("/", Path.DirectorySeparatorChar.ToString())));

        // Strict canonical boundary enforcement against path escape
        if (!physicalPath.StartsWith(baseRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        return Task.CompletedTask;
    }
}
