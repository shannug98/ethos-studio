using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminTrainerTierService : IAdminTrainerTierService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;

    public AdminTrainerTierService(
        AppDbContext db,
        IAdminAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<TrainerTierResponse>> GetAllTiersAsync(
        CancellationToken cancellationToken)
    {
        return await _db.TrainerTiers
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new TrainerTierResponse
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                DisplayOrder = x.DisplayOrder,
                ApplicationFee = x.ApplicationFee,
                UpgradeFee = x.UpgradeFee,
                Description = x.Description,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<TrainerTierResponse?> GetTrainerTierByTrainerIdAsync(
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles
            .AsNoTracking()
            .Include(t => t.CurrentTier)
            .FirstOrDefaultAsync(t => t.Id == trainerId || t.UserId == trainerId, cancellationToken);

        if (trainer == null || trainer.CurrentTier == null) return null;

        var x = trainer.CurrentTier;
        return new TrainerTierResponse
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            DisplayOrder = x.DisplayOrder,
            ApplicationFee = x.ApplicationFee,
            UpgradeFee = x.UpgradeFee,
            Description = x.Description,
            IsActive = x.IsActive
        };
    }

    public async Task<IReadOnlyList<TrainerTierHistoryResponse>> GetTrainerTierHistoryByTrainerIdAsync(
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == trainerId || t.UserId == trainerId, cancellationToken);

        if (trainer == null) return Array.Empty<TrainerTierHistoryResponse>();

        return await _db.TrainerTierHistories
            .AsNoTracking()
            .Include(th => th.PreviousTier)
            .Include(th => th.NewTier)
            .Where(th => th.TrainerProfileId == trainer.Id)
            .OrderByDescending(th => th.ChangedAt)
            .Select(th => new TrainerTierHistoryResponse
            {
                Id = th.Id,
                PreviousTier = th.PreviousTier != null ? th.PreviousTier.Name : null,
                NewTier = th.NewTier.Name,
                Reason = th.Reason,
                ChangedAt = th.ChangedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateTierPermissionsAsync(
        Guid tierId,
        Guid adminUserId,
        AdminUpdateTierPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var tier = await _db.TrainerTiers
            .Include(x => x.Permissions)
            .FirstOrDefaultAsync(x => x.Id == tierId, cancellationToken);

        if (tier == null)
            throw new ArgumentException("Trainer tier not found.");

        foreach (var pReq in request.Permissions)
        {
            var permission = await _db.Permissions
                .FirstOrDefaultAsync(x => x.Code == pReq.PermissionCode, cancellationToken);

            if (permission != null)
            {
                var existingTp = tier.Permissions
                    .FirstOrDefault(x => x.PermissionId == permission.Id);

                if (existingTp != null)
                {
                    existingTp.IsAllowed = pReq.IsAllowed;
                }
                else
                {
                    _db.TrainerTierPermissions.Add(new TrainerTierPermission
                    {
                        TrainerTierId = tier.Id,
                        PermissionId = permission.Id,
                        IsAllowed = pReq.IsAllowed
                    });
                }
            }
        }

        _auditService.AddAuditLog(
            adminUserId,
            "TRAINER_TIER_PERMISSIONS_UPDATED",
            "TrainerTier",
            tier.Id,
            $"Updated permissions for tier {tier.Name}");

        await _db.SaveChangesAsync(cancellationToken);
    }
}
