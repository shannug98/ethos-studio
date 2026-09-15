using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminTrainerUpgradeService : IAdminTrainerUpgradeService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;

    public AdminTrainerUpgradeService(
        AppDbContext db,
        IAdminAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<TrainerUpgradeRequestResponse>> GetAllUpgradeRequestsAsync(
        CancellationToken cancellationToken)
    {
        return await _db.TrainerUpgradeRequests
            .AsNoTracking()
            .Include(x => x.CurrentTier)
            .Include(x => x.RequestedTier)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<TrainerUpgradeRequestResponse>> GetUpgradeRequestsAsync(
        int page,
        int pageSize,
        TrainerUpgradeRequestStatus? status,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.TrainerUpgradeRequests
            .AsNoTracking()
            .Include(x => x.CurrentTier)
            .Include(x => x.RequestedTier)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var requests = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = requests.Select(x => Map(x)).ToList();

        return new PagedResult<TrainerUpgradeRequestResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<TrainerUpgradeRequestResponse?> GetUpgradeRequestByIdAsync(
        Guid upgradeId,
        CancellationToken cancellationToken)
    {
        var req = await _db.TrainerUpgradeRequests
            .AsNoTracking()
            .Include(x => x.CurrentTier)
            .Include(x => x.RequestedTier)
            .FirstOrDefaultAsync(x => x.Id == upgradeId, cancellationToken);

        return req == null ? null : Map(req);
    }

    public async Task ApproveUpgradeRequestAsync(
        Guid requestId,
        Guid adminUserId,
        string? notes,
        CancellationToken cancellationToken)
    {
        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var upgradeReq = await _db.TrainerUpgradeRequests
            .Include(x => x.TrainerProfile)
            .Include(x => x.RequestedTier)
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (upgradeReq == null)
            throw new ArgumentException("Upgrade request not found.");

        if (upgradeReq.Status != TrainerUpgradeRequestStatus.Pending && upgradeReq.Status != TrainerUpgradeRequestStatus.PaymentVerified)
        {
            throw new InvalidOperationException("Only pending or payment verified upgrade requests can be approved.");
        }

        var trainer = upgradeReq.TrainerProfile;
        var previousTierId = trainer.CurrentTierId;

        trainer.CurrentTierId = upgradeReq.RequestedTierId;
        trainer.UpdatedAt = DateTime.UtcNow;

        upgradeReq.Status = TrainerUpgradeRequestStatus.Approved;
        upgradeReq.AdminNotes = notes?.Trim();
        upgradeReq.ReviewedAt = DateTime.UtcNow;
        upgradeReq.ReviewedByUserId = adminUserId;

        _db.TrainerTierHistories.Add(new TrainerTierHistory
        {
            Id = Guid.NewGuid(),
            TrainerProfileId = trainer.Id,
            PreviousTierId = previousTierId,
            NewTierId = upgradeReq.RequestedTierId,
            Reason = $"Tier upgrade approved by admin. {upgradeReq.Reason}".Trim(),
            ChangedByUserId = adminUserId,
            ChangedAt = DateTime.UtcNow
        });

        _auditService.AddAuditLog(
            adminUserId,
            "TRAINER_UPGRADE_APPROVED",
            "TrainerUpgradeRequest",
            upgradeReq.Id,
            notes ?? $"Upgraded trainer to {upgradeReq.RequestedTier.Name}");

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task RejectUpgradeRequestAsync(
        Guid requestId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        var upgradeReq = await _db.TrainerUpgradeRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (upgradeReq == null)
            throw new ArgumentException("Upgrade request not found.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required.");

        upgradeReq.Status = TrainerUpgradeRequestStatus.Rejected;
        upgradeReq.AdminNotes = reason.Trim();
        upgradeReq.ReviewedAt = DateTime.UtcNow;
        upgradeReq.ReviewedByUserId = adminUserId;

        _auditService.AddAuditLog(
            adminUserId,
            "TRAINER_UPGRADE_REJECTED",
            "TrainerUpgradeRequest",
            upgradeReq.Id,
            reason);

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static TrainerUpgradeRequestResponse Map(TrainerUpgradeRequest x)
    {
        return new TrainerUpgradeRequestResponse
        {
            Id = x.Id,
            CurrentTier = x.CurrentTier?.Name ?? "Silver Trainer",
            RequestedTier = x.RequestedTier?.Name ?? "Gold Trainer",
            Status = x.Status.ToString(),
            Reason = x.Reason,
            AdminNotes = x.AdminNotes,
            CreatedAt = x.CreatedAt,
            ReviewedAt = x.ReviewedAt
        };
    }
}
