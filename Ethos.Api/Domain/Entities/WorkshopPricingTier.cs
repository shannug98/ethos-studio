using System.ComponentModel.DataAnnotations;

namespace Ethos.Api.Domain.Entities;

public class WorkshopPricingTier
{
    public Guid Id { get; set; }

    public Guid WorkshopId { get; set; }

    public int TierNumber { get; set; } // 1, 2, 3, 4

    [MaxLength(100)]
    public string TierName { get; set; } = string.Empty;

    public int MinTickets { get; set; } // 1, 11, 21, 31

    public int? MaxTickets { get; set; } // 10, 20, 30, null for Tier 4 (31+)

    public decimal Price { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Workshop Workshop { get; set; } = null!;
}
