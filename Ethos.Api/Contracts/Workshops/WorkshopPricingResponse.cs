namespace Ethos.Api.Contracts.Workshops;

public class WorkshopPricingTierDto
{
    public int TierNumber { get; set; } // 1, 2, 3, 4
    public string TierName { get; set; } = string.Empty;
    public int MinTickets { get; set; }
    public int? MaxTickets { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "UPCOMING"; // COMPLETED, ACTIVE, UPCOMING
}

public class WorkshopPriceQuoteItem
{
    public int TierNumber { get; set; }
    public string TierName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}

public class WorkshopPriceQuoteResponse
{
    public Guid WorkshopId { get; set; }
    public string WorkshopTitle { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsSplitTier { get; set; }
    public string SplitTierMessage { get; set; } = string.Empty;
    public List<WorkshopPriceQuoteItem> Breakdown { get; set; } = new();
}

public class WorkshopPricingResponse
{
    public Guid WorkshopId { get; set; }

    public string WorkshopTitle { get; set; } = string.Empty;

    public decimal StartingPrice { get; set; }

    public decimal CurrentPrice { get; set; }

    public decimal CurrentPublicPrice { get; set; }

    public decimal StudentPrice { get; set; }

    public decimal FinalAmount { get; set; }

    public int CurrentTier { get; set; }

    public string CurrentTierName { get; set; } = string.Empty;

    public int TicketsSold { get; set; }

    public int TierCapacity { get; set; }

    public int TicketsFilledInTier { get; set; }

    public int TicketsRemainingInTier { get; set; }

    public decimal? NextPrice { get; set; }

    public int ProgressPercentage { get; set; }

    public bool IsStudentEligible { get; set; }

    public int Capacity { get; set; }

    public int BookedSeats { get; set; }

    public int RemainingSeats { get; set; }

    public bool IsFull { get; set; }

    public List<WorkshopPricingTierDto> Tiers { get; set; } = new();
}
