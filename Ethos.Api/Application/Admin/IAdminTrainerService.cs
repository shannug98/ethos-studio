using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;

namespace Ethos.Api.Application.Admin;

public interface IAdminTrainerService
{
    Task<IReadOnlyList<TrainerResponse>> GetAllTrainersAsync(
        CancellationToken cancellationToken);

    Task<PagedResult<AdminTrainerListResponse>> GetTrainersAsync(
        int page,
        int pageSize,
        string? search,
        Guid? tierId,
        string? status,
        CancellationToken cancellationToken);

    Task<AdminTrainerSummaryStatsResponse> GetTrainerStatsAsync(
        CancellationToken cancellationToken = default);

    Task<TrainerResponse?> GetTrainerByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<AdminTrainerDetailsResponse?> GetTrainerDetailsByIdAsync(
        Guid trainerId,
        CancellationToken cancellationToken);

    Task UpdateTrainerTierAsync(
        Guid trainerId,
        Guid adminUserId,
        AdminUpdateTrainerTierRequest request,
        CancellationToken cancellationToken);

    Task UpdateTrainerStatusAsync(
        Guid trainerId,
        Guid adminUserId,
        Domain.Enums.TrainerStatus newStatus,
        string? reason,
        CancellationToken cancellationToken);

    Task<TrainerDiagnosticReport?> GetTrainerDiagnosticsAsync(
        Guid trainerId,
        string? traceId,
        CancellationToken cancellationToken);
}
