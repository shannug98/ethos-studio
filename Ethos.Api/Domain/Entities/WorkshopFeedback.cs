namespace Ethos.Api.Domain.Entities;

public class WorkshopFeedback
{
    public Guid Id { get; set; }

    public Guid? WorkshopBookingId { get; set; }

    public Guid WorkshopId { get; set; }

    public Guid? StudentProfileId { get; set; }

    public int Rating { get; set; }

    public int? TeachingRating { get; set; }

    public int? EnergyRating { get; set; }

    public int? ContentRating { get; set; }

    public int? ExplanationClarity { get; set; }

    public int? DemonstrationRating { get; set; }

    public int? InteractionRating { get; set; }

    public int? DurationRating { get; set; }

    public int? OrganizationRating { get; set; }

    public int? VenueRating { get; set; }

    public int? ValueForMoney { get; set; }

    public string? Comment { get; set; }

    public string? Improvements { get; set; }

    public bool WouldRecommend { get; set; }

    public bool WouldAttendTrainerAgain { get; set; }

    public DateTime SubmittedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsValid { get; set; } = true;

    public string? InvalidationReason { get; set; }

    public DateTime? InvalidatedAt { get; set; }

    public Workshop Workshop { get; set; } = null!;

    public StudentProfile? StudentProfile { get; set; }

    public WorkshopBooking? WorkshopBooking { get; set; }
}
