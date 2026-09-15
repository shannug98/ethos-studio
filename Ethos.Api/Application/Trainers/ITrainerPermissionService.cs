namespace Ethos.Api.Application.Trainers;

public interface ITrainerPermissionService
{
    Task<bool> HasPermissionAsync(
        Guid trainerProfileId,
        string permissionCode,
        CancellationToken cancellationToken = default);
}
