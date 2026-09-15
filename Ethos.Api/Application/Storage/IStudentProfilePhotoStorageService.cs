using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Application.Storage;

public interface IStudentProfilePhotoStorageService
{
    Task<string> SaveAsync(
        Guid userId,
        IFormFile file,
        CancellationToken cancellationToken);

    Task<(Stream Stream, string ContentType)?> GetPhotoStreamAsync(
        string? storagePath,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string? storagePath,
        CancellationToken cancellationToken);
}
