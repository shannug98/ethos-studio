using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;

namespace Ethos.Api.Application.Admin;

public interface IAdminTrainerTierService
{
    Task<IReadOnlyList<TrainerTierResponse>> GetAllTiersAsync(
        CancellationToken cancellationToken);

    Task<TrainerTierResponse?> GetTrainerTierByTrainerIdAsync(
        Guid trainerId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerTierHistoryResponse>> GetTrainerTierHistoryByTrainerIdAsync(
        Guid trainerId,
        CancellationToken cancellationToken);

    Task UpdateTierPermissionsAsync(
        Guid tierId,
        Guid adminUserId,
        AdminUpdateTierPermissionsRequest request,
        CancellationToken cancellationToken);
}
