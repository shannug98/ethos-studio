namespace Ethos.Api.Domain.Entities;

/// <summary>
/// Represents a single website section placement for a MediaItem.
/// One MediaItem can have multiple MediaPlacement rows — one per section it should appear in.
/// The MediaItem holds the physical file; MediaPlacement holds display metadata.
/// </summary>
public class MediaPlacement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK → media_items. Required.</summary>
    public Guid MediaItemId { get; set; }

    /// <summary>
    /// Target section controlled by MediaConstants.Sections.
    /// e.g. "HomepageScrolling", "HomepageReels", "GalleryImages".
    /// </summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>Sort order within this section. Lower = appears first.</summary>
    public int DisplayOrder { get; set; } = 0;

    /// <summary>
    /// When false the asset is hidden from the public website in this section
    /// but the MediaItem still exists and may be published in other sections.
    /// </summary>
    public bool IsPublished { get; set; } = true;

    /// <summary>Optional featured flag for UI highlights within the section.</summary>
    public bool IsFeatured { get; set; } = false;

    /// <summary>UTC timestamp when this placement becomes visible. Null = no restriction.</summary>
    public DateTime? VisibleFromUtc { get; set; }

    /// <summary>UTC timestamp when this placement expires. Null = no expiry.</summary>
    public DateTime? VisibleUntilUtc { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public MediaItem MediaItem { get; set; } = null!;
}
