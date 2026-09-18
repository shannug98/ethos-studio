using Ethos.Api.Application.Common;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Application.Payments;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Notifications;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class WorkshopCapacityConcurrencyTests
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

    private DbContextOptions<AppDbContext> CreateOptions(string dbName)
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
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
        var fulfillmentService = new PaymentFulfillmentService(
            dbContext,
            ticketService,
            razorpaySettings,
            notificationService,
            NullLogger<PaymentFulfillmentService>.Instance);

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
    public async Task CreateWorkshopOrderAsync_WhenCapacityIsOne_OnlyOneOrderSucceedsAndSecondFails()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateOptions(dbName);

        Guid workshopId;
        using (var setupDb = new AppDbContext(options))
        {
            setupDb.Roles.Add(new Role { Id = Guid.NewGuid(), Code = "STUDENT", Name = "Student" });
            var workshop = new Workshop
            {
                Id = Guid.NewGuid(),
                Title = "Exclusive Masterclass",
                DanceStyle = "HipHop",
                Level = "Advanced",
                Description = "High demand masterclass",
                WorkshopDate = DateTime.UtcNow.AddDays(7),
                StartTime = TimeSpan.FromHours(18),
                EndTime = TimeSpan.FromHours(20),
                Venue = "Studio A",
                Capacity = 1, // Only 1 seat available
                Price = 1000,
                Status = WorkshopStatus.Published
            };
            setupDb.Workshops.Add(workshop);
            await setupDb.SaveChangesAsync();
            workshopId = workshop.Id;
        }

        // Two competing requests to book the only seat
        var request1 = new CreateWorkshopOrderRequest
        {
            Quantity = 1,
            FullName = "First Booker",
            Phone = "9876543210",
            Email = "first@example.com",
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        var request2 = new CreateWorkshopOrderRequest
        {
            Quantity = 1,
            FullName = "Second Booker",
            Phone = "9876543211",
            Email = "second@example.com",
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        var successCount = 0;
        var failureCount = 0;

        using var db1 = new AppDbContext(options);
        var service1 = CreateService(db1);

        using var db2 = new AppDbContext(options);
        var service2 = CreateService(db2);

        // Attempt order 1
        try
        {
            var res1 = await service1.CreateWorkshopOrderAsync(workshopId, request1);
            if (res1 != null) successCount++;
        }
        catch (InvalidOperationException)
        {
            failureCount++;
        }

        // Attempt order 2 for the same workshop whose capacity is now reserved
        try
        {
            var res2 = await service2.CreateWorkshopOrderAsync(workshopId, request2);
            if (res2 != null) successCount++;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("sold out") || ex.Message.Contains("capacity"))
        {
            failureCount++;
        }

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        using var verifyDb = new AppDbContext(options);
        var totalBookings = await verifyDb.WorkshopBookings.CountAsync(b => b.WorkshopId == workshopId);
        Assert.Equal(1, totalBookings);
    }

    [Fact]
    public async Task CreateWorkshopOrderAsync_WhenMultipleTasksExecuteSimultaneously_NeverExceedsCapacity()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateOptions(dbName);

        Guid workshopId;
        const int capacity = 3;
        const int concurrentAttempts = 10;

        using (var setupDb = new AppDbContext(options))
        {
            setupDb.Roles.Add(new Role { Id = Guid.NewGuid(), Code = "STUDENT", Name = "Student" });
            var workshop = new Workshop
            {
                Id = Guid.NewGuid(),
                Title = "High Demand Showcase",
                DanceStyle = "Commercial",
                Level = "All Levels",
                Description = "Popular workshop",
                WorkshopDate = DateTime.UtcNow.AddDays(14),
                StartTime = TimeSpan.FromHours(19),
                EndTime = TimeSpan.FromHours(21),
                Venue = "Studio 1",
                Capacity = capacity,
                Price = 600,
                Status = WorkshopStatus.Published
            };
            setupDb.Workshops.Add(workshop);
            await setupDb.SaveChangesAsync();
            workshopId = workshop.Id;
        }

        var tasks = Enumerable.Range(0, concurrentAttempts).Select(async i =>
        {
            using var db = new AppDbContext(options);
            var service = CreateService(db);
            var req = new CreateWorkshopOrderRequest
            {
                Quantity = 1,
                FullName = $"Dancer {i}",
                Phone = $"98765432{i:D2}",
                Email = $"dancer{i}@example.com",
                IdempotencyKey = Guid.NewGuid().ToString()
            };

            try
            {
                var result = await service.CreateWorkshopOrderAsync(workshopId, req);
                return result != null;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        var successCount = results.Count(r => r);
        var failureCount = results.Count(r => !r);

        using var verifyDb2 = new AppDbContext(options);
        var totalBookings = await verifyDb2.WorkshopBookings.CountAsync(b => b.WorkshopId == workshopId);

        Assert.True(totalBookings <= capacity, $"Total bookings ({totalBookings}) exceeded capacity ({capacity}).");
        Assert.Equal(totalBookings, successCount);
        Assert.Equal(concurrentAttempts - successCount, failureCount);
    }

    [Fact]
    public async Task FulfillWorkshopPaymentAsync_WhenConfirmedSeatsExceedCapacity_MarksFailedAndFlagsOversoldRefundRequired()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateOptions(dbName);
        var razorpaySettings = Options.Create(new RazorpaySettings
        {
            KeyId = "rzp_test_placeholder",
            KeySecret = "test_secret",
            WebhookSecret = "test_webhook_secret"
        });

        var ticketService = new MockTicketService();
        var notificationService = new MockNotificationService();

        Guid workshopId;
        Guid txId;
        Guid bookingId;

        using (var setupDb = new AppDbContext(options))
        {
            var role = new Role { Id = Guid.NewGuid(), Code = "STUDENT", Name = "Student" };
            setupDb.Roles.Add(role);

            var user1 = new User
            {
                Id = Guid.NewGuid(),
                CustomerCode = "CUST-001",
                FullName = "Winner Dancer",
                Phone = "9876543210",
                IsActive = true
            };
            user1.UserRoles.Add(new UserRole { UserId = user1.Id, RoleId = role.Id });
            setupDb.Users.Add(user1);

            var profile1 = new StudentProfile { Id = Guid.NewGuid(), UserId = user1.Id };
            setupDb.StudentProfiles.Add(profile1);

            var workshop = new Workshop
            {
                Id = Guid.NewGuid(),
                Title = "Strict Capacity Workshop",
                Capacity = 1, // Only 1 seat
                WorkshopDate = DateTime.UtcNow.AddDays(4),
                StartTime = TimeSpan.FromHours(14),
                EndTime = TimeSpan.FromHours(16)
            };
            setupDb.Workshops.Add(workshop);

            // Existing confirmed booking takes the 1 available seat
            var confirmedBooking = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                StudentProfileId = profile1.Id,
                Quantity = 1,
                TotalPrice = 500m,
                Status = WorkshopBookingStatus.Confirmed,
                BookedAt = DateTime.UtcNow
            };
            setupDb.WorkshopBookings.Add(confirmedBooking);

            // Second user has an already-captured payment transaction, but capacity was consumed by winner
            var user2 = new User
            {
                Id = Guid.NewGuid(),
                CustomerCode = "CUST-002",
                FullName = "Late Dancer",
                Phone = "9876543211",
                IsActive = true
            };
            user2.UserRoles.Add(new UserRole { UserId = user2.Id, RoleId = role.Id });
            setupDb.Users.Add(user2);

            var profile2 = new StudentProfile { Id = Guid.NewGuid(), UserId = user2.Id };
            setupDb.StudentProfiles.Add(profile2);

            bookingId = Guid.NewGuid();
            var lateBooking = new WorkshopBooking
            {
                Id = bookingId,
                WorkshopId = workshop.Id,
                StudentProfileId = profile2.Id,
                Quantity = 1,
                TotalPrice = 500m,
                Status = WorkshopBookingStatus.PendingPayment,
                BookedAt = DateTime.UtcNow
            };
            setupDb.WorkshopBookings.Add(lateBooking);

            txId = Guid.NewGuid();
            var transaction = new PaymentTransaction
            {
                Id = txId,
                UserId = user2.Id,
                Purpose = PaymentPurpose.WorkshopBooking,
                ReferenceId = lateBooking.Id,
                Amount = 500m,
                Currency = "INR",
                Status = PaymentStatus.OrderCreated,
                RazorpayOrderId = "order_late_123",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            setupDb.PaymentTransactions.Add(transaction);

            await setupDb.SaveChangesAsync();
            workshopId = workshop.Id;
        }

        using (var db = new AppDbContext(options))
        {
            var fulfillmentService = new PaymentFulfillmentService(
                db,
                ticketService,
                razorpaySettings,
                notificationService,
                NullLogger<PaymentFulfillmentService>.Instance);

            var transaction = await db.PaymentTransactions.FindAsync(txId);
            Assert.NotNull(transaction);

            // Fulfillment must detect capacity is exceeded, NOT issue tickets, and set status to Failed
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fulfillmentService.FulfillWorkshopPaymentAsync(
                    transaction!,
                    "pay_late_captured_999",
                    "valid_sig",
                    source: "FrontendVerify"));

            Assert.Contains("reached full capacity", ex.Message);
        }

        // Verify database state: transaction marked Failed and booking marked Cancelled
        using (var verifyDb = new AppDbContext(options))
        {
            var verifiedTx = await verifyDb.PaymentTransactions.FindAsync(txId);
            Assert.Equal(PaymentStatus.Failed, verifiedTx!.Status);
            Assert.Equal("pay_late_captured_999", verifiedTx.RazorpayPaymentId);

            var verifiedBooking = await verifyDb.WorkshopBookings.FindAsync(bookingId);
            Assert.Equal(WorkshopBookingStatus.Cancelled, verifiedBooking!.Status);

            // Audit event for OversoldRefundRequired logged in PaymentEvents
            var paymentEvent = await verifyDb.PaymentEvents
                .FirstOrDefaultAsync(e => e.EventType == "OversoldRefundRequired");
            Assert.NotNull(paymentEvent);
            Assert.Contains("pay_late_captured_999", paymentEvent!.Payload);
        }
    }
}
