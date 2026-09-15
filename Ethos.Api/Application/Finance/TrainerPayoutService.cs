using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Finance;

public class TrainerPayoutService : ITrainerPayoutService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;

    public TrainerPayoutService(
        AppDbContext db,
        IAdminAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<AdminTrainerPayoutResponse> GetTrainerPayoutsAsync(CancellationToken cancellationToken)
    {
        var trainers = await _db.TrainerProfiles
            .AsNoTracking()
            .Include(t => t.User)
            .Include(t => t.CurrentTier)
            .Include(t => t.Workshops)
            .ThenInclude(w => w.Bookings)
            .OrderBy(t => t.FullName)
            .ToListAsync(cancellationToken);

        // Query previously recorded payouts from AdminActions
        var processedActions = await _db.AdminActions
            .AsNoTracking()
            .Where(a => a.ActionType == "TRAINER_PAYOUT_PROCESSED" &&
                        (a.EntityType == "TrainerProfile" || a.EntityType == "TRAINERPROFILE"))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var latestProcessedMap = processedActions
            .GroupBy(a => a.EntityId)
            .ToDictionary(g => g.Key, g => g.First());

        var payoutItems = new List<AdminTrainerPayoutItem>();

        decimal totalGross = 0m;
        decimal totalTrainerShare = 0m;
        decimal totalStudioShare = 0m;

        foreach (var trainer in trainers)
        {
            var tierCode = trainer.CurrentTier?.Code?.ToUpperInvariant() ?? "UNKNOWN";
            var tierName = trainer.CurrentTier?.Name ?? "Standard Tier";

            // Canonical Tier Commission Mapping
            decimal trainerPct = 0m;
            decimal studioPct = 0m;
            bool hasConfiguredCommission = true;
            string? notes = null;

            switch (tierCode)
            {
                case "SILVER":
                    trainerPct = 60m;
                    studioPct = 40m;
                    break;
                case "GOLD":
                    trainerPct = 70m;
                    studioPct = 30m;
                    break;
                case "DIAMOND":
                    trainerPct = 80m;
                    studioPct = 20m;
                    break;
                case "PLATINUM":
                    // Per user directive: Platinum has no authoritative commission value; flag it rather than guessing.
                    hasConfiguredCommission = false;
                    trainerPct = 0m;
                    studioPct = 0m;
                    notes = "Commission policy for PLATINUM is not configured.";
                    break;
                default:
                    trainerPct = 70m;
                    studioPct = 30m;
                    break;
            }

            var trainerWorkshops = trainer.Workshops.ToList();
            int totalWorkshops = trainerWorkshops.Count;
            int totalBookings = 0;
            decimal grossRevenue = 0m;

            foreach (var ws in trainerWorkshops)
            {
                var validBookings = ws.Bookings
                    .Where(b => b.Status == WorkshopBookingStatus.Confirmed || b.Status == WorkshopBookingStatus.Attended)
                    .ToList();

                totalBookings += validBookings.Count;
                var effectivePrice = ws.AdminApprovedPrice ?? ws.TrainerProposedPrice ?? ws.Price;
                grossRevenue += validBookings.Count * effectivePrice;
            }

            decimal trainerPayoutAmount = 0m;
            decimal studioRetentionAmount = 0m;

            if (hasConfiguredCommission && grossRevenue > 0)
            {
                trainerPayoutAmount = Math.Round(grossRevenue * (trainerPct / 100m), 2);
                studioRetentionAmount = grossRevenue - trainerPayoutAmount;
            }
            else
            {
                studioRetentionAmount = grossRevenue;
            }

            totalGross += grossRevenue;
            totalTrainerShare += trainerPayoutAmount;
            totalStudioShare += studioRetentionAmount;

            string status = "CALCULATED";
            string? payoutRef = null;
            DateTime? processedAt = null;
            string? processedBy = null;

            if (latestProcessedMap.TryGetValue(trainer.Id, out var lastPayout))
            {
                status = "PROCESSED";
                payoutRef = lastPayout.Reason;
                processedAt = lastPayout.CreatedAt;
                processedBy = "Admin";
            }

            payoutItems.Add(new AdminTrainerPayoutItem
            {
                TrainerId = trainer.Id,
                UserId = trainer.UserId,
                TrainerCode = trainer.TrainerCode,
                FullName = trainer.FullName,
                Phone = trainer.User?.Phone ?? "",
                TierCode = tierCode,
                TierName = tierName,
                TrainerSharePercentage = trainerPct,
                StudioSharePercentage = studioPct,
                TotalWorkshops = totalWorkshops,
                TotalBookings = totalBookings,
                GrossRevenue = grossRevenue,
                TrainerPayoutAmount = trainerPayoutAmount,
                StudioRetentionAmount = studioRetentionAmount,
                Status = status,
                PayoutReference = payoutRef,
                ProcessedAt = processedAt,
                ProcessedByAdmin = processedBy,
                HasConfiguredCommission = hasConfiguredCommission,
                Notes = notes
            });
        }

        return new AdminTrainerPayoutResponse
        {
            Summary = new AdminTrainerPayoutSummary
            {
                TotalTrainers = payoutItems.Count,
                TotalGrossRevenue = totalGross,
                TotalTrainerPayouts = totalTrainerShare,
                TotalStudioRetention = totalStudioShare
            },
            Payouts = payoutItems
        };
    }

    public async Task ProcessTrainerPayoutAsync(
        Guid trainerId,
        Guid adminUserId,
        AdminProcessTrainerPayoutRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Payout amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(request.PayoutReference))
            throw new ArgumentException("Payout reference number is required.");

        var trainer = await _db.TrainerProfiles
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == trainerId, cancellationToken);

        if (trainer == null)
            throw new ArgumentException("Trainer profile not found.");

        _auditService.AddAuditLog(
            adminUserId,
            "TRAINER_PAYOUT_PROCESSED",
            "TrainerProfile",
            trainer.Id,
            $"Recorded external payout of {request.Amount:F2} INR for {trainer.FullName} ({trainer.TrainerCode}). Ref: {request.PayoutReference}. Notes: {request.Notes ?? "none"}");

        await _db.SaveChangesAsync(cancellationToken);
    }
}