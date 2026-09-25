using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;

namespace Ethos.Api.Application.Workshops;

public interface IWorkshopPricingService
{
    Task<WorkshopPricingResponse> CalculatePricingAsync(
        Workshop workshop,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    Task<WorkshopPriceQuoteResponse> CalculateQuoteAsync(
        Guid workshopId,
        int quantity,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    Task<WorkshopPriceQuoteResponse> CalculateTicketTypeQuoteAsync(
        WorkshopPassType ticketType,
        int quantity,
        int currentTicketsSold,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    void ValidateTicketTypePricingTiers(
        int totalQuantity,
        List<Ethos.Api.Contracts.Admin.AdminWorkshopPricingTierItem>? tiers);

    Task EnsureDefaultTiersAsync(
        Workshop workshop,
        CancellationToken cancellationToken = default);

    decimal CalculatePublicPrice(decimal startingPrice, int bookedSeats);

    decimal CalculateStudentPrice(decimal startingPrice);

    WorkshopPricingResponse CalculatePricing(
        Workshop workshop,
        IReadOnlyList<WorkshopPricingTier>? tiers,
        int bookedSeats,
        bool isStudentEligible)
    {
        var tierList = (tiers ?? Array.Empty<WorkshopPricingTier>())
            .Where(t => t.WorkshopPassTypeId == null)
            .OrderBy(t => t.TierNumber)
            .ToList();

        int nextTicketIndex = bookedSeats + 1;
        var activeTier = tierList.FirstOrDefault(t => nextTicketIndex >= t.MinTickets && (t.MaxTickets == null || nextTicketIndex <= t.MaxTickets))
            ?? tierList.LastOrDefault()
            ?? new WorkshopPricingTier { TierNumber = 1, TierName = "Standard Tier", Price = workshop.Price };

        int currentTierNum = activeTier.TierNumber;
        var currentPrice = activeTier.Price;
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
        var nextTier = tierList.FirstOrDefault(t => t.TierNumber > currentTierNum);
        if (nextTier != null) nextPrice = nextTier.Price;

        var remainingSeats = Math.Max(0, workshop.Capacity - bookedSeats);
        var isFull = remainingSeats <= 0;
        var studentPrice = CalculateStudentPrice(currentPrice);
        var finalAmount = isStudentEligible ? studentPrice : currentPrice;

        var tierDtos = tierList.Select(t =>
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

    Task<bool> IsStudentEligibleAsync(
        Guid? userId,
        CancellationToken cancellationToken = default) => Task.FromResult(false);
}
