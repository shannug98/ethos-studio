using Ethos.Api.Application.Students;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Workshops;

public class WorkshopPricingService : IWorkshopPricingService
{
    private readonly AppDbContext _db;
    private readonly IStudentEligibilityService _eligibilityService;

    public WorkshopPricingService(
        AppDbContext db,
        IStudentEligibilityService eligibilityService)
    {
        _db = db;
        _eligibilityService = eligibilityService;
    }

    public async Task EnsureDefaultTiersAsync(
        Workshop workshop,
        CancellationToken cancellationToken = default)
    {
        // If workshop has pass types, it uses the new ticket architecture; do not generate legacy tiers
        var hasPassTypes = await _db.WorkshopPassTypes
            .AnyAsync(p => p.WorkshopId == workshop.Id && p.IsActive, cancellationToken);
        if (hasPassTypes)
        {
            return;
        }

        var existingLegacyTiers = await _db.WorkshopPricingTiers
            .Where(t => t.WorkshopId == workshop.Id && t.WorkshopPassTypeId == null)
            .OrderBy(t => t.TierNumber)
            .ToListAsync(cancellationToken);

        if (existingLegacyTiers.Count == 4)
        {
            return;
        }

        var basePrice = workshop.AdminApprovedPrice ?? workshop.Price;
        if (basePrice <= 0) basePrice = 299m;

        if (existingLegacyTiers.Count > 0)
        {
            _db.WorkshopPricingTiers.RemoveRange(existingLegacyTiers);
        }

        var defaultTiers = new List<WorkshopPricingTier>
        {
            new()
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                WorkshopPassTypeId = null,
                TierNumber = 1,
                TierName = "Early pricing",
                MinTickets = 1,
                MaxTickets = 10,
                Price = basePrice,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                WorkshopPassTypeId = null,
                TierNumber = 2,
                TierName = "Standard pricing",
                MinTickets = 11,
                MaxTickets = 20,
                Price = basePrice + 100m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                WorkshopPassTypeId = null,
                TierNumber = 3,
                TierName = "Late pricing",
                MinTickets = 21,
                MaxTickets = 30,
                Price = basePrice + 200m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                WorkshopPassTypeId = null,
                TierNumber = 4,
                TierName = "Final pricing tier",
                MinTickets = 31,
                MaxTickets = null,
                Price = basePrice + 300m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _db.WorkshopPricingTiers.AddRange(defaultTiers);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public decimal CalculatePublicPrice(decimal startingPrice, int bookedSeats)
    {
        var tier = bookedSeats / 10;
        return startingPrice + (tier * 100m);
    }

    public decimal CalculateStudentPrice(decimal startingPrice)
    {
        return Math.Max(0m, startingPrice - 100m);
    }

    public async Task<bool> IsStudentEligibleAsync(
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        if (!userId.HasValue || userId.Value == Guid.Empty)
            return false;

        return await _eligibilityService.IsStudentPortalEligibleAsync(userId.Value, cancellationToken);
    }

    public async Task<WorkshopPricingResponse> CalculatePricingAsync(
        Workshop workshop,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var hasPassTypes = await _db.WorkshopPassTypes
            .AnyAsync(p => p.WorkshopId == workshop.Id && p.IsActive, cancellationToken);

        if (!hasPassTypes)
        {
            await EnsureDefaultTiersAsync(workshop, cancellationToken);
        }

        var tiers = await _db.WorkshopPricingTiers
            .Where(t => t.WorkshopId == workshop.Id && t.WorkshopPassTypeId == null)
            .OrderBy(t => t.TierNumber)
            .ToListAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var bookedSeats = await _db.WorkshopBookings
            .Where(b => b.WorkshopId == workshop.Id &&
                       (b.Status == WorkshopBookingStatus.Confirmed ||
                        b.Status == WorkshopBookingStatus.Attended ||
                        (b.Status == WorkshopBookingStatus.PendingPayment &&
                         b.ReservationExpiresAt.HasValue &&
                         b.ReservationExpiresAt.Value > nowUtc)))
            .SumAsync(b => (int?)b.Quantity, cancellationToken) ?? 0;

        var isStudentEligible = await IsStudentEligibleAsync(userId, cancellationToken);

        return ((IWorkshopPricingService)this).CalculatePricing(workshop, tiers, bookedSeats, isStudentEligible);
    }

    public async Task<WorkshopPriceQuoteResponse> CalculateQuoteAsync(
        Guid workshopId,
        int quantity,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) quantity = 1;

        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        if (workshop.IsBookingClosed())
        {
            throw new InvalidOperationException("Bookings for this workshop are closed as the booking cutoff time has passed.");
        }

        var hasPassTypes = await _db.WorkshopPassTypes
            .AnyAsync(p => p.WorkshopId == workshop.Id && p.IsActive, cancellationToken);

        if (!hasPassTypes)
        {
            await EnsureDefaultTiersAsync(workshop, cancellationToken);
        }

        var tiers = await _db.WorkshopPricingTiers
            .Where(t => t.WorkshopId == workshopId && t.WorkshopPassTypeId == null)
            .OrderBy(t => t.TierNumber)
            .ToListAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var bookedSeats = await _db.WorkshopBookings
            .Where(b => b.WorkshopId == workshopId &&
                       (b.Status == WorkshopBookingStatus.Confirmed ||
                        b.Status == WorkshopBookingStatus.Attended ||
                        (b.Status == WorkshopBookingStatus.PendingPayment &&
                         b.ReservationExpiresAt.HasValue &&
                         b.ReservationExpiresAt.Value > nowUtc)))
            .SumAsync(b => (int?)b.Quantity, cancellationToken) ?? 0;

        var remainingSeats = Math.Max(0, workshop.Capacity - bookedSeats);
        if (quantity > remainingSeats)
        {
            throw new InvalidOperationException($"Only {remainingSeats} seats remaining for this workshop.");
        }

        var isStudent = false;
        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            isStudent = await _eligibilityService.IsStudentPortalEligibleAsync(userId.Value, cancellationToken);
        }

        // Calculate each ticket's tier
        // Target tickets are: (bookedSeats + 1) through (bookedSeats + quantity)
        var tierCounts = new Dictionary<int, int>(); // TierNumber -> count
        for (int i = 1; i <= quantity; i++)
        {
            int ticketIdx = bookedSeats + i;
            int tierNum;
            if (ticketIdx <= 10) tierNum = 1;
            else if (ticketIdx <= 20) tierNum = 2;
            else if (ticketIdx <= 30) tierNum = 3;
            else tierNum = 4;

            if (!tierCounts.ContainsKey(tierNum)) tierCounts[tierNum] = 0;
            tierCounts[tierNum]++;
        }

        var breakdown = new List<WorkshopPriceQuoteItem>();
        decimal grossTotal = 0m;

        foreach (var kvp in tierCounts.OrderBy(k => k.Key))
        {
            var tierNum = kvp.Key;
            var count = kvp.Value;
            var tierObj = tiers.FirstOrDefault(t => t.TierNumber == tierNum) ?? tiers.Last();

            var unitPrice = isStudent ? CalculateStudentPrice(tierObj.Price) : tierObj.Price;
            var subtotal = unitPrice * count;
            grossTotal += subtotal;

            breakdown.Add(new WorkshopPriceQuoteItem
            {
                TierNumber = tierNum,
                TierName = tierObj.TierName,
                Quantity = count,
                UnitPrice = unitPrice,
                Subtotal = subtotal
            });
        }

        var isSplit = breakdown.Count > 1;
        var splitMessage = isSplit
            ? $"Only {breakdown[0].Quantity} tickets remain at ₹{breakdown[0].UnitPrice}. The remaining {quantity - breakdown[0].Quantity} tickets will be charged at ₹{breakdown[1].UnitPrice}."
            : string.Empty;

        return new WorkshopPriceQuoteResponse
        {
            WorkshopId = workshopId,
            WorkshopTitle = workshop.Title,
            RequestedQuantity = quantity,
            TotalAmount = grossTotal,
            IsSplitTier = isSplit,
            SplitTierMessage = splitMessage,
            Breakdown = breakdown
        };
    }

    public async Task<WorkshopPriceQuoteResponse> CalculateTicketTypeQuoteAsync(
        WorkshopPassType ticketType,
        int quantity,
        int currentTicketsSold,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) quantity = 1;

        if (currentTicketsSold + quantity > ticketType.TotalQuantity)
        {
            var remaining = Math.Max(0, ticketType.TotalQuantity - currentTicketsSold);
            throw new InvalidOperationException($"Only {remaining} passes remaining for ticket '{ticketType.Name}'.");
        }

        var isStudent = false;
        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            isStudent = await _eligibilityService.IsStudentPortalEligibleAsync(userId.Value, cancellationToken);
        }

        var tiers = ticketType.PricingTiers != null && ticketType.PricingTiers.Count > 0
            ? ticketType.PricingTiers.OrderBy(t => t.TierNumber).ToList()
            : await _db.WorkshopPricingTiers
                .Where(t => t.WorkshopPassTypeId == ticketType.Id)
                .OrderBy(t => t.TierNumber)
                .ToListAsync(cancellationToken);

        var breakdown = new List<WorkshopPriceQuoteItem>();
        decimal grossTotal = 0m;

        if (tiers.Count == 0)
        {
            // Flat price ticket type
            var unitPrice = isStudent ? CalculateStudentPrice(ticketType.Price) : ticketType.Price;
            grossTotal = unitPrice * quantity;

            breakdown.Add(new WorkshopPriceQuoteItem
            {
                TierNumber = 1,
                TierName = "Standard",
                Quantity = quantity,
                UnitPrice = unitPrice,
                Subtotal = grossTotal
            });

            return new WorkshopPriceQuoteResponse
            {
                WorkshopId = ticketType.WorkshopId,
                WorkshopTitle = ticketType.Workshop?.Title ?? "Workshop",
                RequestedQuantity = quantity,
                TotalAmount = grossTotal,
                IsSplitTier = false,
                SplitTierMessage = string.Empty,
                Breakdown = breakdown
            };
        }

        // Option A: Sequential per-ticket progressive pricing
        // Each ticket i (1..quantity) gets slot = currentTicketsSold + i
        var tierCounts = new Dictionary<int, (WorkshopPricingTier Tier, int Count)>();

        for (int i = 1; i <= quantity; i++)
        {
            int slotIndex = currentTicketsSold + i;

            var matchingTier = tiers.FirstOrDefault(t => slotIndex >= t.MinTickets && (t.MaxTickets == null || slotIndex <= t.MaxTickets));
            if (matchingTier == null)
            {
                throw new InvalidOperationException(
                    $"No matching pricing tier found for ticket slot #{slotIndex} in ticket '{ticketType.Name}'.");
            }

            if (!tierCounts.ContainsKey(matchingTier.TierNumber))
            {
                tierCounts[matchingTier.TierNumber] = (matchingTier, 0);
            }
            var current = tierCounts[matchingTier.TierNumber];
            tierCounts[matchingTier.TierNumber] = (current.Tier, current.Count + 1);
        }

        foreach (var kvp in tierCounts.OrderBy(k => k.Key))
        {
            var tierObj = kvp.Value.Tier;
            var count = kvp.Value.Count;

            var unitPrice = isStudent ? CalculateStudentPrice(tierObj.Price) : tierObj.Price;
            var subtotal = unitPrice * count;
            grossTotal += subtotal;

            breakdown.Add(new WorkshopPriceQuoteItem
            {
                TierNumber = tierObj.TierNumber,
                TierName = tierObj.TierName,
                Quantity = count,
                UnitPrice = unitPrice,
                Subtotal = subtotal
            });
        }

        var isSplit = breakdown.Count > 1;
        var splitMessage = isSplit
            ? $"Only {breakdown[0].Quantity} ticket(s) at ₹{breakdown[0].UnitPrice}. The remaining {quantity - breakdown[0].Quantity} ticket(s) charged at ₹{breakdown[1].UnitPrice}."
            : string.Empty;

        return new WorkshopPriceQuoteResponse
        {
            WorkshopId = ticketType.WorkshopId,
            WorkshopTitle = ticketType.Workshop?.Title ?? "Workshop",
            RequestedQuantity = quantity,
            TotalAmount = grossTotal,
            IsSplitTier = isSplit,
            SplitTierMessage = splitMessage,
            Breakdown = breakdown
        };
    }

    public void ValidateTicketTypePricingTiers(
        int totalQuantity,
        List<AdminWorkshopPricingTierItem>? tiers)
    {
        if (tiers == null || tiers.Count == 0) return;

        if (tiers.Select(t => t.MinTickets).Distinct().Count() != tiers.Count)
        {
            throw new ArgumentException("Duplicate MinTickets detected in pricing tiers.");
        }

        var sortedTiers = tiers.OrderBy(t => t.TierNumber).ToList();

        if (sortedTiers[0].MinTickets != 1)
        {
            throw new ArgumentException("The first pricing tier must start at ticket 1.");
        }

        for (int i = 0; i < sortedTiers.Count; i++)
        {
            var tier = sortedTiers[i];
            if (tier.MinTickets < 1)
            {
                throw new ArgumentException($"Tier '{tier.TierName}' MinTickets must be at least 1.");
            }

            if (tier.Price <= 0)
            {
                throw new ArgumentException($"Tier '{tier.TierName}' price must be greater than zero.");
            }

            if (tier.MinTickets > totalQuantity)
            {
                throw new ArgumentException($"Tier '{tier.TierName}' MinTickets ({tier.MinTickets}) exceeds total ticket capacity ({totalQuantity}).");
            }

            if (tier.MaxTickets.HasValue && tier.MaxTickets.Value > totalQuantity)
            {
                throw new ArgumentException($"Tier '{tier.TierName}' MaxTickets ({tier.MaxTickets.Value}) exceeds total ticket capacity ({totalQuantity}). Tiers must not exceed ticket capacity.");
            }

            if (tier.MaxTickets.HasValue && tier.MaxTickets.Value < tier.MinTickets)
            {
                throw new ArgumentException($"Tier '{tier.TierName}' MaxTickets ({tier.MaxTickets.Value}) cannot be less than MinTickets ({tier.MinTickets}).");
            }

            if (i > 0)
            {
                var prevTier = sortedTiers[i - 1];
                if (!prevTier.MaxTickets.HasValue)
                {
                    throw new ArgumentException($"Tier '{prevTier.TierName}' is open-ended, so no subsequent tier can follow it.");
                }
                if (tier.MinTickets != prevTier.MaxTickets.Value + 1)
                {
                    throw new ArgumentException(
                        $"Pricing tier gap or overlap detected between '{prevTier.TierName}' (ends at {prevTier.MaxTickets.Value}) and '{tier.TierName}' (starts at {tier.MinTickets}). Tiers must be strictly contiguous.");
                }
                if (tier.Price < prevTier.Price)
                {
                    throw new ArgumentException(
                        $"Tier '{tier.TierName}' price ({tier.Price}) cannot be less than previous tier '{prevTier.TierName}' price ({prevTier.Price}). Pricing must be non-decreasing.");
                }
            }
        }

        var lastTier = sortedTiers.Last();
        if (lastTier.MaxTickets.HasValue && lastTier.MaxTickets.Value < totalQuantity)
        {
            throw new ArgumentException(
                $"Pricing tiers do not cover total ticket capacity ({totalQuantity}). The final tier ends at {lastTier.MaxTickets.Value}. Either make the final tier open-ended or set it to cover up to {totalQuantity}.");
        }
    }
}
