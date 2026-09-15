using Ethos.Api.Contracts.Trainers;

namespace Ethos.Api.Application.Trainers;

public interface ITrainerAvailabilityService
{
    Task<IReadOnlyList<TrainerAvailabilityResponse>> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerAvailabilityResponse>> ReplaceMineAsync(
        Guid userId,
        List<TrainerAvailabilityRequest> requests,
        CancellationToken cancellationToken);
}
