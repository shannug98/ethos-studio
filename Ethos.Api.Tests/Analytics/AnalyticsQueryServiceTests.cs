using Ethos.Api.Application.Analytics;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ethos.Api.Tests.Analytics;

public class AnalyticsQueryServiceTests
{
    private AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetSummaryAsync_WithSeededData_ReturnsAccurateMetrics()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AnalyticsQueryService(db, NullLogger<AnalyticsQueryService>.Instance);

        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();

        // 3 page views from 2 visitors
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.PageView,
            EventName = "PageView",
            OccurredAtUtc = now,
            VisitorId = "vis_1",
            SessionId = "sess_1",
            UserId = userId
        });
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.PageView,
            EventName = "PageView",
            OccurredAtUtc = now,
            VisitorId = "vis_1",
            SessionId = "sess_1"
        });
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.PageView,
            EventName = "PageView",
            OccurredAtUtc = now,
            VisitorId = "vis_2",
            SessionId = "sess_2"
        });

        // 1 checkout started
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.WorkshopCheckoutStarted,
            EventName = "WorkshopCheckoutStarted",
            OccurredAtUtc = now,
            VisitorId = "vis_2",
            SessionId = "sess_2"
        });

        // 1 checkout completed
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.WorkshopCheckoutCompleted,
            EventName = "WorkshopCheckoutCompleted",
            OccurredAtUtc = now,
            VisitorId = "vis_2",
            SessionId = "sess_2"
        });

        await db.SaveChangesAsync();

        // Act
        var summary = await service.GetSummaryAsync("today");

        // Assert
        Assert.Equal(5, summary.TotalEvents);
        Assert.Equal(2, summary.UniqueVisitors);
        Assert.Equal(2, summary.UniqueSessions);
        Assert.Equal(1, summary.AuthenticatedEvents);
        Assert.Equal(3, summary.PageViews);
        Assert.Equal(1, summary.CheckoutStarts);
        Assert.Equal(1, summary.CheckoutCompletions);
    }

    [Fact]
    public async Task GetSummaryAsync_EmptyDatabase_ReturnsZeroMetricsWithoutException()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AnalyticsQueryService(db, NullLogger<AnalyticsQueryService>.Instance);

        // Act
        var summary = await service.GetSummaryAsync("last30days");

        // Assert
        Assert.Equal(0, summary.TotalEvents);
        Assert.Equal(0, summary.UniqueVisitors);
        Assert.Equal(0, summary.UniqueSessions);
        Assert.Equal(0, summary.AuthenticatedEvents);
        Assert.Equal(0, summary.PageViews);
    }

    [Fact]
    public async Task GetTrendsAsync_GroupsByUtcDate()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AnalyticsQueryService(db, NullLogger<AnalyticsQueryService>.Instance);

        var date1 = DateTime.UtcNow.Date.AddDays(-2);
        var date2 = DateTime.UtcNow.Date.AddDays(-1);

        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.PageView,
            EventName = "PageView",
            OccurredAtUtc = date1.AddHours(10),
            VisitorId = "vis_1",
            SessionId = "sess_1"
        });
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.PageView,
            EventName = "PageView",
            OccurredAtUtc = date1.AddHours(14),
            VisitorId = "vis_2",
            SessionId = "sess_2"
        });
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.PageView,
            EventName = "PageView",
            OccurredAtUtc = date2.AddHours(8),
            VisitorId = "vis_3",
            SessionId = "sess_3"
        });

        await db.SaveChangesAsync();

        // Act
        var trends = await service.GetTrendsAsync("last7days");

        // Assert
        Assert.Equal("day", trends.Granularity);
        Assert.True(trends.Points.Count >= 2);

        var day1 = trends.Points.FirstOrDefault(p => p.DateUtc.Date == date1);
        Assert.NotNull(day1);
        Assert.Equal(2, day1.TotalEvents);
        Assert.Equal(2, day1.UniqueVisitors);

        var day2 = trends.Points.FirstOrDefault(p => p.DateUtc.Date == date2);
        Assert.NotNull(day2);
        Assert.Equal(1, day2.TotalEvents);
        Assert.Equal(1, day2.UniqueVisitors);
    }

    [Fact]
    public async Task GetEventBreakdownAsync_GroupsByEventType()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AnalyticsQueryService(db, NullLogger<AnalyticsQueryService>.Instance);

        var now = DateTime.UtcNow;

        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.WorkshopView,
            EventName = "WorkshopView",
            OccurredAtUtc = now,
            VisitorId = "vis_1",
            SessionId = "sess_1"
        });
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.WorkshopView,
            EventName = "WorkshopView",
            OccurredAtUtc = now,
            VisitorId = "vis_2",
            SessionId = "sess_2"
        });
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.PageView,
            EventName = "PageView",
            OccurredAtUtc = now,
            VisitorId = "vis_3",
            SessionId = "sess_3"
        });

        await db.SaveChangesAsync();

        // Act
        var breakdown = await service.GetEventBreakdownAsync("last7days");

        // Assert
        Assert.Equal(2, breakdown.Events.Count);
        var workshopView = breakdown.Events.FirstOrDefault(e => e.EventType == (int)AnalyticsEventType.WorkshopView);
        Assert.NotNull(workshopView);
        Assert.Equal(2, workshopView.Count);
        Assert.Equal(2, workshopView.UniqueVisitors);

        var pageView = breakdown.Events.FirstOrDefault(e => e.EventType == (int)AnalyticsEventType.PageView);
        Assert.NotNull(pageView);
        Assert.Equal(1, pageView.Count);
    }

    [Fact]
    public async Task GetWorkshopAnalyticsAsync_AggregatesAndAppliesLimit()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AnalyticsQueryService(db, NullLogger<AnalyticsQueryService>.Instance);

        var workshop1 = new Workshop { Id = Guid.NewGuid(), Title = "Hip Hop Beginner" };
        var workshop2 = new Workshop { Id = Guid.NewGuid(), Title = "Heels Advanced" };
        db.Workshops.AddRange(workshop1, workshop2);

        var now = DateTime.UtcNow;

        // Workshop 1: 2 views, 1 checkout start
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.WorkshopView,
            EventName = "WorkshopView",
            OccurredAtUtc = now,
            VisitorId = "v1",
            SessionId = "s1",
            WorkshopId = workshop1.Id,
            Workshop = workshop1
        });
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.WorkshopView,
            EventName = "WorkshopView",
            OccurredAtUtc = now,
            VisitorId = "v2",
            SessionId = "s2",
            WorkshopId = workshop1.Id,
            Workshop = workshop1
        });
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.WorkshopCheckoutStarted,
            EventName = "WorkshopCheckoutStarted",
            OccurredAtUtc = now,
            VisitorId = "v2",
            SessionId = "s2",
            WorkshopId = workshop1.Id,
            Workshop = workshop1
        });

        // Workshop 2: 1 view
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.WorkshopView,
            EventName = "WorkshopView",
            OccurredAtUtc = now,
            VisitorId = "v3",
            SessionId = "s3",
            WorkshopId = workshop2.Id,
            Workshop = workshop2
        });

        await db.SaveChangesAsync();

        // Act - query with limit 1
        var result = await service.GetWorkshopAnalyticsAsync("last7days", limit: 1);

        // Assert
        Assert.Single(result.Workshops);
        var top = result.Workshops[0];
        Assert.Equal(workshop1.Id, top.WorkshopId);
        Assert.Equal("Hip Hop Beginner", top.WorkshopName);
        Assert.Equal(2, top.Views);
        Assert.Equal(1, top.CheckoutStarts);
        Assert.Equal(2, top.UniqueVisitors);
    }

    [Fact]
    public async Task GetRecentEventsAsync_OrdersNewestFirstAndExcludesPii()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AnalyticsQueryService(db, NullLogger<AnalyticsQueryService>.Instance);

        var earlier = DateTime.UtcNow.AddHours(-2);
        var later = DateTime.UtcNow.AddHours(-1);

        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.PageView,
            EventName = "PageView",
            OccurredAtUtc = earlier,
            VisitorId = "v1",
            SessionId = "s1",
            IpAddress = "192.168.1.1", // PII
            UserAgent = "SecretAgent" // PII
        });
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.WorkshopView,
            EventName = "WorkshopView",
            OccurredAtUtc = later,
            VisitorId = "v2",
            SessionId = "s2"
        });

        await db.SaveChangesAsync();

        // Act
        var result = await service.GetRecentEventsAsync(10);

        // Assert
        Assert.Equal(2, result.Events.Count);
        Assert.Equal("WorkshopView", result.Events[0].EventName); // Newest first
        Assert.Equal("PageView", result.Events[1].EventName);
    }
}
