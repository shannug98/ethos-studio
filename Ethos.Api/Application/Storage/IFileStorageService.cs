namespace Ethos.Api.Application.Storage;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        Guid applicationId,
        CancellationToken cancellationToken);

    Task DeleteFileAsync(
        string filePath,
        CancellationToken cancellationToken);

    bool FileExists(string? filePath);
}