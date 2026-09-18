using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Ethos.Api.Application.Storage;
using Microsoft.Extensions.Options;

namespace Ethos.Api.Infrastructure.Storage;

public class CloudflareR2StorageService : ICloudflareR2StorageService
{
    private readonly CloudflareR2Settings _settings;
    private readonly ILogger<CloudflareR2StorageService> _logger;
    private readonly IWebHostEnvironment _environment;

    public CloudflareR2StorageService(
        IOptions<CloudflareR2Settings> settings,
        ILogger<CloudflareR2StorageService> logger,
        IWebHostEnvironment environment)
    {
        _settings = settings.Value;
        _logger = logger;
        _environment = environment;
    }

    private bool IsR2Configured =>
        !string.IsNullOrWhiteSpace(_settings.AccountId) &&
        !string.IsNullOrWhiteSpace(_settings.AccessKeyId) &&
        !string.IsNullOrWhiteSpace(_settings.SecretAccessKey);

    private IAmazonS3 CreateS3Client()
    {
        var credentials = new BasicAWSCredentials(_settings.AccessKeyId, _settings.SecretAccessKey);
        var config = new AmazonS3Config
        {
            ServiceURL = _settings.ServiceUrl,
            ForcePathStyle = true, // Required for Cloudflare R2
            Timeout = TimeSpan.FromSeconds(60)
        };

        return new AmazonS3Client(credentials, config);
    }

    public async Task<R2UploadResult> UploadAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        string section,
        CancellationToken cancellationToken = default)
    {
        var sanitizedSection = string.IsNullOrWhiteSpace(section) ? "general" : section.Trim().ToLowerInvariant();
        var mediaTypeFolder = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? "videos" : "images";
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? ".mp4" : ".webp";
        }

        var now = DateTime.UtcNow;
        var uniqueId = Guid.NewGuid().ToString("N");
        var objectKey = $"{sanitizedSection}/{mediaTypeFolder}/{now:yyyy}/{now:MM}/{uniqueId}{extension}";

        // Calculate SHA-256 Checksum while reading stream
        string checksum;
        long streamLength = 0;
        using (var sha256 = SHA256.Create())
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
                var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
                checksum = Convert.ToHexString(hashBytes).ToLowerInvariant();
                streamLength = stream.Length;
                stream.Position = 0;
            }
            else
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, cancellationToken);
                ms.Position = 0;
                var hashBytes = await sha256.ComputeHashAsync(ms, cancellationToken);
                checksum = Convert.ToHexString(hashBytes).ToLowerInvariant();
                streamLength = ms.Length;
                ms.Position = 0;
                stream = ms;
            }
        }

        string publicUrl;

        if (!IsR2Configured)
        {
            _logger.LogError("Cloudflare R2 is not configured. Media/photo storage operation cannot proceed without valid credentials.");
            throw new InvalidOperationException("Cloudflare R2 credentials (AccountId, AccessKeyId, SecretAccessKey) are required for persistent media and profile photo storage. Please configure CloudflareR2 in application settings.");
        }

        using (var client = CreateS3Client())
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = objectKey,
                InputStream = stream,
                ContentType = contentType,
                DisablePayloadSigning = true
            };

            putRequest.Metadata.Add("original-filename", originalFileName);
            putRequest.Metadata.Add("uploaded-at", now.ToString("O"));

            await client.PutObjectAsync(putRequest, cancellationToken);

            publicUrl = GetPublicUrl(objectKey);
            _logger.LogInformation("File successfully uploaded to Cloudflare R2: {ObjectKey}", objectKey);
        }

        return new R2UploadResult
        {
            ObjectKey = objectKey,
            PublicUrl = publicUrl,
            ContentType = contentType,
            FileSizeBytes = streamLength,
            Checksum = checksum
        };
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var localFilePath = Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", objectKey.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(localFilePath))
        {
            File.Delete(localFilePath);
            _logger.LogInformation("Deleted local legacy file: {LocalPath}", localFilePath);
            return;
        }

        if (!IsR2Configured)
        {
            _logger.LogWarning("Cloudflare R2 is not configured. Cannot delete remote object: {ObjectKey}", objectKey);
            return;
        }

        using var client = CreateS3Client();
        var deleteRequest = new DeleteObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = objectKey
        };

        await client.DeleteObjectAsync(deleteRequest, cancellationToken);
        _logger.LogInformation("Deleted object from Cloudflare R2: {ObjectKey}", objectKey);
    }

    public string GetPublicUrl(string objectKey)
    {
        if (!string.IsNullOrWhiteSpace(_settings.PublicDomain))
        {
            var domain = _settings.PublicDomain.TrimEnd('/');
            return $"{domain}/{objectKey.TrimStart('/')}";
        }

        return $"https://{_settings.BucketName}.r2.cloudflarestorage.com/{objectKey.TrimStart('/')}";
    }

    public string GeneratePreSignedGetUrl(string objectKey, TimeSpan duration)
    {
        if (!IsR2Configured)
        {
            return $"/uploads/{objectKey.TrimStart('/')}";
        }

        using var client = CreateS3Client();
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,
            Key = objectKey,
            Expires = DateTime.UtcNow.Add(duration),
            Verb = HttpVerb.GET
        };

        return client.GetPreSignedURL(request);
    }

    public async Task<(Stream Stream, string ContentType)?> GetObjectStreamAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (IsR2Configured)
        {
            try
            {
                var client = CreateS3Client();
                var response = await client.GetObjectAsync(_settings.BucketName, objectKey, cancellationToken);
                return (response.ResponseStream, response.Headers.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch object {ObjectKey} from R2 directly", objectKey);
            }
        }

        var localFilePath = Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", objectKey.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(localFilePath))
        {
            var stream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var ext = Path.GetExtension(localFilePath).ToLowerInvariant();
            var contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                _ => "application/octet-stream"
            };
            return (stream, contentType);
        }

        return null;
    }
}
