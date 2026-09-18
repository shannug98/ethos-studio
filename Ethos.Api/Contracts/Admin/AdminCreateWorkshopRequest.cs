using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Admin;

public class AdminCreateWorkshopRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    [StringLength(100)]
    public string DanceStyle { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Level { get; set; } = string.Empty;

    [Required]
    public DateTime WorkshopDate { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [Required]
    [StringLength(200)]
    public string Venue { get; set; } = string.Empty;

    [Range(0, 1000000)]
    public decimal Price { get; set; }

    [Range(1, 10000)]
    public int Capacity { get; set; }

    public Guid? TrainerProfileId { get; set; }

    public string? ImageUrl { get; set; }
    public string? LandscapeImageUrl { get; set; }

    public string? City { get; set; }
    public string? Area { get; set; }
    public string? ShortDescription { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactNumber { get; set; }
    public bool? PublicVisibility { get; set; }
    public string? RegistrationType { get; set; }
    public string? TermsAndCancellationPolicy { get; set; }

    public string? GooglePlaceId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? VenueAddress { get; set; }

    public string? Timezone { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkshopStatus? Status { get; set; }

    public bool AllowReEntry { get; set; } = true;

    public bool RequireReEntryVerification { get; set; } = false;

    public TimeSpan? ReEntryCooldown { get; set; }

    public List<AdminWorkshopPricingTierItem>? PricingTiers { get; set; }
}

public class AdminUpdateWorkshopRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    [StringLength(100)]
    public string DanceStyle { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Level { get; set; } = string.Empty;

    [Required]
    public DateTime WorkshopDate { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [Required]
    [StringLength(200)]
    public string Venue { get; set; } = string.Empty;

    [Range(0, 1000000)]
    public decimal Price { get; set; }

    [Range(1, 10000)]
    public int Capacity { get; set; }

    public Guid? TrainerProfileId { get; set; }

    public string? ImageUrl { get; set; }
    public string? LandscapeImageUrl { get; set; }

    public string? City { get; set; }
    public string? Area { get; set; }
    public string? ShortDescription { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactNumber { get; set; }
    public bool? PublicVisibility { get; set; }
    public string? RegistrationType { get; set; }
    public string? TermsAndCancellationPolicy { get; set; }

    public string? GooglePlaceId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? VenueAddress { get; set; }

    public string? Timezone { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkshopStatus? Status { get; set; }

    public bool AllowReEntry { get; set; } = true;

    public bool RequireReEntryVerification { get; set; } = false;

    public TimeSpan? ReEntryCooldown { get; set; }

    public List<AdminWorkshopPricingTierItem>? PricingTiers { get; set; }
}
