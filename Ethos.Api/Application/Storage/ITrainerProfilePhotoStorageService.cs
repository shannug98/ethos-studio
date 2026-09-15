using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Application.Storage;

public interface ITrainerProfilePhotoStorageService
{
    Task<string> SaveAsync(
        Guid userId,
        IFormFile file,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string? storagePath,
        CancellationToken cancellationToken);
}
