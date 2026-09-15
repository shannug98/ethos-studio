using Ethos.Api.Contracts.Trainers;

namespace Ethos.Api.Application.Trainers;

public interface ITrainerPerformanceService
{
    Task<TrainerPerformanceResponse?> GetMyPerformanceAsync(
        Guid userId,
        CancellationToken cancellationToken,
        bool checkPermission = true);

    Task<IReadOnlyList<TrainerPerformanceResponse>> GetMyPerformanceHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken,
        bool checkPermission = true);
}
