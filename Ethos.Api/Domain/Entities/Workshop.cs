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

    public TimeSpan? BookingCutoffTime { get; set; }

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
    public string? LocationUrl { get; set; }

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

    public ICollection<WorkshopTrainer> WorkshopTrainers { get; set; }
        = new List<WorkshopTrainer>();

    public ICollection<WorkshopSession> Sessions { get; set; }
        = new List<WorkshopSession>();

    public ICollection<WorkshopPassType> PassTypes { get; set; }
        = new List<WorkshopPassType>();

    public ICollection<WorkshopFeedback> Feedbacks { get; set; }
        = new List<WorkshopFeedback>();

    public DateTime GetBookingCutoffUtc()
    {
        var tzId = string.IsNullOrWhiteSpace(Timezone) ? "Asia/Kolkata" : Timezone;
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }

        var dateUnspecified = DateTime.SpecifyKind(WorkshopDate.Date, DateTimeKind.Unspecified);

        if (BookingCutoffTime.HasValue)
        {
            var cutoffLocal = dateUnspecified + BookingCutoffTime.Value;
            return TimeZoneInfo.ConvertTimeToUtc(cutoffLocal, tz);
        }

        if (StartUtc.HasValue)
        {
            return StartUtc.Value;
        }

        var effectiveStart = StartTime;
        var startLocal = dateUnspecified + effectiveStart;
        return TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
    }

    public bool IsBookingClosed(DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        if (Sessions != null && Sessions.Count > 0)
        {
            return Sessions.All(s => s.IsBookingClosed(now));
        }

        // If an explicit cutoff is set, enforce it strictly
        if (BookingCutoffTime.HasValue)
        {
            return now >= GetBookingCutoffUtc();
        }

        if (EndUtc.HasValue)
        {
            return now >= EndUtc.Value;
        }

        if (EndTime > TimeSpan.Zero)
        {
            var tzId = string.IsNullOrWhiteSpace(Timezone) ? "Asia/Kolkata" : Timezone;
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
            var dateUnspecified = DateTime.SpecifyKind(WorkshopDate.Date, DateTimeKind.Unspecified);
            var endLocal = dateUnspecified + EndTime;
            return now >= TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);
        }

        return now >= GetBookingCutoffUtc();
    }
}
