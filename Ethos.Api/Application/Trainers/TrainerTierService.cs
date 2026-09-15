using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Trainers;

public class TrainerTierService : ITrainerTierService
{
    private readonly AppDbContext _db;

    public TrainerTierService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TrainerTierResponse?> GetMyTierAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles
            .Include(x => x.CurrentTier)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (trainer?.CurrentTier == null)
            return null;

        return new TrainerTierResponse
        {
            Id = trainer.CurrentTier.Id,
            Code = trainer.CurrentTier.Code,
            Name = trainer.CurrentTier.Name,
            DisplayOrder = trainer.CurrentTier.DisplayOrder,
            ApplicationFee = trainer.CurrentTier.ApplicationFee,
            UpgradeFee = trainer.CurrentTier.UpgradeFee,
            Description = trainer.CurrentTier.Description,
            IsActive = trainer.CurrentTier.IsActive
        };
    }

    public async Task<IReadOnlyList<TrainerTierHistoryResponse>> GetMyTierHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _db.TrainerTierHistories
            .Include(x => x.PreviousTier)
            .Include(x => x.NewTier)
            .Where(x => x.TrainerProfile.UserId == userId)
            .OrderByDescending(x => x.ChangedAt)
            .Select(x => new TrainerTierHistoryResponse
            {
                Id = x.Id,
                PreviousTier = x.PreviousTier != null ? x.PreviousTier.Name : null,
                NewTier = x.NewTier.Name,
                Reason = x.Reason,
                ChangedAt = x.ChangedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TrainerPermissionResponse>> GetMyPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (trainer == null)
            return Array.Empty<TrainerPermissionResponse>();

        var allPermissions = await _db.Permissions
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        var overrides = await _db.TrainerPermissionOverrides
            .Where(x => x.TrainerProfileId == trainer.Id)
            .ToDictionaryAsync(x => x.PermissionId, x => x.IsAllowed, cancellationToken);

        Dictionary<Guid, bool> tierPermissions = new();
        if (trainer.CurrentTierId.HasValue)
        {
            tierPermissions = await _db.TrainerTierPermissions
                .Where(x => x.TrainerTierId == trainer.CurrentTierId.Value)
                .ToDictionaryAsync(x => x.PermissionId, x => x.IsAllowed, cancellationToken);
        }

        var result = new List<TrainerPermissionResponse>();

        foreach (var p in allPermissions)
        {
            bool isAllowed = false;
            if (overrides.TryGetValue(p.Id, out var overrideVal))
            {
                isAllowed = overrideVal;
            }
            else if (tierPermissions.TryGetValue(p.Id, out var tierVal))
            {
                isAllowed = tierVal;
            }

            result.Add(new TrainerPermissionResponse
            {
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
                IsAllowed = isAllowed
            });
        }

        return result;
    }
}
