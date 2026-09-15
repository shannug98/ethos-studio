using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Trainers;

public class TrainerUpgradeService : ITrainerUpgradeService
{
    private readonly AppDbContext _db;

    public TrainerUpgradeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TrainerUpgradeRequestResponse>> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _db.TrainerUpgradeRequests
            .Include(x => x.CurrentTier)
            .Include(x => x.RequestedTier)
            .Where(x => x.TrainerProfile.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new TrainerUpgradeRequestResponse
            {
                Id = x.Id,
                CurrentTier = x.CurrentTier.Name,
                RequestedTier = x.RequestedTier.Name,
                Status = x.Status.ToString(),
                Reason = x.Reason,
                AdminNotes = x.AdminNotes,
                CreatedAt = x.CreatedAt,
                ReviewedAt = x.ReviewedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<TrainerUpgradeRequestResponse> CreateAsync(
        Guid userId,
        CreateTrainerUpgradeRequest request,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles
            .Include(x => x.CurrentTier)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (trainer == null || trainer.CurrentTier == null || !trainer.CurrentTierId.HasValue)
        {
            throw new InvalidOperationException("Trainer profile or active current tier not found.");
        }

        if (string.Equals(trainer.CurrentTier.Code, "PLATINUM", StringComparison.OrdinalIgnoreCase) ||
            !await _db.TrainerTiers.AnyAsync(t => t.DisplayOrder > trainer.CurrentTier.DisplayOrder && t.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("You have reached the highest Ethos trainer tier. No further pathway upgrades are available.");
        }

        var requestedTier = await _db.TrainerTiers
            .FirstOrDefaultAsync(x => x.Id == request.RequestedTierId && x.IsActive, cancellationToken);

        if (requestedTier == null)
        {
            throw new ArgumentException("Requested tier does not exist or is inactive.");
        }

        var nextTier = await _db.TrainerTiers
            .Where(t => t.IsActive && t.DisplayOrder > trainer.CurrentTier.DisplayOrder)
            .OrderBy(t => t.DisplayOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextTier == null || requestedTier.Id != nextTier.Id)
        {
            throw new InvalidOperationException($"You can only request an upgrade to the next immediate tier ({nextTier?.Name ?? "none"}).");
        }

        var existingPending = await _db.TrainerUpgradeRequests
            .AnyAsync(x => x.TrainerProfileId == trainer.Id &&
                (x.Status == TrainerUpgradeRequestStatus.Pending ||
                 x.Status == TrainerUpgradeRequestStatus.PaymentPending ||
                 x.Status == TrainerUpgradeRequestStatus.PaymentVerified), cancellationToken);

        if (existingPending)
        {
            throw new InvalidOperationException("You already have an active pending upgrade request.");
        }

        var upgradeReq = new TrainerUpgradeRequest
        {
            Id = Guid.NewGuid(),
            TrainerProfileId = trainer.Id,
            CurrentTierId = trainer.CurrentTierId.Value,
            RequestedTierId = requestedTier.Id,
            Status = TrainerUpgradeRequestStatus.Pending,
            Reason = request.Reason?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.TrainerUpgradeRequests.Add(upgradeReq);
        await _db.SaveChangesAsync(cancellationToken);

        return new TrainerUpgradeRequestResponse
        {
            Id = upgradeReq.Id,
            CurrentTier = trainer.CurrentTier.Name,
            RequestedTier = requestedTier.Name,
            Status = upgradeReq.Status.ToString(),
            Reason = upgradeReq.Reason,
            CreatedAt = upgradeReq.CreatedAt
        };
    }

    public async Task CancelAsync(
        Guid userId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var upgradeReq = await _db.TrainerUpgradeRequests
            .FirstOrDefaultAsync(x => x.Id == requestId && x.TrainerProfile.UserId == userId, cancellationToken);

        if (upgradeReq == null)
        {
            throw new ArgumentException("Upgrade request not found or access denied.");
        }

        if (upgradeReq.Status != TrainerUpgradeRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only pending upgrade requests can be cancelled.");
        }

        upgradeReq.Status = TrainerUpgradeRequestStatus.Cancelled;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
