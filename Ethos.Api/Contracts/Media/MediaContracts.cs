namespace Ethos.Api.Contracts.Media;

// ── Placement DTOs ────────────────────────────────────────────────────────────

/// <summary>Request body for adding or updating a placement.</summary>
public class AdminPlacementRequest
{
    public string Section { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;
    public bool IsPublished { get; set; } = true;
    public bool IsFeatured { get; set; } = false;
    public DateTime? VisibleFromUtc { get; set; }
    public DateTime? VisibleUntilUtc { get; set; }
}

/// <summary>Placement details returned in admin responses.</summary>
public class AdminPlacementResponse
{
    public Guid Id { get; set; }
    public string Section { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime? VisibleFromUtc { get; set; }
    public DateTime? VisibleUntilUtc { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Public response ───────────────────────────────────────────────────────────

public class PublicMediaResponse
{
    public Guid Id { get; set; }
    public Guid PlacementId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
    public string AltText { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string LayoutType { get; set; } = "Square";
    public int DisplayOrder { get; set; }
    public bool IsFeatured { get; set; }
    public string PublicUrl { get; set; } = string.Empty;
    public string? OptimizedUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string MediaType { get; set; } = "Image";
    public string Section { get; set; } = string.Empty;
    public string FocalPoint { get; set; } = "center";
    public string? TargetUrl { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public double? DurationSeconds { get; set; }
}

// ── Admin response ────────────────────────────────────────────────────────────

public class MediaItemResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
    public string AltText { get; set; } = string.Empty;
    public string FocalPoint { get; set; } = "center";
    public string Category { get; set; } = "General";
    public string LayoutType { get; set; } = "Square";
    public bool IsArchived { get; set; }
    public string? TargetUrl { get; set; }
    public Guid? WorkshopId { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public double? DurationSeconds { get; set; }
    public string PublicUrl { get; set; } = string.Empty;
    public string? OptimizedUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string Visibility { get; set; } = "Public";
    public string ApprovalStatus { get; set; } = "Approved";
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<AdminPlacementResponse> Placements { get; set; } = Array.Empty<AdminPlacementResponse>();
}

// ── Upload response ───────────────────────────────────────────────────────────

public class MediaUploadResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string LayoutType { get; set; } = "Square";
    public long FileSizeBytes { get; set; }
    public string Checksum { get; set; } = string.Empty;
    public IReadOnlyList<AdminPlacementResponse> Placements { get; set; } = Array.Empty<AdminPlacementResponse>();
}

// ── Update / reorder requests ─────────────────────────────────────────────────

public class AdminUpdateMediaRequest
{
    public string? Title { get; set; }
    public string? Caption { get; set; }
    public string? AltText { get; set; }
    public string? FocalPoint { get; set; }
    public string? Category { get; set; }
    public string? LayoutType { get; set; }
    public string? TargetUrl { get; set; }
    public Guid? WorkshopId { get; set; }
}

public class AdminReorderMediaItem
{
    public Guid PlacementId { get; set; }
    public int DisplayOrder { get; set; }
}

public class AdminReorderMediaRequest
{
    public string Section { get; set; } = string.Empty;
    public List<AdminReorderMediaItem> Items { get; set; } = new();
}

public class TogglePublishRequest
{
    public bool IsPublished { get; set; }
}

// ── Large Gallery Video Presigned Upload DTOs ─────────────────────────────────

public class PresignGalleryUploadRequest
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "video/mp4";
    public long FileSizeBytes { get; set; }
}

public class PresignGalleryUploadResponse
{
    public string UploadUrl { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string UploadToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}

public class ConfirmGalleryUploadRequest
{
    public string UploadToken { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public string? AltText { get; set; }
    public string? Category { get; set; }
    public int? DisplayOrder { get; set; }
    public bool? IsPublished { get; set; }
    public bool? IsFeatured { get; set; }
}

// ── React Assets Migration DTOs ───────────────────────────────────────────────

public class MigrateReactAssetsRequest
{
    public string? WebRootPath { get; set; }
}

public class ReactAssetMigrationItemResult
{
    public string OriginalReactAsset { get; set; } = string.Empty;
    public string CurrentUsage { get; set; } = string.Empty;
    public string R2Key { get; set; } = string.Empty;
    public string MediaSection { get; set; } = string.Empty;
    public string DbRecord { get; set; } = string.Empty;
    public bool FrontendUpdated { get; set; }
    public bool SafeToRemoveLocalCopy { get; set; }
    public string Status { get; set; } = string.Empty; // "Migrated", "AlreadyExists", "UpdatedWithPlacements", "Error"
    public string? Error { get; set; }
}

public class ReactAssetMigrationResponse
{
    public int TotalFound { get; set; }
    public int MigratedCount { get; set; }
    public int AlreadyExistedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<ReactAssetMigrationItemResult> Items { get; set; } = new();
}

