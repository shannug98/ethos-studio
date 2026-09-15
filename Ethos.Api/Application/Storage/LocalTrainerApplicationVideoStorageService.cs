namespace Ethos.Api.Application.Storage;

public sealed class LocalTrainerApplicationVideoStorageService
    : ITrainerApplicationVideoStorageService
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

    private const long MaxFileSizeBytes =
        100 * 1024 * 1024;

    private readonly IWebHostEnvironment _environment;

    public LocalTrainerApplicationVideoStorageService(
        IWebHostEnvironment environment)
    {
        _environment = environment;
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
            throw new ArgumentException(
                "Video file name is required.",
                nameof(originalFileName));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException(
                "Video content type is required.",
                nameof(contentType));

        if (fileStream.CanSeek &&
            fileStream.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                "Video file size cannot exceed 100 MB.");
        }

        var extension =
            Path.GetExtension(originalFileName);

        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                "Only MP4, MOV, WEBM, and M4V video files are allowed.");
        }

        if (!AllowedMimeTypes.Contains(contentType))
        {
            throw new InvalidOperationException(
                "The selected video format is not supported.");
        }

        var uploadsRoot = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "uploads",
            "trainer-videos");

        var applicationDirectory = Path.Combine(
            uploadsRoot,
            applicationId.ToString("N"));

        Directory.CreateDirectory(applicationDirectory);

        var generatedFileName =
            $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

        var physicalPath = Path.Combine(
            applicationDirectory,
            generatedFileName);

        await using (
            var outputStream = new FileStream(
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
            "trainer-videos",
            applicationId.ToString("N"),
            generatedFileName);

        return relativePath.Replace(
            Path.DirectorySeparatorChar,
            '/');
    }

    public Task DeleteVideoAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Task.CompletedTask;

        var appDataRoot = Path.GetFullPath(
            Path.Combine(
                _environment.ContentRootPath,
                "App_Data"));

        var videosRoot = Path.GetFullPath(
            Path.Combine(
                appDataRoot,
                "uploads",
                "trainer-videos"));

        var fullPath = Path.GetFullPath(
            Path.Combine(
                appDataRoot,
                filePath
                    .Replace(
                        '/',
                        Path.DirectorySeparatorChar)
                    .Replace(
                        '\\',
                        Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(
                videosRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Invalid video file path.");
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public string GetPhysicalPath(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new ArgumentException(
                "Storage path cannot be empty.",
                nameof(storagePath));
        }

        var appDataRoot = Path.GetFullPath(
            Path.Combine(
                _environment.ContentRootPath,
                "App_Data"));

        var videosRoot = Path.GetFullPath(
            Path.Combine(
                appDataRoot,
                "uploads",
                "trainer-videos"));

        var fullPath = Path.GetFullPath(
            Path.Combine(
                appDataRoot,
                storagePath
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(
                videosRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Invalid video storage path.");
        }

        return fullPath;
    }
}
