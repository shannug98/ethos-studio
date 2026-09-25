namespace Ethos.Api.Domain.Entities;

public class WorkshopSession
{
    public Guid Id { get; set; }

    public Guid WorkshopId { get; set; }

    public DateTime SessionDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public Guid TrainerProfileId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Capacity { get; set; } = 30;

    public TimeSpan? BookingCutoffTime { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Workshop Workshop { get; set; } = null!;

    public TrainerProfile TrainerProfile { get; set; } = null!;

    public string? PosterImageUrl { get; set; }

    public ICollection<WorkshopSessionTrainer> SessionTrainers { get; set; }
        = new List<WorkshopSessionTrainer>();

    public ICollection<WorkshopBookingSession> BookingSessions { get; set; }
        = new List<WorkshopBookingSession>();

    public ICollection<WorkshopTicket> Tickets { get; set; }
        = new List<WorkshopTicket>();

    public DateTime GetBookingCutoffUtc(string timezone = "Asia/Kolkata")
    {
        var tzId = string.IsNullOrWhiteSpace(timezone) ? "Asia/Kolkata" : timezone;
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }

        var dateUnspecified = DateTime.SpecifyKind(SessionDate.Date, DateTimeKind.Unspecified);

        // 1. Authoritative session-level cutoff if explicitly set
        if (BookingCutoffTime.HasValue)
        {
            var cutoffLocal = dateUnspecified + BookingCutoffTime.Value;
            return TimeZoneInfo.ConvertTimeToUtc(cutoffLocal, tz);
        }

        // 2. Fallback to workshop-level cutoff if set
        if (Workshop?.BookingCutoffTime != null)
        {
            var cutoffLocal = dateUnspecified + Workshop.BookingCutoffTime.Value;
            return TimeZoneInfo.ConvertTimeToUtc(cutoffLocal, tz);
        }

        // 3. Fallback to session start time
        var startLocal = dateUnspecified + StartTime;
        return TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
    }

    public bool IsBookingClosed(DateTime? nowUtc = null, string timezone = "Asia/Kolkata")
    {
        var now = nowUtc ?? DateTime.UtcNow;

        // If no explicit cutoff time set, keep bookings open until the session ends
        if (!BookingCutoffTime.HasValue && Workshop?.BookingCutoffTime == null)
        {
            var tzId = string.IsNullOrWhiteSpace(timezone) ? "Asia/Kolkata" : timezone;
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }

            var dateUnspecified = DateTime.SpecifyKind(SessionDate.Date, DateTimeKind.Unspecified);
            var effectiveEnd = EndTime > TimeSpan.Zero ? EndTime : StartTime;
            var endLocal = dateUnspecified + effectiveEnd;
            return now >= TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);
        }

        return now >= GetBookingCutoffUtc(timezone);
    }

    public bool IsCompleted(DateTime? nowUtc = null, string timezone = "Asia/Kolkata")
    {
        var tzId = string.IsNullOrWhiteSpace(timezone) ? "Asia/Kolkata" : timezone;
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }

        var dateUnspecified = DateTime.SpecifyKind(SessionDate.Date, DateTimeKind.Unspecified);
        var endLocal = dateUnspecified + EndTime;
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);
        var now = nowUtc ?? DateTime.UtcNow;
        return now >= endUtc;
    }
}
