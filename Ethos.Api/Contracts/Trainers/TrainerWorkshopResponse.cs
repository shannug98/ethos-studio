namespace Ethos.Api.Contracts.Trainers;

public class TrainerWorkshopResponse
{
    public Guid Id { get; set; }

    public string? WorkshopReference { get; set; }

    public Guid TrainerProfileId { get; set; }

    public string TrainerName { get; set; } = null!;
    public string? TrainerPhotoUrl { get; set; }
    public string? TrainerDanceStyles { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string DanceStyle { get; set; } = null!;

    public string Level { get; set; } = null!;

    public DateTime WorkshopDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public string Venue { get; set; } = null!;

    public decimal? TrainerProposedPrice { get; set; }

    public decimal? AdminApprovedPrice { get; set; }

    public decimal Price { get; set; }

    public int Capacity { get; set; }

    public int BookedCount { get; set; }

    public int AttendedCount { get; set; }

    public decimal TotalRevenue { get; set; }

    public int EnrolledStudentsCount { get; set; }

    public string Status { get; set; } = null!;
    public string ApprovalStatus { get; set; } = null!;
    public string LifecyclePhase { get; set; } = null!;

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
}
