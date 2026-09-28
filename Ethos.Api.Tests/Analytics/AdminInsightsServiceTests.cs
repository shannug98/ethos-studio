using Ethos.Api.Application.Analytics;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ethos.Api.Tests.Analytics;

public class AdminInsightsServiceTests
{
    private AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetOverviewAsync_AuthoritativeCompletedBookings_RequiresConfirmedOrAttendedAndPaidTransaction()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AdminInsightsService(db, NullLogger<AdminInsightsService>.Instance);

        var now = DateTime.UtcNow;
        var workshopId = Guid.NewGuid();

        db.Workshops.Add(new Workshop
        {
            Id = workshopId,
            Title = "Salsa Mastery",
            Status = WorkshopStatus.Published,
            DanceStyle = "Salsa",
            Level = "All",
            Venue = "Main Studio"
        });

        // 1. Confirmed + Paid -> Valid Completed Booking
        var booking1Id = Guid.NewGuid();
        var tx1Id = Guid.NewGuid();
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = tx1Id,
            Purpose = PaymentPurpose.WorkshopBooking,
            ReferenceId = booking1Id,
            Status = PaymentStatus.Paid,
            Amount = 1500m,
            CreatedAt = now,
            PaidAt = now.AddMinutes(2)
        });
        db.WorkshopBookings.Add(new WorkshopBooking
        {
            Id = booking1Id,
            WorkshopId = workshopId,
            Status = WorkshopBookingStatus.Confirmed,
            PaymentTransactionId = tx1Id,
            TotalPrice = 1500m,
            BookedAt = now
        });

        // 2. Confirmed + Failed Payment -> NOT Completed
        var booking2Id = Guid.NewGuid();
        var tx2Id = Guid.NewGuid();
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = tx2Id,
            Purpose = PaymentPurpose.WorkshopBooking,
            ReferenceId = booking2Id,
            Status = PaymentStatus.Failed,
            Amount = 1500m,
            CreatedAt = now
        });
        db.WorkshopBookings.Add(new WorkshopBooking
        {
            Id = booking2Id,
            WorkshopId = workshopId,
            Status = WorkshopBookingStatus.Confirmed,
            PaymentTransactionId = tx2Id,
            TotalPrice = 1500m,
            BookedAt = now
        });

        // 3. PendingPayment + Paid Payment -> NOT Completed (until booking confirmed)
        var booking3Id = Guid.NewGuid();
        var tx3Id = Guid.NewGuid();
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = tx3Id,
            Purpose = PaymentPurpose.WorkshopBooking,
            ReferenceId = booking3Id,
            Status = PaymentStatus.Paid,
            Amount = 1500m,
            CreatedAt = now
        });
        db.WorkshopBookings.Add(new WorkshopBooking
        {
            Id = booking3Id,
            WorkshopId = workshopId,
            Status = WorkshopBookingStatus.PendingPayment,
            PaymentTransactionId = tx3Id,
            TotalPrice = 1500m,
            BookedAt = now
        });

        // 4. Cancelled + Paid -> NOT Completed
        var booking4Id = Guid.NewGuid();
        var tx4Id = Guid.NewGuid();
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = tx4Id,
            Purpose = PaymentPurpose.WorkshopBooking,
            ReferenceId = booking4Id,
            Status = PaymentStatus.Paid,
            Amount = 1500m,
            CreatedAt = now
        });
        db.WorkshopBookings.Add(new WorkshopBooking
        {
            Id = booking4Id,
            WorkshopId = workshopId,
            Status = WorkshopBookingStatus.Cancelled,
            PaymentTransactionId = tx4Id,
            TotalPrice = 1500m,
            BookedAt = now
        });

        // 5. Attended + Paid -> Valid Completed Booking
        var booking5Id = Guid.NewGuid();
        var tx5Id = Guid.NewGuid();
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = tx5Id,
            Purpose = PaymentPurpose.WorkshopBooking,
            ReferenceId = booking5Id,
            Status = PaymentStatus.Paid,
            Amount = 2000m,
            CreatedAt = now,
            PaidAt = now.AddMinutes(3)
        });
        db.WorkshopBookings.Add(new WorkshopBooking
        {
            Id = booking5Id,
            WorkshopId = workshopId,
            Status = WorkshopBookingStatus.Attended,
            PaymentTransactionId = tx5Id,
            TotalPrice = 2000m,
            BookedAt = now
        });

        await db.SaveChangesAsync();

        // Act
        var result = await service.GetOverviewAsync("last30days");

        // Assert
        Assert.Equal(2, result.CompletedBookings); // Only #1 and #5
        Assert.Equal(3500m, result.TotalRevenue); // 1500 + 2000
        Assert.Equal(5, result.PaymentAttempts); // Total 5 workshop payment transactions
    }

    [Fact]
    public async Task GetOverviewAsync_VisitorsAndFunnel_DeduplicatesAndCalculatesSafely()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AdminInsightsService(db, NullLogger<AdminInsightsService>.Instance);

        var now = DateTime.UtcNow;
        var workshopId = Guid.NewGuid();

        // 3 events from visitor 1, 2 events from visitor 2 -> 2 distinct visitors
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
            EventType = AnalyticsEventType.WorkshopView,
            EventName = "WorkshopView",
            OccurredAtUtc = now,
            VisitorId = "vis_1",
            SessionId = "sess_1",
            WorkshopId = workshopId
        });
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.WorkshopCheckoutStarted,
            EventName = "WorkshopCheckoutStarted",
            OccurredAtUtc = now,
            VisitorId = "vis_1",
            SessionId = "sess_1",
            WorkshopId = workshopId
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

        await db.SaveChangesAsync();

        var result = await service.GetOverviewAsync("last30days");

        Assert.Equal(2, result.Visitors);
        Assert.Equal(1, result.WorkshopViews);
        Assert.Equal(1, result.BookingStarts);
        Assert.Equal(50.0, result.Funnel.WorkshopViewRate); // 1 view / 2 visitors = 50%
        Assert.Equal(100.0, result.Funnel.BookingStartRate); // 1 checkout start / 1 view = 100%
    }

    [Fact]
    public async Task GetOverviewAsync_ZeroData_ReturnsZeroRatesWithoutExceptions()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AdminInsightsService(db, NullLogger<AdminInsightsService>.Instance);

        var result = await service.GetOverviewAsync("last30days");

        Assert.Equal(0, result.Visitors);
        Assert.Equal(0, result.WorkshopViews);
        Assert.Equal(0, result.BookingStarts);
        Assert.Equal(0, result.PaymentAttempts);
        Assert.Equal(0, result.CompletedBookings);
        Assert.Equal(0m, result.TotalRevenue);
        Assert.Equal(0.0, result.Funnel.OverallConversionRate);
        Assert.Equal(0.0, result.PaymentOutcomes.SuccessfulPercent);
    }

    [Fact]
    public async Task GetOverviewAsync_WorkshopFilter_OnlyIncludesSelectedWorkshop()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AdminInsightsService(db, NullLogger<AdminInsightsService>.Instance);

        var now = DateTime.UtcNow;
        var workshop1 = Guid.NewGuid();
        var workshop2 = Guid.NewGuid();

        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.WorkshopView,
            EventName = "WorkshopView",
            OccurredAtUtc = now,
            VisitorId = "vis_1",
            SessionId = "sess_1",
            WorkshopId = workshop1
        });
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.WorkshopView,
            EventName = "WorkshopView",
            OccurredAtUtc = now,
            VisitorId = "vis_2",
            SessionId = "sess_2",
            WorkshopId = workshop2
        });

        await db.SaveChangesAsync();

        var result = await service.GetOverviewAsync("last30days", workshopId: workshop1);

        Assert.Equal(1, result.WorkshopViews);
        Assert.Equal(1, result.Visitors);
    }

    [Fact]
    public async Task GetLiveUsersAsync_FiltersByRollingWindow()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AdminInsightsService(db, NullLogger<AdminInsightsService>.Instance);

        var now = DateTime.UtcNow;

        // Recent event (2 minutes ago)
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.PageView,
            EventName = "PageView",
            OccurredAtUtc = now.AddMinutes(-2),
            VisitorId = "vis_live_1",
            SessionId = "sess_1"
        });

        // Stale event (30 minutes ago)
        db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = AnalyticsEventType.PageView,
            EventName = "PageView",
            OccurredAtUtc = now.AddMinutes(-30),
            VisitorId = "vis_stale_2",
            SessionId = "sess_2"
        });

        await db.SaveChangesAsync();

        var live15 = await service.GetLiveUsersAsync(windowMinutes: 15);
        var live5 = await service.GetLiveUsersAsync(windowMinutes: 5);

        Assert.Equal(1, live15.LiveUserCount);
        Assert.Equal(1, live5.LiveUserCount);
    }

    [Fact]
    public async Task GetActivityFeedAsync_NeverExposesPii()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new AdminInsightsService(db, NullLogger<AdminInsightsService>.Instance);

        var now = DateTime.UtcNow;
        var workshopId = Guid.NewGuid();

        db.Workshops.Add(new Workshop
        {
            Id = workshopId,
            Title = "Contemporary Dance",
            Status = WorkshopStatus.Published,
            DanceStyle = "Contemporary",
            Level = "All",
            Venue = "Main Studio"
        });

        var bookingId = Guid.NewGuid();
        var txId = Guid.NewGuid();
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = txId,
            Purpose = PaymentPurpose.WorkshopBooking,
            ReferenceId = bookingId,
            Status = PaymentStatus.Paid,
            Amount = 1200m,
            CreatedAt = now
        });
        db.WorkshopBookings.Add(new WorkshopBooking
        {
            Id = bookingId,
            WorkshopId = workshopId,
            Status = WorkshopBookingStatus.Confirmed,
            PaymentTransactionId = txId,
            TotalPrice = 1200m,
            BookedAt = now,
            GuestName = "Secret Person",
            GuestEmail = "secret@example.com",
            GuestPhone = "+919999999999"
        });

        await db.SaveChangesAsync();

        var feed = await service.GetActivityFeedAsync(10);

        Assert.NotEmpty(feed.Activities);
        foreach (var act in feed.Activities)
        {
            Assert.DoesNotContain("secret@example.com", act.Description);
            Assert.DoesNotContain("Secret Person", act.Description);
            Assert.DoesNotContain("+919999999999", act.Description);
        }
    }

    [Fact]
    public void ResolveRange_InvalidValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AdminInsightsService.ResolveRange("random_invalid_range"));
    }
}
