namespace Ethos.Api.Domain.Entities;

public class WorkshopPassType
{
    public Guid Id { get; set; }

    public Guid WorkshopId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    // null indicates Overall Pass (all active sessions included)
    public int? SessionsIncluded { get; set; }

    public int TotalQuantity { get; set; } = 1000;

    public DateTime? SalesStartUtc { get; set; }

    public DateTime? SalesEndUtc { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? WorkshopSessionId { get; set; }

    public Workshop Workshop { get; set; } = null!;

    public WorkshopSession? WorkshopSession { get; set; }

    public ICollection<WorkshopPricingTier> PricingTiers { get; set; } = new List<WorkshopPricingTier>();

    public bool IsSalesClosed(DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        if (SalesStartUtc.HasValue && now < SalesStartUtc.Value) return true;
        if (SalesEndUtc.HasValue && now >= SalesEndUtc.Value) return true;
        return false;
    }

    public bool IsSalesOpen(DateTime? nowUtc = null)
    {
        return !IsSalesClosed(nowUtc);
    }

    public string GetPassCategory()
    {
        if (!SessionsIncluded.HasValue) return "AllAccess";
        if (WorkshopSessionId.HasValue || SessionsIncluded == 1) return "SingleSession";
        return "MultiSessionBundle";
    }
}
