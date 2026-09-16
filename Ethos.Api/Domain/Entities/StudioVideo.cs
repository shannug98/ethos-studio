namespace Ethos.Api.Domain.Entities;

public class StudioVideo
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Video Section: "ShortVideos" (10-20s dance reels) or "Gallery" (1-min showcases)
    /// </summary>
    public string Section { get; set; } = "ShortVideos";

    /// <summary>
    /// Cloudflare R2 object key identifier (e.g. ethos-videos/short-videos/2026/09/clip-001.mp4)
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// Direct public CDN or presigned streaming URL
    /// </summary>
    public string PublicUrl { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    public int DisplayOrder { get; set; } = 0;

    /// <summary>
    /// If false, video is hidden from public website but preserved in R2 & DB
    /// </summary>
    public bool IsActive { get; set; } = true;

    public double? DurationSeconds { get; set; }

    public long FileSizeBytes { get; set; }

    public string MimeType { get; set; } = "video/mp4";

    public Guid? UploadedByUserId { get; set; }

    public User? UploadedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
