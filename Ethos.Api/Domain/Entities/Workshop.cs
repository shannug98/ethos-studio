using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class Workshop
{
    public Guid Id { get; set; }

    public Guid? TrainerProfileId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string DanceStyle { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public DateTime WorkshopDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public string Venue { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal? TrainerProposedPrice { get; set; }

    public decimal? AdminApprovedPrice { get; set; }

    public DateTime? PriceApprovedAt { get; set; }

    public Guid? PriceApprovedByUserId { get; set; }

    public int Capacity { get; set; }

    public WorkshopStatus Status { get; set; }

    public string? ImageUrl { get; set; }
    public string? LandscapeImageUrl { get; set; }

    public string? City { get; set; }
    public string? Area { get; set; }
    public string? ShortDescription { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactNumber { get; set; }
    public bool PublicVisibility { get; set; } = true;
    public string RegistrationType { get; set; } = "Standard";
    public string? TermsAndCancellationPolicy { get; set; }

    public string? GooglePlaceId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? VenueAddress { get; set; }

    public string Timezone { get; set; } = "Asia/Kolkata";
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public TrainerProfile? TrainerProfile { get; set; }

    public bool AllowReEntry { get; set; } = true;

    public bool RequireReEntryVerification { get; set; } = false;

    public TimeSpan? ReEntryCooldown { get; set; }

    public ICollection<WorkshopBooking> Bookings { get; set; }
        = new List<WorkshopBooking>();

    public ICollection<WorkshopTicket> Tickets { get; set; }
        = new List<WorkshopTicket>();

    public ICollection<WorkshopPricingTier> PricingTiers { get; set; }
        = new List<WorkshopPricingTier>();

    public ICollection<WorkshopFeedback> Feedbacks { get; set; }
        = new List<WorkshopFeedback>();
}
