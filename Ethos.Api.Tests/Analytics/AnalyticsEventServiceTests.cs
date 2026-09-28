using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Ethos.Api.Application.Analytics;
using Ethos.Api.Contracts.Analytics;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ethos.Api.Tests.Analytics;

public class AnalyticsEventServiceTests
{
    private AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task RecordEventAsync_ValidRequest_PersistsEventWithServerAuthoritativeName()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AnalyticsEventService(db, NullLogger<AnalyticsEventService>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("103.21.244.2");
        httpContext.Request.Headers.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

        var request = new RecordAnalyticsEventRequest
        {
            EventType = AnalyticsEventType.WorkshopView,
            VisitorId = "vis_abc12345",
            SessionId = "sess_xyz98765",
            WorkshopId = Guid.NewGuid(),
            Path = "/workshops/urban-groove-masterclass",
            Referrer = "https://instagram.com",
            Metadata = new Dictionary<string, string>
            {
                { "source", "hero_banner" },
                { "trainer", "Alex" }
            }
        };

        // Act
        var result = await service.RecordEventAsync(request, httpContext, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.EventId);

        var saved = await db.AnalyticsEvents.FirstOrDefaultAsync(e => e.Id == result.EventId);
        Assert.NotNull(saved);
        Assert.Equal(AnalyticsEventType.WorkshopView, saved.EventType);
        Assert.Equal("WorkshopView", saved.EventName); // Server-authoritative name derived from enum
        Assert.Equal("vis_abc12345", saved.VisitorId);
        Assert.Equal("sess_xyz98765", saved.SessionId);
        Assert.Equal(request.WorkshopId, saved.WorkshopId);
        Assert.Equal("/workshops/urban-groove-masterclass", saved.Path);
        Assert.Equal("https://instagram.com", saved.Referrer);
        Assert.NotNull(saved.MetadataJson);
        Assert.Contains("hero_banner", saved.MetadataJson);
        Assert.Null(saved.IpAddress);
        Assert.Null(saved.UserAgent);
    }

    [Fact]
    public async Task RecordEventAsync_AuthenticatedUser_CapturesUserId()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AnalyticsEventService(db, NullLogger<AnalyticsEventService>.Instance);

        var userId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "STUDENT")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var request = new RecordAnalyticsEventRequest
        {
            EventType = AnalyticsEventType.PageView,
            VisitorId = "vis_authenticated_1",
            SessionId = "sess_authenticated_1",
            Path = "/classes"
        };

        // Act
        var result = await service.RecordEventAsync(request, httpContext, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var saved = await db.AnalyticsEvents.FirstOrDefaultAsync(e => e.Id == result.EventId);
        Assert.NotNull(saved);
        Assert.Equal(userId, saved.UserId);
    }

    [Fact]
    public void Validation_InvalidVisitorId_YieldsValidationError()
    {
        // Arrange
        var request = new RecordAnalyticsEventRequest
        {
            EventType = AnalyticsEventType.PageView,
            VisitorId = "shrt", // < 8 characters
            SessionId = "sess_valid_12345"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RecordAnalyticsEventRequest.VisitorId)));
    }

    [Fact]
    public void Validation_InvalidSessionIdCharacters_YieldsValidationError()
    {
        // Arrange
        var request = new RecordAnalyticsEventRequest
        {
            EventType = AnalyticsEventType.PageView,
            VisitorId = "vis_valid_12345",
            SessionId = "sess!@#$%^&*()_invalid" // Invalid symbols
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RecordAnalyticsEventRequest.SessionId)));
    }

    [Fact]
    public void Validation_InvalidEventType_YieldsValidationError()
    {
        // Arrange
        var request = new RecordAnalyticsEventRequest
        {
            EventType = (AnalyticsEventType)999, // Undefined enum value
            VisitorId = "vis_valid_12345",
            SessionId = "sess_valid_12345"
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RecordAnalyticsEventRequest.EventType)));
    }

    [Fact]
    public void Validation_ExcessiveMetadataKeys_YieldsValidationError()
    {
        // Arrange
        var metadata = new Dictionary<string, string>();
        for (int i = 0; i < 25; i++)
        {
            metadata[$"key_{i}"] = $"value_{i}";
        }

        var request = new RecordAnalyticsEventRequest
        {
            EventType = AnalyticsEventType.PageView,
            VisitorId = "vis_valid_12345",
            SessionId = "sess_valid_12345",
            Metadata = metadata
        };

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RecordAnalyticsEventRequest.Metadata)));
    }

    [Fact]
    public void AdminPermissions_AnalyticsView_IsRegisteredInAllPermissions()
    {
        // Assert
        Assert.Equal("ANALYTICS_VIEW", AdminPermissions.AnalyticsView);
        Assert.True(AdminPermissions.IsValid("ANALYTICS_VIEW"));
        Assert.Contains("ANALYTICS_VIEW", AdminPermissions.GetAll());
    }
}
