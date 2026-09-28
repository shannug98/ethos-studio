using System.Text.Json;
using Ethos.Api.Application.Feedback;
using Ethos.Api.Contracts.Feedback;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Exceptions;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ethos.Api.Tests.Feedback;

public class GuestWorkshopFeedbackServiceTests
{
    private const string SecretKey = "test_ticket_security_secret_key_1234567890";

    private AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    private (Workshop workshop, WorkshopBooking booking, FeedbackFormVersion version, WorkshopFeedbackToken token, string rawToken)
        SeedWorkshopWithFormAndToken(AppDbContext db, FeedbackAudienceType audienceType = FeedbackAudienceType.Attended, bool isUsed = false, bool isExpired = false)
    {
        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Contemporary Masterclass",
            WorkshopDate = DateTime.UtcNow.Date.AddDays(-1),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Timezone = "Asia/Kolkata",
            Status = WorkshopStatus.Completed,
            Capacity = 30,
            Price = 1200,
            Venue = "Ethos Studio A",
            DanceStyle = "Contemporary"
        };
        db.Workshops.Add(workshop);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            GuestName = "Priya Rao",
            GuestPhone = "+919876543210",
            Status = audienceType == FeedbackAudienceType.Attended ? WorkshopBookingStatus.Attended : WorkshopBookingStatus.Confirmed,
            BookedAt = DateTime.UtcNow.AddDays(-2)
        };
        db.WorkshopBookings.Add(booking);

        var formVersion = new FeedbackFormVersion
        {
            Id = Guid.NewGuid(),
            WorkshopFeedbackSettingId = Guid.NewGuid(),
            VersionNumber = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        var q1 = new FeedbackQuestion
        {
            Id = Guid.NewGuid(),
            FeedbackFormVersionId = formVersion.Id,
            QuestionKey = "instructor_clarity",
            PromptText = "How clear were the instructor's breakdowns?",
            QuestionType = FeedbackQuestionType.Rating1To5,
            TargetAudience = FeedbackAudienceType.Attended,
            IsRequired = true,
            SortOrder = 1
        };

        var q2 = new FeedbackQuestion
        {
            Id = Guid.NewGuid(),
            FeedbackFormVersionId = formVersion.Id,
            QuestionKey = "missed_reason",
            PromptText = "What was the main reason you could not make it?",
            QuestionType = FeedbackQuestionType.SingleChoice,
            TargetAudience = FeedbackAudienceType.NoShow,
            OptionsJson = JsonSerializer.Serialize(new List<string> { "Schedule Conflict", "Travel / Traffic", "Health", "Other" }),
            IsRequired = true,
            SortOrder = 1
        };

        var q3 = new FeedbackQuestion
        {
            Id = Guid.NewGuid(),
            FeedbackFormVersionId = formVersion.Id,
            QuestionKey = "general_feedback",
            PromptText = "Any thoughts or suggestions?",
            QuestionType = FeedbackQuestionType.Text,
            TargetAudience = FeedbackAudienceType.Both,
            IsRequired = false,
            SortOrder = 2
        };

        formVersion.Questions.Add(q1);
        formVersion.Questions.Add(q2);
        formVersion.Questions.Add(q3);
        db.FeedbackFormVersions.Add(formVersion);

        var rawToken = FeedbackTokenHelper.DeriveRawToken(booking.Id, SecretKey);
        var tokenHash = FeedbackTokenHelper.HashToken(rawToken);

        var token = new WorkshopFeedbackToken
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            TokenHash = tokenHash,
            AudienceType = audienceType,
            FeedbackFormVersionId = formVersion.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = isExpired ? DateTime.UtcNow.AddMinutes(-10) : DateTime.UtcNow.AddDays(6),
            UsedAt = isUsed ? DateTime.UtcNow.AddMinutes(-30) : null
        };
        db.WorkshopFeedbackTokens.Add(token);

        return (workshop, booking, formVersion, token, rawToken);
    }

    [Fact]
    public async Task GetFeedbackDetailsByTokenAsync_AttendedAudience_ReturnsOnlyAttendedAndBothQuestions()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (_, _, _, _, rawToken) = SeedWorkshopWithFormAndToken(db, FeedbackAudienceType.Attended);
        await db.SaveChangesAsync();

        var service = new GuestWorkshopFeedbackService(db);
        var response = await service.GetFeedbackDetailsByTokenAsync(rawToken);

        Assert.True(response.IsEligible);
        Assert.False(response.AlreadySubmitted);
        Assert.Equal(FeedbackAudienceType.Attended, response.AudienceType);
        Assert.Equal("Contemporary Masterclass", response.WorkshopTitle);
        Assert.Equal(2, response.Questions.Count);
        Assert.Contains(response.Questions, q => q.QuestionKey == "instructor_clarity");
        Assert.Contains(response.Questions, q => q.QuestionKey == "general_feedback");
        Assert.DoesNotContain(response.Questions, q => q.QuestionKey == "missed_reason");
    }

    [Fact]
    public async Task GetFeedbackDetailsByTokenAsync_NoShowAudience_ReturnsOnlyNoShowAndBothQuestionsWithChoices()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (_, _, _, _, rawToken) = SeedWorkshopWithFormAndToken(db, FeedbackAudienceType.NoShow);
        await db.SaveChangesAsync();

        var service = new GuestWorkshopFeedbackService(db);
        var response = await service.GetFeedbackDetailsByTokenAsync(rawToken);

        Assert.True(response.IsEligible);
        Assert.False(response.AlreadySubmitted);
        Assert.Equal(FeedbackAudienceType.NoShow, response.AudienceType);
        Assert.Equal(2, response.Questions.Count);

        var singleChoiceQ = response.Questions.FirstOrDefault(q => q.QuestionKey == "missed_reason");
        Assert.NotNull(singleChoiceQ);
        Assert.Equal(4, singleChoiceQ.Choices.Count);
        Assert.Contains("Schedule Conflict", singleChoiceQ.Choices);
        Assert.Contains("Travel / Traffic", singleChoiceQ.Choices);
    }

    [Fact]
    public async Task GetFeedbackDetailsByTokenAsync_ExpiredToken_ReturnsIneligibleAndExpiredMessage()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (_, _, _, _, rawToken) = SeedWorkshopWithFormAndToken(db, FeedbackAudienceType.Attended, isExpired: true);
        await db.SaveChangesAsync();

        var service = new GuestWorkshopFeedbackService(db);
        var response = await service.GetFeedbackDetailsByTokenAsync(rawToken);

        Assert.False(response.IsEligible);
        Assert.Contains("expired", response.IneligibilityReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetFeedbackDetailsByTokenAsync_UsedToken_ReturnsAlreadySubmitted()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (_, _, _, _, rawToken) = SeedWorkshopWithFormAndToken(db, FeedbackAudienceType.Attended, isUsed: true);
        await db.SaveChangesAsync();

        var service = new GuestWorkshopFeedbackService(db);
        var response = await service.GetFeedbackDetailsByTokenAsync(rawToken);

        Assert.False(response.IsEligible);
        Assert.True(response.AlreadySubmitted);
        Assert.Contains("already been submitted", response.IneligibilityReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitGuestFeedbackAsync_AttendedSubmission_PersistsFeedbackAndAnswersAndMarksTokenUsed()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, booking, formVersion, token, rawToken) = SeedWorkshopWithFormAndToken(db, FeedbackAudienceType.Attended);
        await db.SaveChangesAsync();

        var service = new GuestWorkshopFeedbackService(db);

        var clarityQ = formVersion.Questions.First(q => q.QuestionKey == "instructor_clarity");
        var generalQ = formVersion.Questions.First(q => q.QuestionKey == "general_feedback");

        var request = new SubmitGuestWorkshopFeedbackRequest
        {
            Token = rawToken,
            Rating = 5,
            Comment = "Outstanding energy and great choreography!",
            WouldRecommend = true,
            WouldAttendTrainerAgain = true,
            Answers = new[]
            {
                new FeedbackAnswerSubmissionDto
                {
                    QuestionId = clarityQ.Id,
                    NumericValue = 5
                },
                new FeedbackAnswerSubmissionDto
                {
                    QuestionId = generalQ.Id,
                    TextValue = "More intermediate sessions please."
                }
            }
        };

        var result = await service.SubmitGuestFeedbackAsync(request);
        Assert.True(result);

        // Verify token marked used
        var updatedToken = await db.WorkshopFeedbackTokens.FindAsync(token.Id);
        Assert.NotNull(updatedToken);
        Assert.NotNull(updatedToken.UsedAt);

        // Verify WorkshopFeedback created
        var feedback = await db.WorkshopFeedbacks
            .Include(f => f.Answers)
            .FirstOrDefaultAsync(f => f.WorkshopBookingId == booking.Id);

        Assert.NotNull(feedback);
        Assert.Equal(5, feedback.Rating);
        Assert.Equal("Outstanding energy and great choreography!", feedback.Comment);
        Assert.True(feedback.WouldRecommend);
        Assert.True(feedback.WouldAttendTrainerAgain);
        Assert.Equal(2, feedback.Answers.Count);
        Assert.Contains(feedback.Answers, a => a.FeedbackQuestionId == clarityQ.Id && a.NumericValue == 5);
        Assert.Contains(feedback.Answers, a => a.FeedbackQuestionId == generalQ.Id && a.TextValue == "More intermediate sessions please.");
    }

    [Fact]
    public async Task SubmitGuestFeedbackAsync_NoShowSubmission_AllowsNullRatingAndPersistsAnswers()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, booking, formVersion, token, rawToken) = SeedWorkshopWithFormAndToken(db, FeedbackAudienceType.NoShow);
        await db.SaveChangesAsync();

        var service = new GuestWorkshopFeedbackService(db);

        var reasonQ = formVersion.Questions.First(q => q.QuestionKey == "missed_reason");

        var request = new SubmitGuestWorkshopFeedbackRequest
        {
            Token = rawToken,
            Rating = null, // NoShow submissions cleanly do not submit rating
            Comment = "Sorry I got stuck at work, hoping to catch the next one!",
            WouldAttendTrainerAgain = true,
            Answers = new[]
            {
                new FeedbackAnswerSubmissionDto
                {
                    QuestionId = reasonQ.Id,
                    TextValue = "Schedule Conflict"
                }
            }
        };

        var result = await service.SubmitGuestFeedbackAsync(request);
        Assert.True(result);

        var updatedToken = await db.WorkshopFeedbackTokens.FindAsync(token.Id);
        Assert.NotNull(updatedToken);
        Assert.NotNull(updatedToken.UsedAt);

        var feedback = await db.WorkshopFeedbacks
            .Include(f => f.Answers)
            .FirstOrDefaultAsync(f => f.WorkshopBookingId == booking.Id);

        Assert.NotNull(feedback);
        Assert.Null(feedback.Rating);
        Assert.True(feedback.WouldAttendTrainerAgain);
        Assert.Single(feedback.Answers);
        Assert.Equal("Schedule Conflict", feedback.Answers.First().TextValue);
    }

    [Fact]
    public async Task SubmitGuestFeedbackAsync_InvalidChoice_ThrowsBusinessRuleException()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (_, _, formVersion, _, rawToken) = SeedWorkshopWithFormAndToken(db, FeedbackAudienceType.NoShow);
        await db.SaveChangesAsync();

        var service = new GuestWorkshopFeedbackService(db);
        var reasonQ = formVersion.Questions.First(q => q.QuestionKey == "missed_reason");

        var request = new SubmitGuestWorkshopFeedbackRequest
        {
            Token = rawToken,
            Answers = new[]
            {
                new FeedbackAnswerSubmissionDto
                {
                    QuestionId = reasonQ.Id,
                    TextValue = "Alien Abduction" // Not in allowed list
                }
            }
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.SubmitGuestFeedbackAsync(request));
        Assert.Equal("INVALID_CHOICE", ex.Code);
    }

    [Fact]
    public async Task SubmitGuestFeedbackAsync_DoubleSubmission_ThrowsAlreadyUsedConflict()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (_, _, formVersion, _, rawToken) = SeedWorkshopWithFormAndToken(db, FeedbackAudienceType.Attended);
        await db.SaveChangesAsync();

        var service = new GuestWorkshopFeedbackService(db);
        var clarityQ = formVersion.Questions.First(q => q.QuestionKey == "instructor_clarity");

        var request = new SubmitGuestWorkshopFeedbackRequest
        {
            Token = rawToken,
            Rating = 5,
            Answers = new[]
            {
                new FeedbackAnswerSubmissionDto
                {
                    QuestionId = clarityQ.Id,
                    NumericValue = 5
                }
            }
        };

        // First submission succeeds
        await service.SubmitGuestFeedbackAsync(request);

        // Second submission fails with conflict
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.SubmitGuestFeedbackAsync(request));
        Assert.Equal("FEEDBACK_ALREADY_USED", ex.Code);
    }

    [Fact]
    public void FeedbackTokenHelper_DeriveRawToken_FailsClosedWhenSecretKeyMissing()
    {
        var bookingId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => FeedbackTokenHelper.DeriveRawToken(bookingId, null!));
        Assert.Throws<InvalidOperationException>(() => FeedbackTokenHelper.DeriveRawToken(bookingId, ""));
        Assert.Throws<InvalidOperationException>(() => FeedbackTokenHelper.DeriveRawToken(bookingId, "   "));
    }
}
