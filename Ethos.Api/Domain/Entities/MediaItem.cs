namespace Ethos.Api.Domain.Entities;

public class MediaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // ── Content metadata ──────────────────────────────────────────────────────
    public string Title { get; set; } = string.Empty;

    public string Caption { get; set; } = string.Empty;

    public string AltText { get; set; } = string.Empty;

    public string FocalPoint { get; set; } = "center";

    public string Category { get; set; } = "General";

    /// <summary>Default aspect ratio hint for the asset (Portrait/Landscape/Square/Featured).</summary>
    public string LayoutType { get; set; } = "Square";

    public bool IsArchived { get; set; } = false;

    public string? TargetUrl { get; set; }

    /// <summary>Optional FK to a specific workshop (for workshop posters).</summary>
    public Guid? WorkshopId { get; set; }

    // ── Physical file metadata ────────────────────────────────────────────────
    public string ObjectKey { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>"Image" or "Video" — controlled by MediaConstants.MediaTypes.</summary>
    public string MediaType { get; set; } = "Image";

    public string MimeType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public double? DurationSeconds { get; set; }

    public string? ThumbnailObjectKey { get; set; }

    public string? PosterObjectKey { get; set; }

    public string PublicUrl { get; set; } = string.Empty;

    public string? OptimizedUrl { get; set; }

    public string? ThumbnailUrl { get; set; }

    // ── Governance ────────────────────────────────────────────────────────────
    public string Visibility { get; set; } = "Public"; // "Public", "Private"

    public string ApprovalStatus { get; set; } = "Approved"; // "Pending", "Approved", "Rejected"

    public Guid? UploadedByUserId { get; set; }

    public string? Checksum { get; set; }

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ─────────────────────────────────────────────────
    public User? UploadedByUser { get; set; }

    public Workshop? Workshop { get; set; }

    /// <summary>
    /// Per-section placement records. Each entry defines which public section this asset
    /// appears in, with its own display order, publish state, and visibility window.
    /// One file upload → many placements → many website sections.
    /// </summary>
    public ICollection<MediaPlacement> Placements { get; set; } = new List<MediaPlacement>();
}
