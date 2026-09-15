using Ethos.Api.Contracts.Trainers;

namespace Ethos.Api.Application.Trainers;

public interface ITrainerUpgradeService
{
    Task<IReadOnlyList<TrainerUpgradeRequestResponse>> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<TrainerUpgradeRequestResponse> CreateAsync(
        Guid userId,
        CreateTrainerUpgradeRequest request,
        CancellationToken cancellationToken);

    Task CancelAsync(
        Guid userId,
        Guid requestId,
        CancellationToken cancellationToken);
}
