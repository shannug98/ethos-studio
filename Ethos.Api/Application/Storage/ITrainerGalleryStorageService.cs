using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Application.Storage;

public interface ITrainerGalleryStorageService
{
    Task<string> SaveAsync(
        Guid trainerProfileId,
        IFormFile file,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string storagePath,
        CancellationToken cancellationToken);
}
