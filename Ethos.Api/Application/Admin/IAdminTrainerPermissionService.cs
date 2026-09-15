using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;

namespace Ethos.Api.Application.Admin;

public interface IAdminTrainerPermissionService
{
    Task<IReadOnlyList<TrainerPermissionResponse>> GetAllPermissionsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminTrainerPermissionDetailResponse>> GetTrainerPermissionsAsync(
        Guid trainerId,
        CancellationToken cancellationToken);

    Task<AdminTrainerPermissionDetailResponse> SetTrainerPermissionOverrideAsync(
        Guid trainerId,
        Guid adminUserId,
        AdminPermissionOverrideRequest request,
        CancellationToken cancellationToken);

    Task<AdminTrainerPermissionDetailResponse> ClearTrainerPermissionOverrideAsync(
        Guid trainerId,
        Guid adminUserId,
        string permissionCode,
        string reason,
        CancellationToken cancellationToken);
}
