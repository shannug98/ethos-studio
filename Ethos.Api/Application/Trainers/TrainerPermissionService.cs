using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Trainers;

public class TrainerPermissionService : ITrainerPermissionService
{
    private readonly AppDbContext _db;

    public TrainerPermissionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasPermissionAsync(
        Guid trainerProfileId,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        var overrideValue = await _db.TrainerPermissionOverrides
            .Where(x =>
                x.TrainerProfileId == trainerProfileId &&
                x.Permission.Code == permissionCode)
            .Select(x => (bool?)x.IsAllowed)
            .FirstOrDefaultAsync(cancellationToken);

        if (overrideValue.HasValue)
            return overrideValue.Value;

        var tierValue = await _db.TrainerProfiles
            .Where(x =>
                x.Id == trainerProfileId &&
                x.CurrentTierId != null)
            .SelectMany(x => x.CurrentTier!.Permissions)
            .Where(x => x.Permission.Code == permissionCode)
            .Select(x => (bool?)x.IsAllowed)
            .FirstOrDefaultAsync(cancellationToken);

        return tierValue ?? false;
    }
}
