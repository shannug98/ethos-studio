using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Media;
using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Application.Media;

public interface IMediaService
{
    Task<MediaUploadResponse> UploadMediaAsync(
        IFormFile file,
        string section,
        Guid? adminUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaItemResponse>> GetPublicMediaBySectionAsync(
        string section,
        CancellationToken cancellationToken = default);

    Task<PagedResult<MediaItemResponse>> GetAdminMediaPagedAsync(
        int page,
        int pageSize,
        string? section,
        string? mediaType,
        CancellationToken cancellationToken = default);

    Task DeleteMediaAsync(
        Guid id,
        Guid adminUserId,
        CancellationToken cancellationToken = default);
}
