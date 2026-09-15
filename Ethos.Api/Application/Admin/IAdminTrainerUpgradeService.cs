using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Application.Admin;

public interface IAdminTrainerUpgradeService
{
    Task<IReadOnlyList<TrainerUpgradeRequestResponse>> GetAllUpgradeRequestsAsync(
        CancellationToken cancellationToken);

    Task<PagedResult<TrainerUpgradeRequestResponse>> GetUpgradeRequestsAsync(
        int page,
        int pageSize,
        TrainerUpgradeRequestStatus? status,
        CancellationToken cancellationToken);

    Task<TrainerUpgradeRequestResponse?> GetUpgradeRequestByIdAsync(
        Guid upgradeId,
        CancellationToken cancellationToken);

    Task ApproveUpgradeRequestAsync(
        Guid requestId,
        Guid adminUserId,
        string? notes,
        CancellationToken cancellationToken);

    Task RejectUpgradeRequestAsync(
        Guid requestId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken);
}
