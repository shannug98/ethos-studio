using Ethos.Api.Application.Common;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Notifications;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class WorkshopServiceTests
{
    private class MockCurrentUserService : ICurrentUserService
    {
        public Guid UserId => Guid.NewGuid();
        public bool IsAuthenticated => false;
        public string? Phone => null;
        public string? Name => null;
        public IReadOnlyList<string> Roles => Array.Empty<string>();
    }

    private class MockPricingService : IWorkshopPricingService
    {
        public Task<WorkshopPricingResponse> CalculatePricingAsync(Workshop workshop, Guid? userId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WorkshopPricingResponse
            {
                WorkshopId = workshop.Id,
                StartingPrice = workshop.Price,
                CurrentPrice = workshop.Price
            });
        }

        public Task<WorkshopPriceQuoteResponse> CalculateQuoteAsync(Guid workshopId, int quantity, Guid? userId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WorkshopPriceQuoteResponse
            {
                WorkshopId = workshopId,
                RequestedQuantity = quantity,
                TotalAmount = 500 * quantity
            });
        }

        public Task EnsureDefaultTiersAsync(Workshop workshop, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public decimal CalculatePublicPrice(decimal startingPrice, int bookedSeats) => startingPrice;
        public decimal CalculateStudentPrice(decimal startingPrice) => startingPrice;

        public Task<WorkshopPriceQuoteResponse> CalculateTicketTypeQuoteAsync(WorkshopPassType ticketType, int quantity, int currentTicketsSold, Guid? userId = null, CancellationToken cancellationToken = default)
        {
            var unitPrice = ticketType.Price > 0 ? ticketType.Price : 500m;
            return Task.FromResult(new WorkshopPriceQuoteResponse
            {
                WorkshopId = ticketType.WorkshopId,
                RequestedQuantity = quantity,
                TotalAmount = unitPrice * quantity,
                Breakdown = new List<WorkshopPriceQuoteItem>
                {
                    new() { TierNumber = 1, TierName = "Standard", Quantity = quantity, UnitPrice = unitPrice, Subtotal = unitPrice * quantity }
                }
            });
        }

        public void ValidateTicketTypePricingTiers(int totalQuantity, List<Ethos.Api.Contracts.Admin.AdminWorkshopPricingTierItem>? tiers) { }
    }

    private class MockNotificationService : INotificationService
    {
        public Task<IReadOnlyList<NotificationResponse>> GetMyNotificationsAsync(NotificationType? type = null) => Task.FromResult<IReadOnlyList<NotificationResponse>>(new List<NotificationResponse>());
        public Task<int> GetMyUnreadCountAsync() => Task.FromResult(0);
        public Task<bool> MarkAsReadAsync(Guid notificationId) => Task.FromResult(true);
        public Task MarkAllAsReadAsync() => Task.CompletedTask;
        public Task<bool> DeleteNotificationAsync(Guid notificationId) => Task.FromResult(true);
        public Task<NotificationResponse> SendNotificationAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new NotificationResponse());
    }

    private class MockTicketService : IWorkshopTicketService
    {
        public string DeriveQrToken(WorkshopTicket ticket) => "QR-TOKEN-123";
        public string ComputeTokenHash(string rawToken) => "HASH-123";
        public Task<List<WorkshopTicketResponse>> IssueTicketsForBookingAsync(WorkshopBooking booking, PaymentTransaction transaction, User? user, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<WorkshopTicketResponse>());
        }
        public Task<IReadOnlyList<WorkshopTicketResponse>> GetTicketsForBookingAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorkshopTicketResponse>>(new List<WorkshopTicketResponse>());
        public Task<WorkshopTicketResponse> GetTicketPassAsync(Guid ticketId, Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WorkshopTicketResponse> UpdateAttendeeDetailsAsync(Guid bookingId, Guid ticketId, Guid userId, UpdateAttendeeDetailsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ResendTicketPassAsync(Guid bookingId, Guid ticketId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public string GeneratePdfDownloadToken(Guid ticketId, TimeSpan? validity = null) => "TOKEN-123";
        public bool ValidatePdfDownloadToken(Guid ticketId, string? token) => token == "TOKEN-123";
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options);
        db.Roles.Add(new Role { Id = Guid.NewGuid(), Code = "STUDENT", Name = "Student" });
        db.SaveChanges();
        return db;
    }

    private WorkshopService CreateService(AppDbContext dbContext, Microsoft.AspNetCore.Hosting.IWebHostEnvironment? env = null)
    {
        var razorpaySettings = Options.Create(new RazorpaySettings
        {
            KeyId = "rzp_test_placeholder",
            KeySecret = "test_secret",
            WebhookSecret = "test_webhook_secret"
        });

        var ticketService = new MockTicketService();
        var notificationService = new MockNotificationService();
        var fulfillmentService = new Ethos.Api.Application.Payments.PaymentFulfillmentService(
            dbContext,
            ticketService,
            razorpaySettings,
            notificationService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Ethos.Api.Application.Payments.PaymentFulfillmentService>.Instance);

        return new WorkshopService(
            dbContext,
            new MockCurrentUserService(),
            new MockPricingService(),
            notificationService,
            ticketService,
            fulfillmentService,
            razorpaySettings,
            env ?? new TestWebHostEnvironment());
    }

    [Fact]
    public async Task CreateWorkshopOrderAsync_SameStudentCanBookMultipleTimes()
    {
        using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Hip Hop Choreography",
            DanceStyle = "HipHop",
            Level = "Open",
            Description = "Test Workshop",
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            StartTime = TimeSpan.FromHours(18),
            EndTime = TimeSpan.FromHours(20),
            Venue = "Studio A",
            Capacity = 20,
            Price = 500,
            Status = WorkshopStatus.Published
        };
        dbContext.Workshops.Add(workshop);
        await dbContext.SaveChangesAsync();

        var request1 = new CreateWorkshopOrderRequest
        {
            Quantity = 1,
            FullName = "John Doe",
            Phone = "9876543210",
            Email = "john@example.com",
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        var request2 = new CreateWorkshopOrderRequest
        {
            Quantity = 3,
            FullName = "John Doe",
            Phone = "9876543210",
            Email = "john@example.com",
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        // Act
        var order1 = await service.CreateWorkshopOrderAsync(workshop.Id, request1);
        var order2 = await service.CreateWorkshopOrderAsync(workshop.Id, request2);

        // Assert
        Assert.NotNull(order1);
        Assert.NotNull(order2);
        Assert.NotEqual(order1.TransactionId, order2.TransactionId);

        var bookings = await dbContext.WorkshopBookings.Where(b => b.WorkshopId == workshop.Id).ToListAsync();
        Assert.Equal(2, bookings.Count);
        Assert.Equal(1, bookings[0].Quantity);
        Assert.Equal(3, bookings[1].Quantity);
    }

    [Fact]
    public async Task CreateWorkshopOrderAsync_SameIdempotencyKey_ReturnsSameOrder()
    {
        using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Urban Dance",
            DanceStyle = "Urban",
            Level = "Open",
            Description = "Test",
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            StartTime = TimeSpan.FromHours(18),
            EndTime = TimeSpan.FromHours(20),
            Venue = "Studio B",
            Capacity = 10,
            Price = 600,
            Status = WorkshopStatus.Published
        };
        dbContext.Workshops.Add(workshop);
        await dbContext.SaveChangesAsync();

        var idempotencyKey = "UNIQUE-IDEMPOTENCY-KEY-12345";
        var request = new CreateWorkshopOrderRequest
        {
            Quantity = 2,
            FullName = "Jane Smith",
            Phone = "9876543211",
            Email = "jane@example.com",
            IdempotencyKey = idempotencyKey
        };

        // Act
        var order1 = await service.CreateWorkshopOrderAsync(workshop.Id, request);
        var order2 = await service.CreateWorkshopOrderAsync(workshop.Id, request);

        // Assert
        Assert.Equal(order1.TransactionId, order2.TransactionId);
        Assert.Equal(order1.Amount, order2.Amount);

        var bookings = await dbContext.WorkshopBookings.Where(b => b.WorkshopId == workshop.Id).ToListAsync();
        Assert.Single(bookings);
        Assert.Equal(idempotencyKey, bookings[0].IdempotencyKey);
    }

    [Fact]
    public async Task CreateWorkshopOrderAsync_CapacityExceeded_ThrowsInvalidOperationException()
    {
        using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Intensive Masterclass",
            DanceStyle = "Contemporary",
            Level = "Advanced",
            Description = "Limited seats",
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12),
            Venue = "Studio Main",
            Capacity = 5,
            Price = 1000,
            Status = WorkshopStatus.Published
        };
        dbContext.Workshops.Add(workshop);

        // Add 4 confirmed booking seats
        dbContext.WorkshopBookings.Add(new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = Guid.NewGuid(),
            Quantity = 4,
            Status = WorkshopBookingStatus.Confirmed,
            BookedAt = DateTime.UtcNow,
            IdempotencyKey = Guid.NewGuid().ToString()
        });

        await dbContext.SaveChangesAsync();

        var request = new CreateWorkshopOrderRequest
        {
            Quantity = 2, // Requests 2 seats, but only 1 seat is left (5 - 4)
            FullName = "Bob Vance",
            Phone = "9876543212",
            Email = "bob@example.com",
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateWorkshopOrderAsync(workshop.Id, request));

        Assert.Contains("does not have enough remaining seats available", ex.Message);
    }

    [Fact]
    public async Task CreateWorkshopOrderAsync_ExpiredPendingBooking_DoesNotBlockCapacity()
    {
        using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "K-Pop Choreography",
            DanceStyle = "KPop",
            Level = "All",
            Description = "Test",
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            StartTime = TimeSpan.FromHours(14),
            EndTime = TimeSpan.FromHours(16),
            Venue = "Studio C",
            Capacity = 5,
            Price = 500,
            Status = WorkshopStatus.Published
        };
        dbContext.Workshops.Add(workshop);

        // 3 confirmed seats
        dbContext.WorkshopBookings.Add(new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = Guid.NewGuid(),
            Quantity = 3,
            Status = WorkshopBookingStatus.Confirmed,
            BookedAt = DateTime.UtcNow,
            IdempotencyKey = Guid.NewGuid().ToString()
        });

        // Pending booking that expired 5 minutes ago
        var expiredPendingBooking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = Guid.NewGuid(),
            Quantity = 2,
            Status = WorkshopBookingStatus.PendingPayment,
            BookedAt = DateTime.UtcNow.AddMinutes(-20),
            ReservationExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            IdempotencyKey = "EXPIRED-KEY-001"
        };
        dbContext.WorkshopBookings.Add(expiredPendingBooking);
        await dbContext.SaveChangesAsync();

        // Total capacity = 5. Confirmed = 3. Unexpired Pending = 0. Remaining = 2.
        var request = new CreateWorkshopOrderRequest
        {
            Quantity = 2, // Requesting 2 seats
            FullName = "Alice Walker",
            Phone = "9876543213",
            Email = "alice@example.com",
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        // Act
        var order = await service.CreateWorkshopOrderAsync(workshop.Id, request);

        // Assert
        Assert.NotNull(order);
        var pendingBookings = await dbContext.WorkshopBookings
            .Where(b => b.WorkshopId == workshop.Id && b.Status == WorkshopBookingStatus.PendingPayment)
            .ToListAsync();
        Assert.Equal(2, pendingBookings.Count); // Old expired one + new active one
    }

    [Fact]
    public async Task CreateWorkshopOrder_WhenBookingCutoffPassed_ThrowsInvalidOperationException()
    {
        // Arrange
        var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        // Workshop yesterday with cutoff time 10:00 AM (definitely passed)
        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Cutoff Passed Workshop",
            DanceStyle = "Hip Hop",
            Level = "Intermediate",
            WorkshopDate = DateTime.UtcNow.Date.AddDays(-1),
            StartTime = new TimeSpan(18, 0, 0),
            EndTime = new TimeSpan(20, 0, 0),
            BookingCutoffTime = new TimeSpan(17, 0, 0),
            Venue = "Main Studio",
            Price = 500,
            Capacity = 30,
            Status = WorkshopStatus.Published,
            PublicVisibility = true
        };
        dbContext.Workshops.Add(workshop);
        await dbContext.SaveChangesAsync();

        var request = new CreateWorkshopOrderRequest
        {
            Quantity = 1,
            FullName = "Late Attendee",
            Phone = "9876543214",
            Email = "late@example.com"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateWorkshopOrderAsync(workshop.Id, request));

        Assert.Contains("closed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateWorkshopOrder_WhenBookingCutoffInFuture_Succeeds()
    {
        // Arrange
        var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        // Workshop tomorrow with cutoff time 18:00 (future)
        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Future Cutoff Workshop",
            DanceStyle = "Contemporary",
            Level = "Open Level",
            WorkshopDate = DateTime.UtcNow.Date.AddDays(2),
            StartTime = new TimeSpan(18, 0, 0),
            EndTime = new TimeSpan(20, 0, 0),
            BookingCutoffTime = new TimeSpan(17, 30, 0),
            Venue = "Main Studio",
            Price = 600,
            Capacity = 25,
            Status = WorkshopStatus.Published,
            PublicVisibility = true
        };
        dbContext.Workshops.Add(workshop);
        await dbContext.SaveChangesAsync();

        var request = new CreateWorkshopOrderRequest
        {
            Quantity = 1,
            FullName = "Timely Attendee",
            Phone = "9876543215",
            Email = "timely@example.com"
        };

        // Act
        var order = await service.CreateWorkshopOrderAsync(workshop.Id, request);

        // Assert
        Assert.NotNull(order);
        Assert.Equal(workshop.Id, order.WorkshopId);
    }

    [Fact]
    public void GetBookingCutoffUtc_WhenCutoffNull_DefaultsToStartUtc()
    {
        var workshopDate = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
        var startTime = new TimeSpan(18, 0, 0); // 6:00 PM IST
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        var expectedStartUtc = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 11, 20, 18, 0, 0, DateTimeKind.Unspecified), tz);

        var workshop = new Workshop
        {
            WorkshopDate = workshopDate,
            StartTime = startTime,
            EndTime = new TimeSpan(20, 0, 0),
            StartUtc = expectedStartUtc,
            BookingCutoffTime = null,
            Timezone = "Asia/Kolkata"
        };

        var calculatedCutoffUtc = workshop.GetBookingCutoffUtc();

        Assert.Equal(expectedStartUtc, calculatedCutoffUtc);
        Assert.Equal(DateTimeKind.Utc, calculatedCutoffUtc.Kind);
    }

    [Fact]
    public void GetBookingCutoffUtc_ExplicitCutoff_ConvertsFromAsiaKolkataToUtcWithoutDateShift()
    {
        // Workshop on 15 Oct 2026 with cutoff at 17:30 IST
        // 17:30 IST (UTC+5:30) is exactly 12:00 UTC on the same calendar day.
        var workshop = new Workshop
        {
            WorkshopDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
            StartTime = new TimeSpan(18, 0, 0),
            EndTime = new TimeSpan(20, 0, 0),
            BookingCutoffTime = new TimeSpan(17, 30, 0),
            Timezone = "Asia/Kolkata"
        };

        var cutoffUtc = workshop.GetBookingCutoffUtc();
        var expectedUtc = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(expectedUtc, cutoffUtc);
    }

    [Fact]
    public void IsBookingClosed_ExactBoundaryConditions_EvaluatedCorrectly()
    {
        var workshop = new Workshop
        {
            WorkshopDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
            StartTime = new TimeSpan(18, 0, 0),
            EndTime = new TimeSpan(20, 0, 0),
            BookingCutoffTime = new TimeSpan(17, 30, 0),
            Timezone = "Asia/Kolkata"
        };

        var cutoffUtc = workshop.GetBookingCutoffUtc(); // 2026-10-15 12:00:00 UTC

        // 1. One second before boundary -> OPEN
        var oneSecBefore = cutoffUtc.AddSeconds(-1);
        Assert.False(workshop.IsBookingClosed(oneSecBefore));

        // 2. Exactly at the boundary -> CLOSED (now >= cutoff)
        Assert.True(workshop.IsBookingClosed(cutoffUtc));

        // 3. One second after boundary -> CLOSED
        var oneSecAfter = cutoffUtc.AddSeconds(1);
        Assert.True(workshop.IsBookingClosed(oneSecAfter));

        // 4. One day before -> OPEN
        var dayBefore = cutoffUtc.AddDays(-1);
        Assert.False(workshop.IsBookingClosed(dayBefore));

        // 5. One day after -> CLOSED
        var dayAfter = cutoffUtc.AddDays(1);
        Assert.True(workshop.IsBookingClosed(dayAfter));
    }

    [Fact]
    public async Task GetApprovedWorkshopsAsync_FiltersOutUnpublishedAndCancelledWorkshops()
    {
        using var db = CreateDbContext();
        var service = CreateService(db);

        // 1. Published and public -> Eligible
        var w1 = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Published Workshop",
            Status = WorkshopStatus.Published,
            PublicVisibility = true,
            Capacity = 30,
            Price = 500m,
            WorkshopDate = DateTime.UtcNow.AddDays(5),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0)
        };

        // 2. Approved and public -> Eligible
        var w2 = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Approved Workshop",
            Status = WorkshopStatus.Approved,
            PublicVisibility = true,
            Capacity = 25,
            Price = 600m,
            WorkshopDate = DateTime.UtcNow.AddDays(6),
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(16, 0, 0)
        };

        // 3. Cancelled but public -> INELIGIBLE
        var w3 = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Cancelled Workshop",
            Status = WorkshopStatus.Cancelled,
            PublicVisibility = true,
            Capacity = 20,
            Price = 400m,
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0)
        };

        // 4. Published but PublicVisibility = false -> INELIGIBLE
        var w4 = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Hidden Workshop",
            Status = WorkshopStatus.Published,
            PublicVisibility = false,
            Capacity = 15,
            Price = 700m,
            WorkshopDate = DateTime.UtcNow.AddDays(8),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0)
        };

        // 5. Draft / PendingApproval -> INELIGIBLE
        var w5 = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Draft Workshop",
            Status = WorkshopStatus.Draft,
            PublicVisibility = true,
            Capacity = 10,
            Price = 300m,
            WorkshopDate = DateTime.UtcNow.AddDays(9),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0)
        };

        db.Workshops.AddRange(w1, w2, w3, w4, w5);
        await db.SaveChangesAsync();

        var result = await service.GetApprovedWorkshopsAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Id == w1.Id && r.Title == "Published Workshop");
        Assert.Contains(result, r => r.Id == w2.Id && r.Title == "Approved Workshop");
        Assert.DoesNotContain(result, r => r.Id == w3.Id);
        Assert.DoesNotContain(result, r => r.Id == w4.Id);
        Assert.DoesNotContain(result, r => r.Id == w5.Id);
    }

    [Fact]
    public async Task GetApprovedWorkshopsAsync_CorrectlyMapsBatchSessionAndPassBookedCounts()
    {
        using var db = CreateDbContext();
        var service = CreateService(db);

        var workshopId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var passId = Guid.NewGuid();

        var trainer = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            TrainerCode = "TR01",
            FullName = "Master Trainer",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.TrainerProfiles.Add(trainer);

        var workshop = new Workshop
        {
            Id = workshopId,
            Title = "Multi-Session Masterclass",
            Status = WorkshopStatus.Published,
            PublicVisibility = true,
            Capacity = 50,
            Price = 1000m,
            WorkshopDate = DateTime.UtcNow.AddDays(10),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(13, 0, 0)
        };
        db.Workshops.Add(workshop);

        var session = new WorkshopSession
        {
            Id = sessionId,
            WorkshopId = workshopId,
            TrainerProfileId = trainer.Id,
            Title = "Session A",
            SessionDate = DateTime.UtcNow.AddDays(10),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Capacity = 25,
            IsActive = true
        };
        db.WorkshopSessions.Add(session);

        var pass = new WorkshopPassType
        {
            Id = passId,
            WorkshopId = workshopId,
            Name = "Full Access Pass",
            Price = 1000m,
            TotalQuantity = 30,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(pass);

        // Add confirmed booking with 2 tickets
        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            WorkshopPassTypeId = passId,
            Quantity = 2,
            TotalPrice = 2000m,
            Status = WorkshopBookingStatus.Confirmed,
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);

        // Add booking session
        db.WorkshopBookingSessions.Add(new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopSessionId = sessionId,
            Status = WorkshopBookingSessionStatus.Booked
        });

        await db.SaveChangesAsync();

        var result = await service.GetApprovedWorkshopsAsync();

        Assert.Single(result);
        var res = result[0];
        Assert.Equal(workshopId, res.Id);
        Assert.Equal(2, res.BookedSeats);
        Assert.Equal(48, res.RemainingSeats);

        // Session check
        Assert.Single(res.Sessions);
        var sDto = res.Sessions[0];
        Assert.Equal(sessionId, sDto.Id);
        Assert.Equal(1, sDto.BookedSeats);
        Assert.Equal(24, sDto.RemainingSeats);

        // Pass check
        Assert.Single(res.PassTypes);
        var pDto = res.PassTypes[0];
        Assert.Equal(passId, pDto.Id);
        // Under Phase 5 All-Access capacity invariant: min(pass quota 28, session remaining 24) = 24
        Assert.Equal(24, pDto.RemainingQuantity);
    }

    [Fact]
    public async Task GetWorkshopByIdAsync_ReturnsMappedWorkshopWithCorrectData()
    {
        using var db = CreateDbContext();
        var service = CreateService(db);

        var workshopId = Guid.NewGuid();
        var trainer = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            TrainerCode = "TR02",
            FullName = "Senior Faculty",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.TrainerProfiles.Add(trainer);

        var workshop = new Workshop
        {
            Id = workshopId,
            TrainerProfileId = trainer.Id,
            Title = "Solo Workshop",
            Status = WorkshopStatus.Published,
            PublicVisibility = true,
            Capacity = 20,
            Price = 750m,
            WorkshopDate = DateTime.UtcNow.AddDays(3),
            StartTime = new TimeSpan(11, 0, 0),
            EndTime = new TimeSpan(13, 0, 0)
        };
        db.Workshops.Add(workshop);

        // Add 3 bookings
        db.WorkshopBookings.Add(new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            Quantity = 3,
            TotalPrice = 2250m,
            Status = WorkshopBookingStatus.Confirmed,
            BookedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var result = await service.GetWorkshopByIdAsync(workshopId);

        Assert.NotNull(result);
        Assert.Equal(workshopId, result!.Id);
        Assert.Equal("Solo Workshop", result.Title);
        Assert.Equal("Senior Faculty", result.TrainerName);
        Assert.Equal(3, result.BookedSeats);
        Assert.Equal(17, result.RemainingSeats);
        Assert.False(result.IsFull);
    }

    [Fact]
    public void CalculatePricing_AppliesVolumeTiersCorrectly()
    {
        var pricingService = new WorkshopPricingService(null!, null!);
        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Tiered Workshop",
            Capacity = 40,
            Price = 500m
        };

        var tiers = new List<WorkshopPricingTier>
        {
            new() { TierNumber = 1, TierName = "Early Bird", MinTickets = 1, MaxTickets = 10, Price = 500m },
            new() { TierNumber = 2, TierName = "Standard", MinTickets = 11, MaxTickets = 20, Price = 600m },
            new() { TierNumber = 3, TierName = "Late", MinTickets = 21, MaxTickets = 30, Price = 700m },
            new() { TierNumber = 4, TierName = "Final", MinTickets = 31, MaxTickets = null, Price = 800m }
        };

        // When 5 tickets sold, next ticket is 6 -> Early Bird (Tier 1) active at 500
        var p1 = ((IWorkshopPricingService)pricingService).CalculatePricing(workshop, tiers, 5, false);
        Assert.Equal(1, p1.CurrentTier);
        Assert.Equal("Early Bird", p1.CurrentTierName);
        Assert.Equal(500m, p1.CurrentPrice);
        Assert.Equal(600m, p1.NextPrice);
        Assert.Equal(5, p1.TicketsFilledInTier);
        Assert.Equal(5, p1.TicketsRemainingInTier);

        // When 10 tickets sold, next ticket is 11 -> Standard (Tier 2) active at 600
        var p2 = ((IWorkshopPricingService)pricingService).CalculatePricing(workshop, tiers, 10, false);
        Assert.Equal(2, p2.CurrentTier);
        Assert.Equal("Standard", p2.CurrentTierName);
        Assert.Equal(600m, p2.CurrentPrice);
        Assert.Equal(700m, p2.NextPrice);
        Assert.Equal(0, p2.TicketsFilledInTier);
        Assert.Equal(10, p2.TicketsRemainingInTier);

        // When student eligible, student price is 100 off current price
        var pStudent = ((IWorkshopPricingService)pricingService).CalculatePricing(workshop, tiers, 5, true);
        Assert.True(pStudent.IsStudentEligible);
        Assert.Equal(400m, pStudent.FinalAmount);
        Assert.Equal(500m, pStudent.CurrentPublicPrice);
    }
}
