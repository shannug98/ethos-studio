using Ethos.Api.Application.Trainers.DTOs;
using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Application.Trainers;

public interface ITrainerGalleryService
{
    Task<IReadOnlyList<TrainerGalleryImageDto>>
        GetMineAsync(
            Guid userId,
            CancellationToken cancellationToken);

    Task<TrainerGalleryImageDto>
        UploadAsync(
            Guid userId,
            IFormFile file,
            CancellationToken cancellationToken);

    Task DeleteAsync(
        Guid userId,
        Guid imageId,
        CancellationToken cancellationToken);

    Task<(string PhysicalPath, string ContentType)?>
        GetImageFileAsync(
            Guid userId,
            Guid imageId,
            CancellationToken cancellationToken);
}
