using System.Security.Cryptography;
using System.Text;
using Ethos.Api.Contracts.Feedback;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Exceptions;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Feedback;

public class GuestWorkshopFeedbackService : IGuestWorkshopFeedbackService
{
    private readonly AppDbContext _db;

    public GuestWorkshopFeedbackService(AppDbContext db)
    {
        _db = db;
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token.Trim());
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<GenerateFeedbackTokenResponse> GenerateTokenForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await _db.WorkshopBookings
            .Include(b => b.Workshop)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
            throw new BusinessRuleException("BOOKING_NOT_ELIGIBLE", "Workshop booking not found.", StatusCodes.Status404NotFound);

        if (booking.Status is WorkshopBookingStatus.Cancelled or WorkshopBookingStatus.PendingPayment)
            throw new BusinessRuleException("BOOKING_NOT_ELIGIBLE", "Cannot generate feedback token for a cancelled or unpaid booking.");

        if (booking.Status != WorkshopBookingStatus.Attended)
            throw new BusinessRuleException("ATTENDANCE_REQUIRED", "Feedback token can only be generated after the attendee has attended the workshop.");

        var now = DateTime.UtcNow;
        var workshopEndUtc = DateTime.SpecifyKind(booking.Workshop.WorkshopDate.Date.Add(booking.Workshop.EndTime), DateTimeKind.Utc);
        if (workshopEndUtc > now)
        {
            throw new BusinessRuleException("WORKSHOP_NOT_FINISHED", "Feedback token can only be generated after the workshop has finished.");
        }

        // Check if feedback already submitted
        var existingFeedback = await _db.WorkshopFeedbacks
            .AnyAsync(f => f.WorkshopBookingId == bookingId && f.IsValid, cancellationToken);

        if (existingFeedback)
            throw new BusinessRuleException("FEEDBACK_ALREADY_SUBMITTED", "Feedback has already been submitted for this booking.", StatusCodes.Status409Conflict);

        // Check if an existing unused, unexpired token exists
        var existingToken = await _db.WorkshopFeedbackTokens
            .Where(t => t.WorkshopBookingId == bookingId && t.UsedAt == null && t.ExpiresAt > now)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Generate a new 64-character random token
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenHash = HashToken(rawToken);
        var expiresAt = now.AddDays(7);

        var tokenEntity = new WorkshopFeedbackToken
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = bookingId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            UsedAt = null,
            CreatedAt = now
        };

        _db.WorkshopFeedbackTokens.Add(tokenEntity);
        await _db.SaveChangesAsync(cancellationToken);

        return new GenerateFeedbackTokenResponse
        {
            BookingId = bookingId,
            Token = rawToken,
            FeedbackUrl = $"/feedback/workshop/{rawToken}",
            ExpiresAt = expiresAt
        };
    }

    public async Task<GuestWorkshopFeedbackDetailsResponse> GetFeedbackDetailsByTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new GuestWorkshopFeedbackDetailsResponse
            {
                IsEligible = false,
                IneligibilityReason = "Feedback token is missing or invalid."
            };
        }

        var tokenHash = HashToken(token);
        var tokenEntity = await _db.WorkshopFeedbackTokens
            .Include(t => t.WorkshopBooking)
                .ThenInclude(b => b.Workshop)
                    .ThenInclude(w => w.TrainerProfile)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (tokenEntity == null)
        {
            return new GuestWorkshopFeedbackDetailsResponse
            {
                IsEligible = false,
                IneligibilityReason = "Feedback token is invalid or does not exist."
            };
        }

        var now = DateTime.UtcNow;
        if (tokenEntity.UsedAt != null)
        {
            return new GuestWorkshopFeedbackDetailsResponse
            {
                BookingId = tokenEntity.WorkshopBookingId,
                BookingReference = tokenEntity.WorkshopBookingId.ToString()[..8].ToUpperInvariant(),
                WorkshopTitle = tokenEntity.WorkshopBooking?.Workshop?.Title ?? "Workshop",
                TrainerName = tokenEntity.WorkshopBooking?.Workshop?.TrainerProfile?.FullName ?? "Instructor",
                AlreadySubmitted = true,
                IsEligible = false,
                IneligibilityReason = "Feedback has already been submitted for this session. Thank you!"
            };
        }

        if (tokenEntity.ExpiresAt < now)
        {
            return new GuestWorkshopFeedbackDetailsResponse
            {
                IsEligible = false,
                IneligibilityReason = "This feedback link has expired."
            };
        }

        var booking = tokenEntity.WorkshopBooking;
        if (booking == null || booking.Status is WorkshopBookingStatus.Cancelled or WorkshopBookingStatus.PendingPayment)
        {
            return new GuestWorkshopFeedbackDetailsResponse
            {
                IsEligible = false,
                IneligibilityReason = "This booking was cancelled or unpaid and is not eligible for feedback."
            };
        }

        if (booking.Status != WorkshopBookingStatus.Attended)
        {
            return new GuestWorkshopFeedbackDetailsResponse
            {
                BookingId = booking.Id,
                BookingReference = booking.Id.ToString()[..8].ToUpperInvariant(),
                WorkshopTitle = booking.Workshop?.Title ?? "Workshop",
                TrainerName = booking.Workshop?.TrainerProfile?.FullName ?? "Instructor",
                IsEligible = false,
                IneligibilityReason = "Feedback is available only after the attendee has attended the workshop."
            };
        }

        var workshopEndUtc = DateTime.SpecifyKind(booking.Workshop.WorkshopDate.Date.Add(booking.Workshop.EndTime), DateTimeKind.Utc);
        if (workshopEndUtc > now)
        {
            return new GuestWorkshopFeedbackDetailsResponse
            {
                BookingId = booking.Id,
                BookingReference = booking.Id.ToString()[..8].ToUpperInvariant(),
                WorkshopTitle = booking.Workshop?.Title ?? "Workshop",
                TrainerName = booking.Workshop?.TrainerProfile?.FullName ?? "Instructor",
                IsEligible = false,
                IneligibilityReason = "Feedback becomes available after the workshop has finished."
            };
        }

        // Check if already submitted via another path
        var alreadySubmitted = await _db.WorkshopFeedbacks
            .AnyAsync(f => f.WorkshopBookingId == booking.Id && f.IsValid, cancellationToken);

        if (alreadySubmitted)
        {
            return new GuestWorkshopFeedbackDetailsResponse
            {
                BookingId = booking.Id,
                BookingReference = booking.Id.ToString()[..8].ToUpperInvariant(),
                WorkshopTitle = booking.Workshop?.Title ?? "Workshop",
                TrainerName = booking.Workshop?.TrainerProfile?.FullName ?? "Instructor",
                AlreadySubmitted = true,
                IsEligible = false,
                IneligibilityReason = "Feedback has already been recorded for this booking."
            };
        }

        return new GuestWorkshopFeedbackDetailsResponse
        {
            BookingId = booking.Id,
            BookingReference = booking.Id.ToString()[..8].ToUpperInvariant(),
            WorkshopTitle = booking.Workshop.Title,
            TrainerName = booking.Workshop.TrainerProfile?.FullName ?? "Staff Trainer",
            WorkshopDate = booking.Workshop.WorkshopDate,
            StartTime = booking.Workshop.StartTime,
            EndTime = booking.Workshop.EndTime,
            DanceStyle = booking.Workshop.DanceStyle,
            Venue = booking.Workshop.Venue,
            AlreadySubmitted = false,
            IsEligible = true
        };
    }

    public async Task<bool> SubmitGuestFeedbackAsync(
        SubmitGuestWorkshopFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new BusinessRuleException("FEEDBACK_TOKEN_INVALID", "Feedback token is required.");

        if (request.Rating < 1 || request.Rating > 5)
            throw new BusinessRuleException("VALIDATION_FAILED", "Rating must be between 1 and 5 stars.");

        if (request.Comment?.Length > 2000)
            throw new BusinessRuleException("VALIDATION_FAILED", "Comment cannot exceed 2000 characters.");

        var tokenHash = HashToken(request.Token);
        var tokenEntity = await _db.WorkshopFeedbackTokens
            .Include(t => t.WorkshopBooking)
                .ThenInclude(b => b.Workshop)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (tokenEntity == null)
            throw new BusinessRuleException("FEEDBACK_TOKEN_INVALID", "Invalid feedback token.", StatusCodes.Status404NotFound);

        if (tokenEntity.UsedAt != null)
            throw new BusinessRuleException("FEEDBACK_ALREADY_USED", "Feedback has already been submitted using this link.", StatusCodes.Status409Conflict);

        if (tokenEntity.ExpiresAt < DateTime.UtcNow)
            throw new BusinessRuleException("FEEDBACK_TOKEN_EXPIRED", "This feedback link has expired.");

        var booking = tokenEntity.WorkshopBooking;
        if (booking == null || booking.Status is WorkshopBookingStatus.Cancelled or WorkshopBookingStatus.PendingPayment)
            throw new BusinessRuleException("BOOKING_NOT_ELIGIBLE", "Booking is cancelled, unpaid, or invalid.");

        if (booking.Status != WorkshopBookingStatus.Attended)
            throw new BusinessRuleException("ATTENDANCE_REQUIRED", "Feedback is available only after the attendee has attended the workshop.");

        var nowUtc = DateTime.UtcNow;
        var workshopEndUtc = DateTime.SpecifyKind(booking.Workshop.WorkshopDate.Date.Add(booking.Workshop.EndTime), DateTimeKind.Utc);
        if (workshopEndUtc > nowUtc)
            throw new BusinessRuleException("WORKSHOP_NOT_FINISHED", "Feedback becomes available after the workshop has finished.");

        // Check duplicate
        var alreadyExists = await _db.WorkshopFeedbacks
            .AnyAsync(f => f.WorkshopBookingId == booking.Id && f.IsValid, cancellationToken);

        if (alreadyExists)
            throw new BusinessRuleException("FEEDBACK_ALREADY_SUBMITTED", "Feedback has already been submitted for this booking.", StatusCodes.Status409Conflict);

        var feedback = new WorkshopFeedback
        {
            Id = Guid.NewGuid(),
            WorkshopId = booking.WorkshopId,
            WorkshopBookingId = booking.Id,
            StudentProfileId = booking.StudentProfileId == Guid.Empty ? null : booking.StudentProfileId,
            Rating = request.Rating,
            Comment = request.Comment?.Trim(),
            WouldRecommend = request.Rating >= 4,
            WouldAttendTrainerAgain = request.Rating >= 4,
            SubmittedAt = DateTime.UtcNow,
            IsValid = true
        };

        _db.WorkshopFeedbacks.Add(feedback);
        tokenEntity.UsedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TrainerAggregateFeedbackDto> GetTrainerAggregateFeedbackAsync(
        Guid trainerProfileId,
        CancellationToken cancellationToken = default)
    {
        // Enforce trainer privacy: only aggregate ratings and distributions, NO attendee metadata!
        // ONLY include valid feedbacks from confirmed ATTENDED bookings!
        var feedbacks = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Where(f => f.Workshop.TrainerProfileId == trainerProfileId &&
                        f.IsValid &&
                        f.WorkshopBooking != null &&
                        f.WorkshopBooking.Status == WorkshopBookingStatus.Attended)
            .Select(f => f.Rating)
            .ToListAsync(cancellationToken);

        if (feedbacks.Count == 0)
        {
            return new TrainerAggregateFeedbackDto
            {
                AverageRating = 0m,
                TotalReviews = 0,
                FiveStars = 0,
                FourStars = 0,
                ThreeStars = 0,
                TwoStars = 0,
                OneStar = 0
            };
        }

        var total = feedbacks.Count;
        var avg = Math.Round((decimal)feedbacks.Average(), 2);
        var five = feedbacks.Count(r => r == 5);
        var four = feedbacks.Count(r => r == 4);
        var three = feedbacks.Count(r => r == 3);
        var two = feedbacks.Count(r => r == 2);
        var one = feedbacks.Count(r => r == 1);

        return new TrainerAggregateFeedbackDto
        {
            AverageRating = avg,
            TotalReviews = total,
            FiveStars = five,
            FourStars = four,
            ThreeStars = three,
            TwoStars = two,
            OneStar = one
        };
    }

    public async Task<IReadOnlyList<AdminTrainerFeedbackDto>> GetAdminTrainerFeedbackListAsync(
        Guid trainerProfileId,
        CancellationToken cancellationToken = default)
    {
        // Full transparency for Admin: includes written feedback, workshop references, and booking codes
        // ONLY include valid feedbacks from confirmed ATTENDED bookings!
        var items = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Where(f => f.Workshop.TrainerProfileId == trainerProfileId &&
                        f.IsValid &&
                        f.WorkshopBooking != null &&
                        f.WorkshopBooking.Status == WorkshopBookingStatus.Attended)
            .Include(f => f.Workshop)
            .OrderByDescending(f => f.SubmittedAt)
            .Select(f => new AdminTrainerFeedbackDto
            {
                FeedbackId = f.Id,
                WorkshopId = f.WorkshopId,
                WorkshopTitle = f.Workshop.Title,
                WorkshopDate = f.Workshop.WorkshopDate,
                Rating = f.Rating,
                Comment = f.Comment,
                BookingReference = f.WorkshopBookingId.HasValue ? f.WorkshopBookingId.Value.ToString().Substring(0, 8).ToUpper() : "DIRECT",
                SubmittedAt = f.SubmittedAt
            })
            .ToListAsync(cancellationToken);

        return items;
    }
}
