using System.ComponentModel.DataAnnotations;
using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Workshops;

public class WorkshopTrainerDto
{
    public Guid TrainerProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string? DanceStyles { get; set; }
    public int DisplayOrder { get; set; }
}

public class WorkshopSessionDto
{
    public Guid Id { get; set; }
    public Guid WorkshopId { get; set; }
    public DateTime SessionDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public Guid TrainerProfileId { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public string? TrainerPhotoUrl { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Capacity { get; set; }
    public int BookedSeats { get; set; }
    public int RemainingSeats { get; set; }
    public bool IsFull { get; set; }
    public TimeSpan? BookingCutoffTime { get; set; }
    public DateTime? BookingCutoffUtc { get; set; }
    public bool IsBookingClosed { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string? PosterImageUrl { get; set; }
    public List<WorkshopTrainerDto> Trainers { get; set; } = new();
    public string AvailabilityLabel { get; set; } = "Available";
}

public class WorkshopPassTypeDto
{
    public Guid Id { get; set; }
    public Guid WorkshopId { get; set; }
    public Guid? WorkshopSessionId { get; set; }
    public string? SessionTitle { get; set; }
    public DateTime? SessionDate { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int? SessionsIncluded { get; set; }
    public bool IsOverallPass => !SessionsIncluded.HasValue;
    public int TotalQuantity { get; set; } = 1000;
    public int RemainingQuantity { get; set; } = 1000;
    public DateTime? SalesStartUtc { get; set; }
    public DateTime? SalesEndUtc { get; set; }
    public bool IsSalesClosed { get; set; }
    public bool IsSalesOpen => !IsSalesClosed && RemainingQuantity > 0;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    // Pricing & Tier properties
    public decimal CurrentPrice { get; set; }
    public decimal? NextTierPrice { get; set; }
    public int CurrentTierNumber { get; set; }
    public string? CurrentTierName { get; set; }
    public int TicketsRemainingInCurrentTier { get; set; }
    public List<WorkshopPricingTierDto> PricingTiers { get; set; } = new();

    // Properties for contract alignment
    public int AvailableSeats => RemainingQuantity;
    public bool IsAvailable => !IsSalesClosed && RemainingQuantity > 0;
    public bool IsFull => RemainingQuantity <= 0;
    public string AvailabilityLabel { get; set; } = "Available";
    public int TotalCapacity => TotalQuantity;
    public int AllocatedSeats => TotalQuantity - RemainingQuantity;
    public int? SessionCount => SessionsIncluded;
    public string PassType => Name;
}

public class WorkshopBookingSessionDto
{
    public Guid Id { get; set; }
    public Guid WorkshopBookingId { get; set; }
    public Guid WorkshopSessionId { get; set; }
    public Guid? WorkshopTicketId { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
    public DateTime SessionDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public WorkshopBookingSessionStatus Status { get; set; }
    public string? TicketNumber { get; set; }
    public Guid? OriginalSessionId { get; set; }
    public DateTime? ReplacedAt { get; set; }
    public bool CutoffOverrideUsed { get; set; }
    public string? OverrideReason { get; set; }
}

public class AdminWorkshopSessionItem
{
    public Guid? Id { get; set; }
    [Required]
    public DateTime SessionDate { get; set; }
    [Required]
    public TimeSpan StartTime { get; set; }
    [Required]
    public TimeSpan EndTime { get; set; }
    public Guid? TrainerProfileId { get; set; }
    public List<Guid>? TrainerProfileIds { get; set; }
    public string? PosterImageUrl { get; set; }
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Range(1, 10000)]
    public int Capacity { get; set; } = 30;
    public TimeSpan? BookingCutoffTime { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}

public class AdminWorkshopPassTypeItem
{
    public Guid? Id { get; set; }
    public Guid? WorkshopSessionId { get; set; }
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Range(0, 1000000)]
    public decimal Price { get; set; }
    public int? SessionsIncluded { get; set; }
    [Range(1, 100000)]
    public int TotalQuantity { get; set; } = 1000;
    public DateTime? SalesStartUtc { get; set; }
    public DateTime? SalesEndUtc { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public List<Ethos.Api.Contracts.Admin.AdminWorkshopPricingTierItem>? PricingTiers { get; set; }
}
