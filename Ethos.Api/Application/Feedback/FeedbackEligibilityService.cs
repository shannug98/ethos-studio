using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Application.Feedback;

public class FeedbackEligibilityService : IFeedbackEligibilityService
{
    private readonly AppDbContext _db;
    private readonly ILogger<FeedbackEligibilityService> _logger;
    private readonly string? _secretKey;

    public FeedbackEligibilityService(
        AppDbContext db,
        ILogger<FeedbackEligibilityService> logger,
        IConfiguration? configuration = null)
    {
        _db = db;
        _logger = logger;
        _secretKey = configuration?["TicketSecurity:SecretKey"];
    }

    public async Task<FeedbackEligibilityEvaluationResult> EvaluateWorkshopBookingsAsync(
        Guid workshopId,
        DateTime? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var result = new FeedbackEligibilityEvaluationResult
        {
            WorkshopId = workshopId,
            WorkshopsProcessed = 1
        };

        var workshop = await _db.Workshops
            .Include(w => w.Sessions.OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime))
            .Include(w => w.PassTypes)
            .Include(w => w.FeedbackSetting)
                .ThenInclude(fs => fs!.ActiveVersion)
            .Include(w => w.Bookings)
                .ThenInclude(b => b.BookingSessions)
                    .ThenInclude(bs => bs.WorkshopSession)
            .Include(w => w.Bookings)
                .ThenInclude(b => b.Tickets)
                    .ThenInclude(t => t.Attendance)
            .Include(w => w.Bookings)
                .ThenInclude(b => b.StudentProfile)
                    .ThenInclude(sp => sp.User)
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
        {
            result.Logs.Add($"Workshop {workshopId} not found.");
            return result;
        }

        // 1. Ensure Feedback Setting exists and check if feedback is enabled
        var setting = workshop.FeedbackSetting;
        if (setting == null)
        {
            setting = new WorkshopFeedbackSetting
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                IsFeedbackEnabled = true,
                IsLocked = false,
                CreatedAtUtc = now
            };

            var defaultVersion = new FeedbackFormVersion
            {
                Id = Guid.NewGuid(),
                WorkshopFeedbackSettingId = setting.Id,
                VersionNumber = 1,
                IsFrozen = false,
                CreatedAtUtc = now
            };

            setting.FormVersions.Add(defaultVersion);

            _db.WorkshopFeedbackSettings.Add(setting);
            _db.FeedbackFormVersions.Add(defaultVersion);
            workshop.FeedbackSetting = setting;

            await _db.SaveChangesAsync(cancellationToken);

            setting.ActiveVersionId = defaultVersion.Id;
            setting.ActiveVersion = defaultVersion;
        }

        if (!setting.IsFeedbackEnabled)
        {
            result.Logs.Add($"Feedback is disabled for Workshop {workshopId}.");
            return result;
        }

        // 2. Auto-lock setting and freeze active version if ConfigCutoffUtc has arrived
        var earliestSessionStartUtc = GetEarliestStartUtc(workshop);
        var effectiveCutoffUtc = setting.ConfigCutoffUtc ?? earliestSessionStartUtc.AddMinutes(-15);

        if (now >= effectiveCutoffUtc && !setting.IsLocked)
        {
            setting.IsLocked = true;
            setting.LockedAtUtc = now;
            if (setting.ActiveVersion != null && !setting.ActiveVersion.IsFrozen)
            {
                setting.ActiveVersion.IsFrozen = true;
                setting.ActiveVersion.FrozenAtUtc = now;
            }
            result.Logs.Add($"Workshop feedback settings locked and active form version frozen at {now:u}.");
        }

        var versionNumber = setting.ActiveVersion?.VersionNumber ?? 1;

        // Preload existing refunds and submitted feedbacks for fast in-memory lookup
        var bookingIds = workshop.Bookings.Select(b => b.Id).ToList();

        var refundedBookingIds = await _db.PaymentRefunds
            .Where(r => bookingIds.Contains(r.BookingId) &&
                        (r.Status == RefundStatus.Processed ||
                         r.Status == RefundStatus.Requested ||
                         r.Status == RefundStatus.Processing))
            .Select(r => r.BookingId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var existingSubmittedFeedbackBookingIds = await _db.WorkshopFeedbacks
            .Where(f => f.WorkshopBookingId.HasValue &&
                        bookingIds.Contains(f.WorkshopBookingId.Value) &&
                        f.IsValid)
            .Select(f => f.WorkshopBookingId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var existingTokens = await _db.WorkshopFeedbackTokens
            .Where(t => bookingIds.Contains(t.WorkshopBookingId) && t.UsedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        var existingNotifications = await _db.WhatsAppNotifications
            .Where(n => bookingIds.Contains(n.BookingId))
            .ToListAsync(cancellationToken);

        foreach (var booking in workshop.Bookings)
        {
            result.TotalBookingsEvaluated++;

            // A. Exclusion Rules
            if (booking.Status == WorkshopBookingStatus.Cancelled || booking.CancelledAt.HasValue)
            {
                result.ExcludedCount++;
                continue;
            }

            if (booking.Status == WorkshopBookingStatus.PendingPayment)
            {
                result.ExcludedCount++;
                continue;
            }

            if (refundedBookingIds.Contains(booking.Id))
            {
                result.ExcludedCount++;
                continue;
            }

            if (booking.Tickets.Count > 0 && booking.Tickets.All(t => t.Status == TicketStatus.Cancelled || t.Status == TicketStatus.Refunded))
            {
                result.ExcludedCount++;
                continue;
            }

            if (IsTestBooking(booking))
            {
                result.ExcludedCount++;
                continue;
            }

            // B. Determine Final Applicable Session End UTC
            var finalApplicableSessionEndUtc = DetermineFinalApplicableSessionEndUtc(workshop, booking);

            if (now < finalApplicableSessionEndUtc)
            {
                // Booking's applicable sessions have not finished yet
                result.PendingNotEndedCount++;
                continue;
            }

            // Skip if feedback was already submitted
            if (existingSubmittedFeedbackBookingIds.Contains(booking.Id))
            {
                continue;
            }

            // C. Authoritative Attendance Classification
            var hasCheckIn = booking.Tickets.Any(t =>
                t.Attendance != null ||
                t.CheckedInAt.HasValue ||
                t.Status == TicketStatus.Issued && t.CheckedInAt.HasValue);

            var audienceType = hasCheckIn ? FeedbackAudienceType.Attended : FeedbackAudienceType.NoShow;
            if (audienceType == FeedbackAudienceType.Attended)
            {
                result.EligibleAttendedCount++;
            }
            else
            {
                result.EligibleNoShowCount++;
            }

            // D. Generate or reuse WorkshopFeedbackToken
            var tokenEntity = existingTokens.FirstOrDefault(t => t.WorkshopBookingId == booking.Id);
            if (tokenEntity == null)
            {
                var rawToken = FeedbackTokenHelper.DeriveRawToken(booking.Id, _secretKey);
                var tokenHash = FeedbackTokenHelper.HashToken(rawToken);

                tokenEntity = new WorkshopFeedbackToken
                {
                    Id = Guid.NewGuid(),
                    WorkshopBookingId = booking.Id,
                    TokenHash = tokenHash,
                    AudienceType = audienceType,
                    FeedbackFormVersionId = setting.ActiveVersionId,
                    ExpiresAt = now.AddDays(7),
                    UsedAt = null,
                    CreatedAt = now
                };

                _db.WorkshopFeedbackTokens.Add(tokenEntity);
                existingTokens.Add(tokenEntity);
                result.TokensGeneratedCount++;
            }

            // E. Queue WhatsApp Outbox Notification (Idempotent)
            var audienceKey = audienceType == FeedbackAudienceType.Attended ? "attended" : "noshow";
            var idempotencyKey = $"feedback:{workshop.Id}:{booking.Id}:{audienceKey}:v{versionNumber}";

            var notificationAlreadyExists = existingNotifications.Any(n => n.IdempotencyKey == idempotencyKey);
            if (!notificationAlreadyExists)
            {
                var recipientPhone = booking.GuestPhone
                                     ?? booking.StudentProfile?.User?.Phone
                                     ?? booking.Tickets.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.AttendeePhone))?.AttendeePhone;

                if (!string.IsNullOrWhiteSpace(recipientPhone))
                {
                    var notificationType = audienceType == FeedbackAudienceType.Attended
                        ? WhatsAppNotificationType.FeedbackAttended
                        : WhatsAppNotificationType.FeedbackNoShow;

                    var primaryTicket = booking.Tickets.FirstOrDefault(t => t.IsPrimaryAttendee)
                                        ?? booking.Tickets.FirstOrDefault();

                    var notification = new WhatsAppNotification
                    {
                        Id = Guid.NewGuid(),
                        BookingId = booking.Id,
                        WorkshopTicketId = primaryTicket?.Id,
                        NotificationType = notificationType,
                        RecipientPhone = recipientPhone.Trim(),
                        IdempotencyKey = idempotencyKey,
                        Status = WhatsAppNotificationStatus.Pending,
                        CreatedAt = now
                    };

                    _db.WhatsAppNotifications.Add(notification);
                    existingNotifications.Add(notification);
                    result.NotificationsQueuedCount++;
                }
                else
                {
                    result.Logs.Add($"Booking {booking.Id} has no recipient phone; skipped notification outbox generation.");
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<FeedbackEligibilityEvaluationResult> ProcessCompletedWorkshopsAsync(
        DateTime? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        var now = nowUtc ?? DateTime.UtcNow;

        // Find workshops that are active/completed and whose date has occurred
        var candidateWorkshopIds = await _db.Workshops
            .Where(w => w.Status == WorkshopStatus.Published || w.Status == WorkshopStatus.Completed)
            .Where(w => w.WorkshopDate <= now.Date.AddDays(1))
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);

        var combinedResult = new FeedbackEligibilityEvaluationResult
        {
            WorkshopsProcessed = 0
        };

        foreach (var workshopId in candidateWorkshopIds)
        {
            var r = await EvaluateWorkshopBookingsAsync(workshopId, now, cancellationToken);
            combinedResult.WorkshopsProcessed++;
            combinedResult.TotalBookingsEvaluated += r.TotalBookingsEvaluated;
            combinedResult.EligibleAttendedCount += r.EligibleAttendedCount;
            combinedResult.EligibleNoShowCount += r.EligibleNoShowCount;
            combinedResult.ExcludedCount += r.ExcludedCount;
            combinedResult.PendingNotEndedCount += r.PendingNotEndedCount;
            combinedResult.TokensGeneratedCount += r.TokensGeneratedCount;
            combinedResult.NotificationsQueuedCount += r.NotificationsQueuedCount;
            combinedResult.Logs.AddRange(r.Logs);
        }

        return combinedResult;
    }

    private static bool IsTestBooking(WorkshopBooking booking)
    {
        var email = booking.GuestEmail ?? booking.StudentProfile?.User?.Email;
        if (!string.IsNullOrWhiteSpace(email))
        {
            if (email.EndsWith("@test.com", StringComparison.OrdinalIgnoreCase) ||
                email.EndsWith("@example.com", StringComparison.OrdinalIgnoreCase) ||
                email.StartsWith("test-", StringComparison.OrdinalIgnoreCase) ||
                email.StartsWith("mock-", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static DateTime GetEarliestStartUtc(Workshop workshop)
    {
        var tzId = string.IsNullOrWhiteSpace(workshop.Timezone) ? "Asia/Kolkata" : workshop.Timezone;
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }

        if (workshop.Sessions.Count > 0)
        {
            var earliest = workshop.Sessions.Min(s =>
            {
                var dateUnspecified = DateTime.SpecifyKind(s.SessionDate.Date, DateTimeKind.Unspecified);
                var startLocal = dateUnspecified + s.StartTime;
                return TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
            });
            return earliest;
        }

        if (workshop.StartUtc.HasValue)
        {
            return workshop.StartUtc.Value;
        }

        var workshopDateUnspecified = DateTime.SpecifyKind(workshop.WorkshopDate.Date, DateTimeKind.Unspecified);
        var workshopStartLocal = workshopDateUnspecified + workshop.StartTime;
        return TimeZoneInfo.ConvertTimeToUtc(workshopStartLocal, tz);
    }

    public static DateTime DetermineFinalApplicableSessionEndUtc(Workshop workshop, WorkshopBooking booking)
    {
        var tzId = string.IsNullOrWhiteSpace(workshop.Timezone) ? "Asia/Kolkata" : workshop.Timezone;
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }

        // 1. Check if booking has explicit BookingSessions
        var activeBookingSessions = booking.BookingSessions
            .Where(bs => bs.Status == WorkshopBookingSessionStatus.Booked && bs.WorkshopSession != null)
            .Select(bs => bs.WorkshopSession)
            .ToList();

        if (activeBookingSessions.Count > 0)
        {
            return activeBookingSessions.Max(s => s.GetSessionEndUtc(workshop.Timezone));
        }

        // 2. Check WorkshopPassType rules
        if (booking.WorkshopPassType != null)
        {
            var pass = booking.WorkshopPassType;
            if (pass.SessionsIncluded == 1 && pass.WorkshopSessionId.HasValue)
            {
                var targetSession = workshop.Sessions.FirstOrDefault(s => s.Id == pass.WorkshopSessionId.Value);
                if (targetSession != null)
                {
                    return targetSession.GetSessionEndUtc(workshop.Timezone);
                }
            }

            // All Workshops / Overall pass: final active workshop session
            if (workshop.Sessions.Count > 0)
            {
                var activeSessions = workshop.Sessions.Where(s => s.IsActive).ToList();
                if (activeSessions.Count > 0)
                {
                    return activeSessions.Max(s => s.GetSessionEndUtc(workshop.Timezone));
                }
            }
        }

        // 3. Fallback to all active workshop sessions
        if (workshop.Sessions.Count > 0)
        {
            var activeSessions = workshop.Sessions.Where(s => s.IsActive).ToList();
            if (activeSessions.Count > 0)
            {
                return activeSessions.Max(s => s.GetSessionEndUtc(workshop.Timezone));
            }
        }

        // 4. Standalone workshop fallback (WorkshopDate + EndTime)
        if (workshop.EndUtc.HasValue)
        {
            return workshop.EndUtc.Value;
        }

        var dateUnspecified = DateTime.SpecifyKind(workshop.WorkshopDate.Date, DateTimeKind.Unspecified);
        var effectiveEnd = workshop.EndTime > TimeSpan.Zero ? workshop.EndTime : workshop.StartTime;
        var endLocal = dateUnspecified + effectiveEnd;
        return TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);
    }
}
