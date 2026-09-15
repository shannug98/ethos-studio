namespace Ethos.Api.Contracts.Admin;

public class AdminWorkshopPricingTierItem
{
    public int TierNumber { get; set; }
    public string TierName { get; set; } = string.Empty;
    public int MinTickets { get; set; }
    public int? MaxTickets { get; set; }
    public decimal Price { get; set; }
}

public class AdminUpdateWorkshopPricingTiersRequest
{
    public List<AdminWorkshopPricingTierItem> Tiers { get; set; } = new();
    public string? Reason { get; set; }
}

public class AdminWorkshopPricingTiersResponse
{
    public Guid WorkshopId { get; set; }
    public string WorkshopTitle { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int ConfirmedTicketsSold { get; set; }
    public List<AdminWorkshopPricingTierItem> Tiers { get; set; } = new();
}
