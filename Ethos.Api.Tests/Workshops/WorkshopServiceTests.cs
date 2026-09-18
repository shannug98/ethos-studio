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

    private WorkshopService CreateService(AppDbContext dbContext)
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
            razorpaySettings);
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
}
