using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Contracts.Videos;

public class StudioVideoResponse
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Section { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public string PublicUrl { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public double? DurationSeconds { get; set; }

    public long FileSizeBytes { get; set; }

    public string MimeType { get; set; } = "video/mp4";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class VideoUploadRequest
{
    public IFormFile File { get; set; } = null!;

    public string Section { get; set; } = "ShortVideos"; // "ShortVideos" or "Gallery"

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? DisplayOrder { get; set; }
}

public class VideoUpdateRequest
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? DisplayOrder { get; set; }

    public bool? IsActive { get; set; }
}

public class VideoReplaceRequest
{
    public IFormFile File { get; set; } = null!;
}

public class VideoOrderItemDto
{
    public Guid Id { get; set; }

    public int DisplayOrder { get; set; }
}

public class VideoReorderRequest
{
    public List<VideoOrderItemDto> Items { get; set; } = new();
}
