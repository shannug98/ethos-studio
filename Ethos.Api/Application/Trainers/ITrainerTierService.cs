using Ethos.Api.Contracts.Trainers;

namespace Ethos.Api.Application.Trainers;

public interface ITrainerTierService
{
    Task<TrainerTierResponse?> GetMyTierAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerTierHistoryResponse>> GetMyTierHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerPermissionResponse>> GetMyPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
