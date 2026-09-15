namespace Ethos.Api.Domain.Entities;

public class MediaItem
{
    public Guid Id { get; set; }

    public string ObjectKey { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string MediaType { get; set; } = "image"; // "image" or "video"

    public string Section { get; set; } = "general"; // "homepage", "ethos", "founders", "trainers", "classes", "gallery", "workshops"

    public string MimeType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public double? DurationSeconds { get; set; }

    public string? ThumbnailObjectKey { get; set; }

    public string? PosterObjectKey { get; set; }

    public string PublicUrl { get; set; } = string.Empty;

    public string Visibility { get; set; } = "Public"; // "Public", "Private"

    public string ApprovalStatus { get; set; } = "Approved"; // "Pending", "Approved", "Rejected"

    public Guid? UploadedByUserId { get; set; }

    public string? Checksum { get; set; }

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? UploadedByUser { get; set; }
}
