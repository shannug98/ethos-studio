using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Media;
using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Application.Media;

public interface IMediaService
{
    /// <summary>
    /// Upload a new media file. Creates one MediaItem in R2 + DB,
    /// then creates a MediaPlacement for each section in <paramref name="sections"/>.
    /// If sections is empty/null, creates a single placement in "Draft".
    /// </summary>
    Task<MediaUploadResponse> UploadMediaAsync(
        IFormFile file,
        IEnumerable<string> sections,
        string? mediaType,
        string? title,
        string? caption,
        string? altText,
        string? focalPoint,
        string? category,
        string? layoutType,
        int? displayOrder,
        bool? isPublished,
        bool? isFeatured,
        double? clientDurationSeconds,
        Guid? adminUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PublicMediaResponse>> GetPublicMediaAsync(
        string? section,
        string? category,
        string? mediaType,
        CancellationToken cancellationToken = default);

    Task<PagedResult<MediaItemResponse>> GetAdminMediaPagedAsync(
        int page,
        int pageSize,
        string? section,
        string? mediaType,
        string? category,
        bool? isPublished,
        bool? isArchived,
        CancellationToken cancellationToken = default);

    Task<MediaItemResponse> UpdateMediaAsync(
        Guid id,
        AdminUpdateMediaRequest request,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    // ── Placement-level operations ────────────────────────────────────────────

    Task<AdminPlacementResponse> AddPlacementAsync(
        Guid mediaItemId,
        AdminPlacementRequest request,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    Task<AdminPlacementResponse> UpdatePlacementAsync(
        Guid placementId,
        AdminPlacementRequest request,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    Task RemovePlacementAsync(
        Guid placementId,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    Task<AdminPlacementResponse> TogglePlacementPublishAsync(
        Guid placementId,
        bool isPublished,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    // ── Item-level operations ─────────────────────────────────────────────────

    Task<MediaItemResponse> ArchiveMediaAsync(
        Guid id,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    Task<MediaItemResponse> RestoreMediaAsync(
        Guid id,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    Task ReorderMediaAsync(
        AdminReorderMediaRequest request,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    Task DeleteMediaAsync(
        Guid id,
        bool permanent,
        Guid adminUserId,
        CancellationToken cancellationToken = default);
}

