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

    Task<(Stream Stream, string ContentType)?> GetObjectStreamAsync(string objectKey, CancellationToken cancellationToken = default);
}
