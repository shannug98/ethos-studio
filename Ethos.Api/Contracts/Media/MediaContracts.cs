namespace Ethos.Api.Contracts.Media;

public class MediaItemResponse
{
    public Guid Id { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public double? DurationSeconds { get; set; }
    public string PublicUrl { get; set; } = string.Empty;
    public string Visibility { get; set; } = "Public";
    public string ApprovalStatus { get; set; } = "Approved";
    public DateTime CreatedAt { get; set; }
}

public class MediaUploadResponse
{
    public Guid Id { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Checksum { get; set; } = string.Empty;
}
