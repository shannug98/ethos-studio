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

        if (_environment.IsDevelopment())
        {
            // Ensure local mirror in App_Data/uploads exists in development for offline fallback
            var localMirrorPath = Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", objectKey.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(localMirrorPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using (var localFs = new FileStream(localMirrorPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                if (stream.CanSeek) stream.Position = 0;
                await stream.CopyToAsync(localFs, cancellationToken);
                if (stream.CanSeek) stream.Position = 0;
            }
            // In development, store in local mirror and optionally mirror to R2 if configured
            if (IsR2Configured && !_settings.AccountId.StartsWith("dev_test_", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var client = CreateS3Client();
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
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Development mode: R2 upload skipped/failed, retaining local mirror for {ObjectKey}.", objectKey);
                }
            }

            publicUrl = $"/uploads/{objectKey.TrimStart('/')}";
            _logger.LogInformation("Development mode: Stored file in local uploads: {ObjectKey}", objectKey);
        }
        else
        {
            // In production, R2 configuration is mandatory - fail fast with explicit exception
            if (!IsR2Configured)
            {
                throw new InvalidOperationException("Cloudflare R2 storage credentials are required in production.");
            }

            try
            {
                using var client = CreateS3Client();
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload to remote Cloudflare R2 for {ObjectKey}.", objectKey);
                throw new InvalidOperationException($"Cloudflare R2 upload failed in production for {objectKey}: {ex.Message}", ex);
            }
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
            try
            {
                File.Delete(localFilePath);
                _logger.LogInformation("Deleted local mirror file: {LocalPath}", localFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete local mirror file: {LocalPath}", localFilePath);
            }
        }

        if (!IsR2Configured || _settings.AccountId.StartsWith("dev_test_", StringComparison.OrdinalIgnoreCase))
        {
            if (!_environment.IsDevelopment())
            {
                throw new InvalidOperationException("Cloudflare R2 storage credentials are required in production.");
            }
            _logger.LogInformation("Cloudflare R2 is placeholder or not configured. Local mirror deleted: {ObjectKey}", objectKey);
            return;
        }

        try
        {
            using var client = CreateS3Client();
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = objectKey
            };

            await client.DeleteObjectAsync(deleteRequest, cancellationToken);
            _logger.LogInformation("Deleted object from Cloudflare R2: {ObjectKey}", objectKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete remote Cloudflare R2 object: {ObjectKey}", objectKey);
        }
    }

    public string GetPublicUrl(string objectKey)
    {
        if (_environment.IsDevelopment())
        {
            return $"/uploads/{objectKey.TrimStart('/')}";
        }

        if (!string.IsNullOrWhiteSpace(_settings.PublicDomain))
        {
            var domain = _settings.PublicDomain.TrimEnd('/');
            return $"{domain}/{objectKey.TrimStart('/')}";
        }

        return $"https://{_settings.BucketName}.r2.cloudflarestorage.com/{objectKey.TrimStart('/')}";
    }

    public string GeneratePreSignedGetUrl(string objectKey, TimeSpan duration)
    {
        if (_environment.IsDevelopment() || !IsR2Configured)
        {
            if (!_environment.IsDevelopment() && !IsR2Configured)
            {
                throw new InvalidOperationException("Cloudflare R2 storage credentials are required in production.");
            }
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

    public string GeneratePreSignedPutUrl(string objectKey, string contentType, TimeSpan duration)
    {
        if (_environment.IsDevelopment() || !IsR2Configured)
        {
            if (!_environment.IsDevelopment() && !IsR2Configured)
            {
                throw new InvalidOperationException("Cloudflare R2 storage credentials are required in production.");
            }
            return $"/uploads/{objectKey.TrimStart('/')}";
        }

        using var client = CreateS3Client();
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,
            Key = objectKey,
            Expires = DateTime.UtcNow.Add(duration),
            Verb = HttpVerb.PUT,
            ContentType = contentType
        };

        return client.GetPreSignedURL(request);
    }

    public async Task<R2ObjectMetadata?> GetObjectMetadataAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (!IsR2Configured)
        {
            var localFilePath = Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", objectKey.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(localFilePath))
            {
                var fi = new FileInfo(localFilePath);
                var ext = fi.Extension.ToLowerInvariant();
                var contentType = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    ".mp4" => "video/mp4",
                    ".webm" => "video/webm",
                    _ => "application/octet-stream"
                };
                return new R2ObjectMetadata
                {
                    ObjectKey = objectKey,
                    ContentLength = fi.Length,
                    ContentType = contentType,
                    LastModified = fi.LastWriteTimeUtc
                };
            }
            return null;
        }

        try
        {
            using var client = CreateS3Client();
            var response = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _settings.BucketName,
                Key = objectKey
            }, cancellationToken);

            return new R2ObjectMetadata
            {
                ObjectKey = objectKey,
                ContentLength = response.ContentLength,
                ContentType = response.Headers.ContentType,
                LastModified = response.LastModified
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inspect R2 object metadata for {ObjectKey}", objectKey);
            return null;
        }
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

    public async Task<R2RangeResult?> GetObjectRangeStreamAsync(
        string objectKey,
        long? fromByte,
        long? toByte,
        CancellationToken cancellationToken = default)
    {
        var localFilePath = Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", objectKey.Replace('/', Path.DirectorySeparatorChar));
        bool hasLocal = File.Exists(localFilePath);
        long totalLength = 0;
        string contentType = "application/octet-stream";

        if (hasLocal)
        {
            var fi = new FileInfo(localFilePath);
            totalLength = fi.Length;
            var ext = fi.Extension.ToLowerInvariant();
            contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                _ => "application/octet-stream"
            };
        }
        else if (IsR2Configured)
        {
            var meta = await GetObjectMetadataAsync(objectKey, cancellationToken);
            if (meta == null) return null;
            totalLength = meta.ContentLength;
            contentType = meta.ContentType;
        }
        else
        {
            return null;
        }

        if (totalLength <= 0) return null;

        // Full file request (no Range requested)
        if (!fromByte.HasValue && !toByte.HasValue)
        {
            if (hasLocal)
            {
                var fs = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                return new R2RangeResult
                {
                    Stream = fs,
                    ContentType = contentType,
                    ContentLength = totalLength,
                    TotalLength = totalLength,
                    FromByte = 0,
                    ToByte = totalLength - 1,
                    IsPartial = false
                };
            }

            var client = CreateS3Client();
            var response = await client.GetObjectAsync(_settings.BucketName, objectKey, cancellationToken);
            return new R2RangeResult
            {
                Stream = response.ResponseStream,
                ContentType = response.Headers.ContentType,
                ContentLength = response.ContentLength,
                TotalLength = totalLength,
                FromByte = 0,
                ToByte = totalLength - 1,
                IsPartial = false
            };
        }

        // Partial Range Request
        long actualFrom;
        long actualTo;

        if (fromByte.HasValue && toByte.HasValue)
        {
            actualFrom = fromByte.Value;
            actualTo = Math.Min(toByte.Value, totalLength - 1);
        }
        else if (fromByte.HasValue && !toByte.HasValue)
        {
            actualFrom = fromByte.Value;
            actualTo = totalLength - 1;
        }
        else if (!fromByte.HasValue && toByte.HasValue)
        {
            actualFrom = Math.Max(0, totalLength - toByte.Value);
            actualTo = totalLength - 1;
        }
        else
        {
            actualFrom = 0;
            actualTo = totalLength - 1;
        }

        if (actualFrom < 0 || actualFrom > actualTo || actualFrom >= totalLength)
        {
            return null;
        }

        long sliceLength = actualTo - actualFrom + 1;

        if (hasLocal)
        {
            var fs = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            fs.Seek(actualFrom, SeekOrigin.Begin);
            return new R2RangeResult
            {
                Stream = new SlicedStream(fs, sliceLength),
                ContentType = contentType,
                ContentLength = sliceLength,
                TotalLength = totalLength,
                FromByte = actualFrom,
                ToByte = actualTo,
                IsPartial = true
            };
        }

        // Remote Cloudflare R2 / S3 Range Forwarding — never buffers the full object to disk
        var s3Client = CreateS3Client();
        var getReq = new GetObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = objectKey,
            ByteRange = new ByteRange(actualFrom, actualTo)
        };
        var s3Resp = await s3Client.GetObjectAsync(getReq, cancellationToken);
        return new R2RangeResult
        {
            Stream = s3Resp.ResponseStream,
            ContentType = contentType,
            ContentLength = sliceLength,
            TotalLength = totalLength,
            FromByte = actualFrom,
            ToByte = actualTo,
            IsPartial = true
        };
    }
}

internal sealed class SlicedStream : Stream
{
    private readonly Stream _inner;
    private long _remaining;

    public SlicedStream(Stream inner, long length)
    {
        _inner = inner;
        _remaining = length;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _remaining;
    public override long Position
    {
        get => 0;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_remaining <= 0) return 0;
        int toRead = (int)Math.Min(count, _remaining);
        int read = _inner.Read(buffer, offset, toRead);
        _remaining -= read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_remaining <= 0) return 0;
        int toRead = (int)Math.Min(buffer.Length, _remaining);
        int read = await _inner.ReadAsync(buffer.Slice(0, toRead), cancellationToken);
        _remaining -= read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }
}
