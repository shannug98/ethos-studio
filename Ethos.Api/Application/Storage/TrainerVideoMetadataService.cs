using MediaInfo;

namespace Ethos.Api.Application.Storage;

public sealed class TrainerVideoMetadataService
    : ITrainerVideoMetadataService
{
    public Task<TrainerVideoMetadataResult> AnalyzeAsync(
        string physicalFilePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(physicalFilePath))
        {
            return Task.FromResult(
                TrainerVideoMetadataResult.Failed(
                    "Video file path is invalid."));
        }

        if (!File.Exists(physicalFilePath))
        {
            return Task.FromResult(
                TrainerVideoMetadataResult.Failed(
                    "The uploaded video could not be found."));
        }

        try
        {
            var mediaInfo = new MediaInfoWrapper(physicalFilePath);

            if (!mediaInfo.Success)
            {
                return Task.FromResult(
                    TrainerVideoMetadataResult.Failed(
                        "The uploaded video could not be analyzed."));
            }

            var durationMilliseconds = mediaInfo.Duration;

            if (durationMilliseconds <= 0)
            {
                return Task.FromResult(
                    TrainerVideoMetadataResult.Failed(
                        "The uploaded video duration could not be determined."));
            }

            return Task.FromResult(
                new TrainerVideoMetadataResult
                {
                    Success = true,
                    HasVideoStream = mediaInfo.VideoStreams?.Count > 0 || mediaInfo.Duration > 0,
                    Duration = TimeSpan.FromMilliseconds(
                        durationMilliseconds)
                });
        }
        catch (Exception)
        {
            return Task.FromResult(
                TrainerVideoMetadataResult.Failed(
                    "The uploaded video could not be analyzed."));
        }
    }
}
