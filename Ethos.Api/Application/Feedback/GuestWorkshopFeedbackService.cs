using System.Text.Json;
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

        string tokenHash;
        try
        {
            tokenHash = FeedbackTokenHelper.HashToken(token);
        }
        catch
        {
            return new GuestWorkshopFeedbackDetailsResponse
            {
                IsEligible = false,
                IneligibilityReason = "Feedback token format is invalid."
            };
        }

        var tokenEntity = await _db.WorkshopFeedbackTokens
            .Include(t => t.WorkshopBooking)
                .ThenInclude(b => b.Workshop)
                    .ThenInclude(w => w.TrainerProfile)
            .Include(t => t.FeedbackFormVersion)
                .ThenInclude(v => v!.Questions.OrderBy(q => q.SortOrder))
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (tokenEntity == null)
        {
            return new GuestWorkshopFeedbackDetailsResponse
            {
                IsEligible = false,
                IneligibilityReason = "Feedback token is invalid or does not exist."
            };
        }

        var booking = tokenEntity.WorkshopBooking;
        var workshop = booking?.Workshop;

        if (tokenEntity.UsedAt != null)
        {
            return new GuestWorkshopFeedbackDetailsResponse
            {
                BookingId = tokenEntity.WorkshopBookingId,
                BookingReference = tokenEntity.WorkshopBookingId.ToString()[..8].ToUpperInvariant(),
                WorkshopTitle = workshop?.Title ?? "Workshop",
                TrainerName = workshop?.TrainerProfile?.FullName ?? "Instructor",
                AudienceType = tokenEntity.AudienceType,
                AlreadySubmitted = true,
                IsEligible = false,
                IneligibilityReason = "Feedback has already been submitted for this session. Thank you!"
            };
        }

        if (tokenEntity.ExpiresAt < DateTime.UtcNow)
        {
            return new GuestWorkshopFeedbackDetailsResponse
            {
                BookingId = tokenEntity.WorkshopBookingId,
                BookingReference = tokenEntity.WorkshopBookingId.ToString()[..8].ToUpperInvariant(),
                WorkshopTitle = workshop?.Title ?? "Workshop",
                TrainerName = workshop?.TrainerProfile?.FullName ?? "Instructor",
                AudienceType = tokenEntity.AudienceType,
                IsEligible = false,
                IneligibilityReason = "This feedback link has expired."
            };
        }

        if (booking == null || booking.Status is WorkshopBookingStatus.Cancelled or WorkshopBookingStatus.PendingPayment)
        {
            return new GuestWorkshopFeedbackDetailsResponse
            {
                BookingId = tokenEntity.WorkshopBookingId,
                BookingReference = tokenEntity.WorkshopBookingId.ToString()[..8].ToUpperInvariant(),
                WorkshopTitle = workshop?.Title ?? "Workshop",
                TrainerName = workshop?.TrainerProfile?.FullName ?? "Instructor",
                AudienceType = tokenEntity.AudienceType,
                IsEligible = false,
                IneligibilityReason = "This booking was cancelled or unpaid and is not eligible for feedback."
            };
        }

        // Check if feedback already recorded for this booking
        var alreadySubmitted = await _db.WorkshopFeedbacks
            .AnyAsync(f => (f.WorkshopBookingId == booking.Id || f.WorkshopFeedbackTokenId == tokenEntity.Id) && f.IsValid, cancellationToken);

        if (alreadySubmitted)
        {
            return new GuestWorkshopFeedbackDetailsResponse
            {
                BookingId = booking.Id,
                BookingReference = booking.Id.ToString()[..8].ToUpperInvariant(),
                WorkshopTitle = workshop?.Title ?? "Workshop",
                TrainerName = workshop?.TrainerProfile?.FullName ?? "Instructor",
                AudienceType = tokenEntity.AudienceType,
                AlreadySubmitted = true,
                IsEligible = false,
                IneligibilityReason = "Feedback has already been recorded for this booking."
            };
        }

        // Resolve active or snapshot form version
        var formVersion = tokenEntity.FeedbackFormVersion;
        if (formVersion == null && workshop != null)
        {
            var setting = await _db.WorkshopFeedbackSettings
                .Include(s => s.ActiveVersion)
                    .ThenInclude(v => v!.Questions.OrderBy(q => q.SortOrder))
                .FirstOrDefaultAsync(s => s.WorkshopId == workshop.Id, cancellationToken);

            formVersion = setting?.ActiveVersion;
        }

        var audience = tokenEntity.AudienceType;
        var questionDtos = new List<FeedbackQuestionDto>();

        if (formVersion?.Questions != null)
        {
            foreach (var q in formVersion.Questions.OrderBy(x => x.SortOrder))
            {
                if (q.TargetAudience != audience && q.TargetAudience != FeedbackAudienceType.Both)
                {
                    continue;
                }

                var choices = new List<string>();
                if (q.QuestionType == FeedbackQuestionType.SingleChoice && !string.IsNullOrWhiteSpace(q.OptionsJson))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<string>>(q.OptionsJson);
                        if (parsed != null) choices.AddRange(parsed);
                    }
                    catch
                    {
                        // Fallback simple comma split if not strict JSON array
                        choices.AddRange(q.OptionsJson.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                    }
                }

                questionDtos.Add(new FeedbackQuestionDto
                {
                    Id = q.Id,
                    QuestionKey = q.QuestionKey,
                    PromptText = q.PromptText,
                    QuestionType = q.QuestionType,
                    TargetAudience = q.TargetAudience,
                    OptionsJson = q.OptionsJson,
                    Choices = choices,
                    IsRequired = q.IsRequired,
                    SortOrder = q.SortOrder
                });
            }
        }

        return new GuestWorkshopFeedbackDetailsResponse
        {
            BookingId = booking.Id,
            BookingReference = booking.Id.ToString()[..8].ToUpperInvariant(),
            WorkshopTitle = workshop?.Title ?? "Masterclass",
            TrainerName = workshop?.TrainerProfile?.FullName ?? "Staff Trainer",
            WorkshopDate = workshop?.WorkshopDate ?? DateTime.UtcNow.Date,
            StartTime = workshop?.StartTime,
            EndTime = workshop?.EndTime,
            DanceStyle = workshop?.DanceStyle,
            Venue = workshop?.Venue,
            AudienceType = audience,
            VersionNumber = formVersion?.VersionNumber ?? 1,
            FormVersionId = formVersion?.Id,
            Questions = questionDtos,
            AlreadySubmitted = false,
            IsEligible = true
        };
    }

    public async Task<bool> SubmitGuestFeedbackAsync(
        SubmitGuestWorkshopFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new BusinessRuleException("FEEDBACK_TOKEN_INVALID", "Feedback token is required.");
        }

        string tokenHash;
        try
        {
            tokenHash = FeedbackTokenHelper.HashToken(request.Token);
        }
        catch
        {
            throw new BusinessRuleException("FEEDBACK_TOKEN_INVALID", "Invalid feedback token format.", StatusCodes.Status400BadRequest);
        }

        var isRelational = _db.Database.IsRelational();
        await using var tx = isRelational ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;

        var tokenEntity = await _db.WorkshopFeedbackTokens
            .Include(t => t.WorkshopBooking)
                .ThenInclude(b => b.Workshop)
            .Include(t => t.FeedbackFormVersion)
                .ThenInclude(v => v!.Questions)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (tokenEntity == null)
        {
            throw new BusinessRuleException("FEEDBACK_TOKEN_INVALID", "Invalid feedback token.", StatusCodes.Status404NotFound);
        }

        if (tokenEntity.UsedAt != null)
        {
            throw new BusinessRuleException("FEEDBACK_ALREADY_USED", "Feedback has already been submitted using this link.", StatusCodes.Status409Conflict);
        }

        if (tokenEntity.ExpiresAt < DateTime.UtcNow)
        {
            throw new BusinessRuleException("FEEDBACK_TOKEN_EXPIRED", "This feedback link has expired.", StatusCodes.Status400BadRequest);
        }

        var booking = tokenEntity.WorkshopBooking;
        if (booking == null || booking.Status is WorkshopBookingStatus.Cancelled or WorkshopBookingStatus.PendingPayment)
        {
            throw new BusinessRuleException("BOOKING_NOT_ELIGIBLE", "Booking is cancelled, unpaid, or invalid.");
        }

        // Check duplicate submission
        var alreadyExists = await _db.WorkshopFeedbacks
            .AnyAsync(f => (f.WorkshopBookingId == booking.Id || f.WorkshopFeedbackTokenId == tokenEntity.Id) && f.IsValid, cancellationToken);

        if (alreadyExists)
        {
            throw new BusinessRuleException("FEEDBACK_ALREADY_SUBMITTED", "Feedback has already been submitted for this booking.", StatusCodes.Status409Conflict);
        }

        // Validate rating rules
        if (tokenEntity.AudienceType == FeedbackAudienceType.Attended)
        {
            if (request.Rating.HasValue && (request.Rating < 1 || request.Rating > 5))
            {
                throw new BusinessRuleException("VALIDATION_FAILED", "Overall rating must be between 1 and 5 stars.");
            }
        }
        else
        {
            // For NoShow, rating is optional. If provided, must still be 1-5.
            if (request.Rating.HasValue && (request.Rating < 1 || request.Rating > 5))
            {
                throw new BusinessRuleException("VALIDATION_FAILED", "Rating must be between 1 and 5 stars if provided.");
            }
        }

        if (request.Comment?.Length > 2000)
        {
            throw new BusinessRuleException("VALIDATION_FAILED", "Comment cannot exceed 2000 characters.");
        }

        // Dynamic Questions Validation & Processing
        var applicableQuestions = tokenEntity.FeedbackFormVersion?.Questions
            .Where(q => q.TargetAudience == tokenEntity.AudienceType || q.TargetAudience == FeedbackAudienceType.Both)
            .ToList() ?? new List<FeedbackQuestion>();

        var submittedAnswers = request.Answers ?? Array.Empty<FeedbackAnswerSubmissionDto>();
        var answerEntities = new List<WorkshopFeedbackAnswer>();

        // Validate each submitted answer
        foreach (var answerDto in submittedAnswers)
        {
            var question = applicableQuestions.FirstOrDefault(q => q.Id == answerDto.QuestionId);
            if (question == null)
            {
                throw new BusinessRuleException(
                    "INVALID_QUESTION",
                    $"Submitted answer references an invalid or non-applicable question ID: {answerDto.QuestionId}");
            }

            if (question.QuestionType == FeedbackQuestionType.Rating1To5)
            {
                if (question.IsRequired && !answerDto.NumericValue.HasValue)
                {
                    throw new BusinessRuleException("VALIDATION_FAILED", $"Question '{question.PromptText}' requires a star rating.");
                }

                if (answerDto.NumericValue.HasValue && (answerDto.NumericValue < 1 || answerDto.NumericValue > 5))
                {
                    throw new BusinessRuleException("VALIDATION_FAILED", $"Rating for '{question.PromptText}' must be between 1 and 5.");
                }
            }
            else if (question.QuestionType == FeedbackQuestionType.SingleChoice)
            {
                if (question.IsRequired && string.IsNullOrWhiteSpace(answerDto.TextValue))
                {
                    throw new BusinessRuleException("VALIDATION_FAILED", $"Please select an option for '{question.PromptText}'.");
                }

                if (!string.IsNullOrWhiteSpace(answerDto.TextValue) && !string.IsNullOrWhiteSpace(question.OptionsJson))
                {
                    List<string>? allowedChoices = null;
                    try
                    {
                        allowedChoices = JsonSerializer.Deserialize<List<string>>(question.OptionsJson);
                    }
                    catch { }

                    if (allowedChoices != null && allowedChoices.Count > 0 &&
                        !allowedChoices.Any(c => string.Equals(c, answerDto.TextValue.Trim(), StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new BusinessRuleException(
                            "INVALID_CHOICE",
                            $"Selected choice '{answerDto.TextValue}' is not valid for question '{question.PromptText}'.");
                    }
                }
            }
            else if (question.QuestionType == FeedbackQuestionType.Text)
            {
                if (question.IsRequired && string.IsNullOrWhiteSpace(answerDto.TextValue))
                {
                    throw new BusinessRuleException("VALIDATION_FAILED", $"Please enter a response for '{question.PromptText}'.");
                }

                if (answerDto.TextValue?.Length > 2000)
                {
                    throw new BusinessRuleException("VALIDATION_FAILED", $"Response for '{question.PromptText}' cannot exceed 2000 characters.");
                }
            }

            answerEntities.Add(new WorkshopFeedbackAnswer
            {
                Id = Guid.NewGuid(),
                FeedbackQuestionId = question.Id,
                NumericValue = answerDto.NumericValue,
                TextValue = answerDto.TextValue?.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        // Check for any missing required questions not answered
        foreach (var reqQ in applicableQuestions.Where(q => q.IsRequired))
        {
            var answered = submittedAnswers.Any(a =>
                a.QuestionId == reqQ.Id &&
                ((reqQ.QuestionType == FeedbackQuestionType.Rating1To5 && a.NumericValue.HasValue) ||
                 (reqQ.QuestionType != FeedbackQuestionType.Rating1To5 && !string.IsNullOrWhiteSpace(a.TextValue))));

            if (!answered)
            {
                throw new BusinessRuleException("REQUIRED_QUESTION_MISSING", $"Please answer required question: '{reqQ.PromptText}'");
            }
        }

        var feedback = new WorkshopFeedback
        {
            Id = Guid.NewGuid(),
            WorkshopId = booking.WorkshopId,
            WorkshopBookingId = booking.Id,
            StudentProfileId = booking.StudentProfileId == Guid.Empty ? null : booking.StudentProfileId,
            FeedbackFormVersionId = tokenEntity.FeedbackFormVersionId,
            WorkshopFeedbackTokenId = tokenEntity.Id,
            AudienceType = tokenEntity.AudienceType,
            Rating = request.Rating,
            Comment = request.Comment?.Trim(),
            WouldRecommend = request.WouldRecommend ?? (request.Rating.HasValue && request.Rating.Value >= 4),
            WouldAttendTrainerAgain = request.WouldAttendTrainerAgain ?? (request.Rating.HasValue && request.Rating.Value >= 4),
            SubmittedAt = DateTime.UtcNow,
            IsValid = true
        };

        foreach (var answer in answerEntities)
        {
            answer.WorkshopFeedbackId = feedback.Id;
            feedback.Answers.Add(answer);
        }

        _db.WorkshopFeedbacks.Add(feedback);
        tokenEntity.UsedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        if (tx != null)
        {
            await tx.CommitAsync(cancellationToken);
        }

        return true;
    }

    public async Task<GenerateFeedbackTokenResponse> GenerateTokenForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        // Backward-compatibility wrapper delegating to authoritative token resolver
        var booking = await _db.WorkshopBookings
            .Include(b => b.Workshop)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
            throw new BusinessRuleException("BOOKING_NOT_ELIGIBLE", "Workshop booking not found.", StatusCodes.Status404NotFound);

        var existingToken = await _db.WorkshopFeedbackTokens
            .Where(t => t.WorkshopBookingId == bookingId && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingToken != null)
        {
            return new GenerateFeedbackTokenResponse
            {
                BookingId = bookingId,
                Token = existingToken.TokenHash,
                FeedbackUrl = $"/feedback/workshop/{existingToken.TokenHash}",
                ExpiresAt = existingToken.ExpiresAt
            };
        }

        throw new BusinessRuleException(
            "TOKEN_NOT_FOUND",
            "Feedback token must be issued through the authoritative FeedbackEligibilityService pipeline.");
    }

    public async Task<TrainerAggregateFeedbackDto> GetTrainerAggregateFeedbackAsync(
        Guid trainerProfileId,
        CancellationToken cancellationToken = default)
    {
        var feedbacks = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Where(f => f.Workshop.TrainerProfileId == trainerProfileId &&
                        f.IsValid &&
                        f.Rating.HasValue &&
                        f.AudienceType == FeedbackAudienceType.Attended)
            .Select(f => f.Rating!.Value)
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
        var items = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Where(f => f.Workshop.TrainerProfileId == trainerProfileId &&
                        f.IsValid &&
                        f.Rating.HasValue &&
                        f.AudienceType == FeedbackAudienceType.Attended)
            .Include(f => f.Workshop)
            .OrderByDescending(f => f.SubmittedAt)
            .Select(f => new AdminTrainerFeedbackDto
            {
                FeedbackId = f.Id,
                WorkshopId = f.WorkshopId,
                WorkshopTitle = f.Workshop.Title,
                WorkshopDate = f.Workshop.WorkshopDate,
                Rating = f.Rating!.Value,
                Comment = f.Comment,
                BookingReference = f.WorkshopBookingId.HasValue ? f.WorkshopBookingId.Value.ToString().Substring(0, 8).ToUpper() : "DIRECT",
                SubmittedAt = f.SubmittedAt
            })
            .ToListAsync(cancellationToken);

        return items;
    }
}
