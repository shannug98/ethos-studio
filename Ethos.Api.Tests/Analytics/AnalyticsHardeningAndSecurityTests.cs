using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Analytics;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Analytics;
using Ethos.Api.Controllers.Admin;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ethos.Api.Tests.Analytics;

public class AnalyticsHardeningAndSecurityTests
{
    private AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task Security_AnonymousUser_CannotSpoofUserId()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AnalyticsEventService(db, NullLogger<AnalyticsEventService>.Instance);

        var httpContext = new DefaultHttpContext();
        var request = new RecordAnalyticsEventRequest
        {
            EventType = AnalyticsEventType.PageView,
            VisitorId = "visitor_anon_123",
            SessionId = "session_anon_123",
            Path = "/workshops",
            Metadata = new Dictionary<string, string>
            {
                { "userId", "impersonated-user-id" },
                { "role", "ADMIN" }
            }
        };

        var result = await service.RecordEventAsync(request, httpContext, CancellationToken.None);

        var saved = await db.AnalyticsEvents.FirstOrDefaultAsync(e => e.Id == result.EventId);
        Assert.NotNull(saved);
        Assert.Null(saved.UserId);
    }

    [Fact]
    public async Task Security_AuthenticatedUser_UserIdStrictlyDerivedFromTokenClaims()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AnalyticsEventService(db, NullLogger<AnalyticsEventService>.Instance);

        var authoritativeUserId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, authoritativeUserId.ToString()),
            new Claim(ClaimTypes.Role, "ADMIN")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        var request = new RecordAnalyticsEventRequest
        {
            EventType = AnalyticsEventType.LoginCompleted,
            VisitorId = "visitor_auth_123",
            SessionId = "session_auth_123",
            Path = "/admin_portal/login"
        };

        var result = await service.RecordEventAsync(request, httpContext, CancellationToken.None);

        var saved = await db.AnalyticsEvents.FirstOrDefaultAsync(e => e.Id == result.EventId);
        Assert.NotNull(saved);
        Assert.Equal(authoritativeUserId, saved.UserId);
    }

    [Fact]
    public async Task Privacy_AnalyticsEvent_DoesNotPersistRawIpOrUserAgent()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AnalyticsEventService(db, NullLogger<AnalyticsEventService>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("103.21.244.2");
        httpContext.Request.Headers.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

        var request = new RecordAnalyticsEventRequest
        {
            EventType = AnalyticsEventType.PageView,
            VisitorId = "visitor_privacy_123",
            SessionId = "session_privacy_123",
            Path = "/classes"
        };

        var result = await service.RecordEventAsync(request, httpContext, CancellationToken.None);

        var saved = await db.AnalyticsEvents.FirstOrDefaultAsync(e => e.Id == result.EventId);
        Assert.NotNull(saved);
        Assert.Null(saved.IpAddress);
        Assert.Null(saved.UserAgent);
    }

    [Fact]
    public async Task Integrity_MetadataStored_IsAlwaysValidJson()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AnalyticsEventService(db, NullLogger<AnalyticsEventService>.Instance);

        var httpContext = new DefaultHttpContext();
        var request = new RecordAnalyticsEventRequest
        {
            EventType = AnalyticsEventType.WorkshopView,
            VisitorId = "visitor_valid_json",
            SessionId = "session_valid_json",
            Metadata = new Dictionary<string, string>
            {
                { "section", "hero" },
                { "tier", "early_bird" }
            }
        };

        var result = await service.RecordEventAsync(request, httpContext, CancellationToken.None);

        var saved = await db.AnalyticsEvents.FirstOrDefaultAsync(e => e.Id == result.EventId);
        Assert.NotNull(saved);
        Assert.NotNull(saved.MetadataJson);

        // Verify valid JSON deserialization
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(saved.MetadataJson);
        Assert.NotNull(parsed);
        Assert.Equal("hero", parsed["section"]);
        Assert.Equal("early_bird", parsed["tier"]);
    }

    [Fact]
    public void Validation_SerializedMetadataExceeding2048Chars_FailsValidation()
    {
        // Construct metadata entries whose serialized JSON exceeds 2048 chars
        var metadata = new Dictionary<string, string>();
        for (int i = 0; i < 15; i++)
        {
            metadata[$"key_{i:D2}_{new string('k', 40)}"] = new string('v', 200);
        }

        var serialized = JsonSerializer.Serialize(metadata);
        Assert.True(serialized.Length > 2048, "Test fixture must generate > 2048 chars JSON");

        var request = new RecordAnalyticsEventRequest
        {
            EventType = AnalyticsEventType.PageView,
            VisitorId = "valid_visitor_123",
            SessionId = "valid_session_123",
            Metadata = metadata
        };

        var context = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, context, validationResults, true);

        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(RecordAnalyticsEventRequest.Metadata)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(-1)]
    public void Validation_InvalidEventType_FailsValidation(int invalidEventType)
    {
        var request = new RecordAnalyticsEventRequest
        {
            EventType = (AnalyticsEventType)invalidEventType,
            VisitorId = "visitor_valid_123",
            SessionId = "session_valid_123",
            Path = "/home"
        };

        var context = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, context, validationResults, true);

        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(RecordAnalyticsEventRequest.EventType)));
    }

    [Theory]
    [InlineData("short")] // < 8 characters
    [InlineData("invalid spaces in id")] // Invalid characters
    [InlineData("invalid$char#")] // Special characters not in [a-zA-Z0-9_-]
    [InlineData("this_is_an_extremely_long_visitor_id_that_exceeds_sixty_four_characters_limit_by_far_and_must_fail")] // > 64 chars
    public void Validation_InvalidVisitorOrSessionId_FailsValidation(string invalidId)
    {
        var request = new RecordAnalyticsEventRequest
        {
            EventType = AnalyticsEventType.PageView,
            VisitorId = invalidId,
            SessionId = "valid_session_123",
            Path = "/home"
        };

        var context = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, context, validationResults, true);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("all_time")]
    [InlineData("invalid_date_range")]
    [InlineData("365days")]
    public void QueryService_InvalidRange_ThrowsArgumentException(string invalidRange)
    {
        Assert.Throws<ArgumentException>(() => AnalyticsQueryService.ResolveRange(invalidRange));
    }

    [Fact]
    public async Task AdminAnalyticsController_InvalidRange_ReturnsBadRequest400()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var queryService = new AnalyticsQueryService(db, NullLogger<AnalyticsQueryService>.Instance);
        var authService = new FakeAdminAuthorizationService { ShouldAuthorize = true };
        var controller = new AdminAnalyticsController(queryService, authService);

        var result = await controller.GetSummary("invalid_unknown_range");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }
}
