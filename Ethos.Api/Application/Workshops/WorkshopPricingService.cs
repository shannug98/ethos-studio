using Ethos.Api.Application.Students;
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
        var existingTiers = await _db.WorkshopPricingTiers
            .Where(t => t.WorkshopId == workshop.Id)
            .OrderBy(t => t.TierNumber)
            .ToListAsync(cancellationToken);

        if (existingTiers.Count == 4)
        {
            return;
        }

        var basePrice = workshop.AdminApprovedPrice ?? workshop.Price;
        if (basePrice <= 0) basePrice = 299m;

        if (existingTiers.Count > 0)
        {
            _db.WorkshopPricingTiers.RemoveRange(existingTiers);
        }

        var defaultTiers = new List<WorkshopPricingTier>
        {
            new()
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
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

    public async Task<WorkshopPricingResponse> CalculatePricingAsync(
        Workshop workshop,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultTiersAsync(workshop, cancellationToken);

        var tiers = await _db.WorkshopPricingTiers
            .Where(t => t.WorkshopId == workshop.Id)
            .OrderBy(t => t.TierNumber)
            .ToListAsync(cancellationToken);

        // Sum Confirmed bookings quantity exclusively
        var bookedSeats = await _db.WorkshopBookings
            .Where(b => b.WorkshopId == workshop.Id && b.Status == WorkshopBookingStatus.Confirmed)
            .SumAsync(b => b.Quantity, cancellationToken);

        int nextTicketIndex = bookedSeats + 1;
        var activeTier = tiers.FirstOrDefault(t => nextTicketIndex >= t.MinTickets && (t.MaxTickets == null || nextTicketIndex <= t.MaxTickets))
            ?? tiers.LastOrDefault()
            ?? new WorkshopPricingTier { TierNumber = 1, TierName = "Standard Tier", Price = workshop.Price };

        int currentTierNum = activeTier.TierNumber;
        var currentPrice = activeTier.Price;

        // Starting price for currently available tickets (if Tier 1 is finished, starting price is Tier 2's price)
        var startingPrice = currentPrice;

        int minSeats = activeTier.MinTickets > 0 ? activeTier.MinTickets : 1;
        int? maxSeats = activeTier.MaxTickets;

        int tierCapacity = maxSeats.HasValue
            ? Math.Max(1, maxSeats.Value - minSeats + 1)
            : Math.Max(1, workshop.Capacity - minSeats + 1);

        int ticketsFilledInTier = Math.Clamp(bookedSeats - (minSeats - 1), 0, tierCapacity);
        int ticketsRemainingInTier = Math.Max(0, tierCapacity - ticketsFilledInTier);

        int progressPercentage = tierCapacity > 0
            ? (int)Math.Round(((double)ticketsFilledInTier / tierCapacity) * 100)
            : 100;

        decimal? nextPrice = null;
        var nextTier = tiers.FirstOrDefault(t => t.TierNumber > currentTierNum);
        if (nextTier != null) nextPrice = nextTier.Price;

        var isStudentEligible = false;
        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            isStudentEligible = await _eligibilityService.IsStudentPortalEligibleAsync(userId.Value, cancellationToken);
        }

        var remainingSeats = Math.Max(0, workshop.Capacity - bookedSeats);
        var isFull = remainingSeats <= 0;
        var studentPrice = CalculateStudentPrice(currentPrice);
        var finalAmount = isStudentEligible ? studentPrice : currentPrice;

        var tierDtos = tiers.Select(t =>
        {
            string status;
            if (t.TierNumber < currentTierNum) status = "COMPLETED";
            else if (t.TierNumber == currentTierNum) status = "ACTIVE";
            else status = "UPCOMING";

            return new WorkshopPricingTierDto
            {
                TierNumber = t.TierNumber,
                TierName = t.TierName,
                MinTickets = t.MinTickets,
                MaxTickets = t.MaxTickets,
                Price = t.Price,
                Status = status
            };
        }).ToList();

        return new WorkshopPricingResponse
        {
            WorkshopId = workshop.Id,
            WorkshopTitle = workshop.Title,
            StartingPrice = startingPrice,
            CurrentPrice = currentPrice,
            CurrentPublicPrice = currentPrice,
            StudentPrice = studentPrice,
            FinalAmount = finalAmount,
            CurrentTier = currentTierNum,
            CurrentTierName = activeTier.TierName,
            TicketsSold = bookedSeats,
            TierCapacity = tierCapacity,
            TicketsFilledInTier = ticketsFilledInTier,
            TicketsRemainingInTier = ticketsRemainingInTier,
            NextPrice = nextPrice,
            ProgressPercentage = progressPercentage,
            IsStudentEligible = isStudentEligible,
            Capacity = workshop.Capacity,
            BookedSeats = bookedSeats,
            RemainingSeats = remainingSeats,
            IsFull = isFull,
            Tiers = tierDtos
        };
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

        await EnsureDefaultTiersAsync(workshop, cancellationToken);

        var tiers = await _db.WorkshopPricingTiers
            .Where(t => t.WorkshopId == workshopId)
            .OrderBy(t => t.TierNumber)
            .ToListAsync(cancellationToken);

        var bookedSeats = await _db.WorkshopBookings
            .Where(b => b.WorkshopId == workshopId && b.Status == WorkshopBookingStatus.Confirmed)
            .SumAsync(b => b.Quantity, cancellationToken);

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
}
