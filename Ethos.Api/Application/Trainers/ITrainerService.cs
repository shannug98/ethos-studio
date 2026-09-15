using Ethos.Api.Contracts.Trainers;
using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Application.Trainers;

public interface ITrainerService
{
    Task<TrainerResponse?> GetMeAsync(
        Guid userId,
        CancellationToken ct);

    Task<TrainerResponse?> UpdateMeAsync(
        Guid userId,
        UpdateTrainerRequest request,
        CancellationToken ct);

    Task<TrainerDashboardResponse?> GetDashboardAsync(
        Guid userId,
        CancellationToken ct);

    Task<TrainerResponse?> UploadProfilePhotoAsync(
        Guid userId,
        IFormFile file,
        CancellationToken ct);

    Task<List<TrainerTierResponse>> GetTiersAsync(
        CancellationToken ct);

    Task<List<TrainerTierHistoryResponse>> GetTierHistoryAsync(
        Guid userId,
        CancellationToken ct);

    Task<List<TrainerWorkshopResponse>> GetMyWorkshopsAsync(
        Guid userId,
        CancellationToken ct);

    Task<TrainerWorkshopResponse?> GetWorkshopAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken ct);

    Task<List<TrainerWorkshopStudentResponse>> GetWorkshopStudentsAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken ct);

    Task<List<TrainerWorkshopFeedbackResponse>> GetWorkshopFeedbackAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken ct);

    Task<TrainerPerformanceResponse?> GetPerformanceAsync(
        Guid userId,
        CancellationToken ct);

    Task<TrainerUpgradeRequestResponse?> RequestUpgradeAsync(
        Guid userId,
        Guid requestedTierId,
        CancellationToken ct);

    Task<List<TrainerUpgradeRequestResponse>> GetUpgradeRequestsAsync(
        Guid userId,
        CancellationToken ct);
}
