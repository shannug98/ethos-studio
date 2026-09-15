namespace Ethos.Api.Application.Storage;

public interface ITrainerApplicationVideoStorageService
{
    Task<string> SaveVideoAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        Guid applicationId,
        CancellationToken cancellationToken);

    Task DeleteVideoAsync(
        string filePath,
        CancellationToken cancellationToken);

    string GetPhysicalPath(string storagePath);
}
