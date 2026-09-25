namespace Ethos.Api.Application.Storage;

public class R2UploadResult
{
    public string ObjectKey { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Checksum { get; set; } = string.Empty;
}

public interface ICloudflareR2StorageService
{
    Task<R2UploadResult> UploadAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        string section,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

    string GetPublicUrl(string objectKey);

    string GeneratePreSignedGetUrl(string objectKey, TimeSpan duration);

    string GeneratePreSignedPutUrl(string objectKey, string contentType, TimeSpan duration);

    Task<R2ObjectMetadata?> GetObjectMetadataAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<(Stream Stream, string ContentType)?> GetObjectStreamAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<R2RangeResult?> GetObjectRangeStreamAsync(
        string objectKey,
        long? fromByte,
        long? toByte,
        CancellationToken cancellationToken = default);
}

public class R2ObjectMetadata
{
    public string ObjectKey { get; set; } = string.Empty;
    public long ContentLength { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime? LastModified { get; set; }
}

public class R2RangeResult
{
    public Stream Stream { get; set; } = Stream.Null;
    public string ContentType { get; set; } = "application/octet-stream";
    public long ContentLength { get; set; }
    public long TotalLength { get; set; }
    public long FromByte { get; set; }
    public long ToByte { get; set; }
    public bool IsPartial { get; set; }
}
