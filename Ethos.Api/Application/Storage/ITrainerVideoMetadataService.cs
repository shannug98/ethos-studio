namespace Ethos.Api.Application.Storage;

public interface ITrainerVideoMetadataService
{
    Task<TrainerVideoMetadataResult> AnalyzeAsync(
        string physicalFilePath,
        CancellationToken cancellationToken);
}

public sealed class TrainerVideoMetadataResult
{
    public bool Success { get; init; }

    public bool HasVideoStream { get; init; }

    public TimeSpan Duration { get; init; }

    public string? ErrorMessage { get; init; }

    public static TrainerVideoMetadataResult Failed(
        string errorMessage)
    {
        return new TrainerVideoMetadataResult
        {
            Success = false,
            HasVideoStream = false,
            ErrorMessage = errorMessage
        };
    }
}
