using Ethos.Api.Contracts.Videos;
using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Application.Videos;

public interface IVideoService
{
    Task<IReadOnlyList<StudioVideoResponse>> GetPublicVideosAsync(
        string section,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudioVideoResponse>> GetAdminVideosAsync(
        string? section = null,
        bool? includeInactive = true,
        CancellationToken cancellationToken = default);

    Task<StudioVideoResponse> GetVideoByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<StudioVideoResponse> UploadVideoAsync(
        IFormFile file,
        string section,
        string title,
        string? description,
        int? displayOrder,
        Guid? uploadedByUserId,
        CancellationToken cancellationToken = default);

    Task<StudioVideoResponse> ReplaceVideoFileAsync(
        Guid id,
        IFormFile newFile,
        Guid? updatedByUserId,
        CancellationToken cancellationToken = default);

    Task<StudioVideoResponse> UpdateVideoMetadataAsync(
        Guid id,
        VideoUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<StudioVideoResponse> ToggleActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task ReorderVideosAsync(
        List<VideoOrderItemDto> items,
        CancellationToken cancellationToken = default);

    Task DeleteVideoPermanentlyAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
