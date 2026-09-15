using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Application.Finance;

public interface ITrainerPayoutService
{
    Task<AdminTrainerPayoutResponse> GetTrainerPayoutsAsync(CancellationToken cancellationToken);

    Task ProcessTrainerPayoutAsync(
        Guid trainerId,
        Guid adminUserId,
        AdminProcessTrainerPayoutRequest request,
        CancellationToken cancellationToken);
}