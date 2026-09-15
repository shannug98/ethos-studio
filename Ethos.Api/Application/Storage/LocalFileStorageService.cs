namespace Ethos.Api.Application.Storage;

public sealed class LocalFileStorageService : IFileStorageService
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

    private static readonly Dictionary<string, string[]> AllowedMimeTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["pdf"] = new[]
            {
                "application/pdf"
            },

            ["jpg"] = new[]
            {
                "image/jpeg"
            },

            ["jpeg"] = new[]
            {
                "image/jpeg"
            },

            ["png"] = new[]
            {
                "image/png"
            },

            ["webp"] = new[]
            {
                "image/webp"
            }
        };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private readonly IWebHostEnvironment _environment;

    public LocalFileStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveFileAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        if (fileStream is null)
            throw new ArgumentNullException(nameof(fileStream));

        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException(
                "File name is required.",
                nameof(originalFileName));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException(
                "Content type is required.",
                nameof(contentType));

        if (fileStream.CanSeek && fileStream.Length > MaxFileSizeBytes)
            throw new InvalidOperationException(
                "File size must not exceed 10 MB.");

        var extension = Path.GetExtension(originalFileName);

        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                "Only PDF, JPG, JPEG, PNG, and WEBP files are allowed.");
        }

        var normalizedExtension =
            extension.TrimStart('.').ToLowerInvariant();

        if (!AllowedMimeTypes.TryGetValue(
                normalizedExtension,
                out var allowedMimeTypes) ||
            !allowedMimeTypes.Contains(
                contentType,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The file content type does not match the file extension.");
        }

        var uploadsRoot = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "uploads",
            "documents");

        var applicationDirectory = Path.Combine(
            uploadsRoot,
            applicationId.ToString("N"));

        Directory.CreateDirectory(applicationDirectory);

        var generatedFileName =
            $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

        var physicalPath = Path.Combine(
            applicationDirectory,
            generatedFileName);

        await using (var outputStream = new FileStream(
            physicalPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true))
        {
            await fileStream.CopyToAsync(
                outputStream,
                cancellationToken);
        }

        var relativePath = Path.Combine(
            "uploads",
            "documents",
            applicationId.ToString("N"),
            generatedFileName);

        return relativePath.Replace(
            Path.DirectorySeparatorChar,
            '/');
    }

    public Task DeleteFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Task.CompletedTask;

        var appDataRoot = Path.GetFullPath(
            Path.Combine(
                _environment.ContentRootPath,
                "App_Data"));

        var fullPath = Path.GetFullPath(
            Path.Combine(
                appDataRoot,
                filePath
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar)));

        // Prevent path traversal outside the document storage root.
        var uploadsRoot = Path.Combine(appDataRoot, "uploads", "documents");
        if (!fullPath.StartsWith(
                uploadsRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Invalid file path.");
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public bool FileExists(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        try
        {
            var appDataRoot = Path.GetFullPath(
                Path.Combine(
                    _environment.ContentRootPath,
                    "App_Data"));

            var fullPath = Path.GetFullPath(
                Path.Combine(
                    appDataRoot,
                    filePath
                        .TrimStart('/')
                        .Replace('/', Path.DirectorySeparatorChar)
                        .Replace('\\', Path.DirectorySeparatorChar)));

            return File.Exists(fullPath);
        }
        catch
        {
            return false;
        }
    }
}