using System.Security.Cryptography;
using System.Text;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Application.Payments;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Notifications;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Payments;

public class RazorpayWebhookAndFulfillmentTests
{
    private const string WebhookSecret = "test_webhook_secret_key_123456789";

    private class MockTicketService : IWorkshopTicketService
    {
        public int IssueCallCount { get; private set; }

        public string DeriveQrToken(WorkshopTicket ticket) => $"QR-{ticket.Id}";
        public string ComputeTokenHash(string rawToken) => "HASH";
        public string GeneratePdfDownloadToken(Guid ticketId, TimeSpan? validity = null) => $"PDF-TOKEN-{ticketId}";
        public bool ValidatePdfDownloadToken(Guid ticketId, string? token) => token == $"PDF-TOKEN-{ticketId}";

        public Task<List<WorkshopTicketResponse>> IssueTicketsForBookingAsync(
            WorkshopBooking booking,
            PaymentTransaction transaction,
            User? user,
            CancellationToken cancellationToken = default)
        {
            IssueCallCount++;
            var tickets = new List<WorkshopTicketResponse>
            {
                new WorkshopTicketResponse
                {
                    Id = Guid.NewGuid(),
                    TicketNumber = $"ETH-WS-{booking.Id.ToString()[..6]}-01",
                    WorkshopBookingId = booking.Id,
                    WorkshopId = booking.WorkshopId,
                    AttendeeName = booking.GuestName ?? "Student",
                    Status = TicketStatus.Issued,
                    IssuedAt = DateTime.UtcNow
                }
            };
            return Task.FromResult(tickets);
        }

        public Task<IReadOnlyList<WorkshopTicketResponse>> GetTicketsForBookingAsync(
            Guid bookingId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var list = new List<WorkshopTicketResponse>
            {
                new WorkshopTicketResponse
                {
                    Id = Guid.NewGuid(),
                    TicketNumber = $"ETH-WS-{bookingId.ToString()[..6]}-01",
                    WorkshopBookingId = bookingId,
                    AttendeeName = "Student",
                    Status = TicketStatus.Issued
                }
            };
            return Task.FromResult<IReadOnlyList<WorkshopTicketResponse>>(list);
        }

        public Task<WorkshopTicketResponse> GetTicketPassAsync(Guid ticketId, Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WorkshopTicketResponse> UpdateAttendeeDetailsAsync(Guid bookingId, Guid ticketId, Guid userId, UpdateAttendeeDetailsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ResendTicketPassAsync(Guid bookingId, Guid ticketId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(true);
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

    private AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void VerifyWebhookSignature_WithValidSignature_ReturnsTrue()
    {
        var payload = "{\"event\":\"payment.captured\"}";
        var sig = ComputeSignature(payload, WebhookSecret);

        var isValid = PaymentFulfillmentService.VerifyWebhookSignature(payload, sig, WebhookSecret);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyWebhookSignature_WithTamperedBody_ReturnsFalse()
    {
        var original = "{\"event\":\"payment.captured\"}";
        var tampered = "{\"event\":\"payment.captured\",\"injected\":true}";
        var sig = ComputeSignature(original, WebhookSecret);

        var isValid = PaymentFulfillmentService.VerifyWebhookSignature(tampered, sig, WebhookSecret);

        Assert.False(isValid);
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_WithInvalidSignature_ThrowsUnauthorizedAccessException()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var ticketService = new MockTicketService();
        var options = Options.Create(new RazorpaySettings { WebhookSecret = WebhookSecret });
        var service = new PaymentFulfillmentService(
            db,
            ticketService,
            options,
            new MockNotificationService(),
            NullLogger<PaymentFulfillmentService>.Instance);

        var payload = "{\"event\":\"payment.captured\"}";

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ProcessWebhookEventAsync(payload, "invalid_signature"));
    }

    private async Task<(User User, StudentProfile Profile)> CreateUserAndProfileAsync(AppDbContext db)
    {
        var role = new Role { Id = Guid.NewGuid(), Code = "STUDENT", Name = "Student" };
        db.Roles.Add(role);

        var user = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "CUST-" + Guid.NewGuid().ToString()[..6],
            FullName = "Test User",
            Phone = "9876543210",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var userRole = new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            AssignedAt = DateTime.UtcNow
        };
        user.UserRoles.Add(userRole);
        db.Users.Add(user);

        var profile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();
        return (user, profile);
    }

    [Fact]
    public async Task ProcessWebhookEventAsync_FulfillsBooking_AndRepeatedDeliveryIsIdempotent()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var ticketService = new MockTicketService();
        var options = Options.Create(new RazorpaySettings { WebhookSecret = WebhookSecret });
        var service = new PaymentFulfillmentService(
            db,
            ticketService,
            options,
            new MockNotificationService(),
            NullLogger<PaymentFulfillmentService>.Instance);

        var (user, profile) = await CreateUserAndProfileAsync(db);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Afrobeats Workshop",
            Capacity = 20,
            WorkshopDate = DateTime.UtcNow.AddDays(5),
            StartTime = TimeSpan.FromHours(17),
            EndTime = TimeSpan.FromHours(19)
        };
        db.Workshops.Add(workshop);

        var bookingId = Guid.NewGuid();
        var booking = new WorkshopBooking
        {
            Id = bookingId,
            WorkshopId = workshop.Id,
            StudentProfileId = profile.Id,
            Quantity = 1,
            TotalPrice = 1000m,
            GuestName = "Test Dancer",
            GuestPhone = "9876543210",
            Status = WorkshopBookingStatus.PendingPayment,
            IdempotencyKey = Guid.NewGuid().ToString(),
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);

        var txId = Guid.NewGuid();
        var tx = new PaymentTransaction
        {
            Id = txId,
            UserId = user.Id,
            Purpose = PaymentPurpose.WorkshopBooking,
            ReferenceId = booking.Id,
            Amount = 1000m,
            Currency = "INR",
            Status = PaymentStatus.OrderCreated,
            RazorpayOrderId = "order_test_12345",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.PaymentTransactions.Add(tx);
        await db.SaveChangesAsync();

        var payload = @"
        {
            ""id"": ""evt_test_1"",
            ""event"": ""payment.captured"",
            ""payload"": {
                ""payment"": {
                    ""entity"": {
                        ""id"": ""pay_test_999"",
                        ""order_id"": ""order_test_12345"",
                        ""amount"": 100000,
                        ""currency"": ""INR"",
                        ""status"": ""captured""
                    }
                }
            }
        }";

        var sig = ComputeSignature(payload, WebhookSecret);

        // First delivery: Fulfills payment
        var result1 = await service.ProcessWebhookEventAsync(payload, sig);
        Assert.True(result1.Success);
        Assert.Equal("Fulfilled", result1.Status);

        var updatedTx = await db.PaymentTransactions.FindAsync(txId);
        Assert.Equal(PaymentStatus.Paid, updatedTx!.Status);
        Assert.Equal("pay_test_999", updatedTx.RazorpayPaymentId);

        var updatedBooking = await db.WorkshopBookings.FindAsync(bookingId);
        Assert.Equal(WorkshopBookingStatus.Confirmed, updatedBooking!.Status);
        Assert.Equal(1, ticketService.IssueCallCount);

        // Second delivery (e.g. Razorpay webhook retry): Must be idempotent
        var result2 = await service.ProcessWebhookEventAsync(payload, sig);
        Assert.True(result2.Success);
        Assert.Equal("AlreadyPaid", result2.Status);

        // Ticket creation was not called a second time
        Assert.Equal(1, ticketService.IssueCallCount);
    }

    [Fact]
    public async Task WebhookFirst_ThenFrontendVerify_DoesNotDuplicateFulfillment()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var ticketService = new MockTicketService();
        var options = Options.Create(new RazorpaySettings { WebhookSecret = WebhookSecret });
        var service = new PaymentFulfillmentService(
            db,
            ticketService,
            options,
            new MockNotificationService(),
            NullLogger<PaymentFulfillmentService>.Instance);

        var (user, profile) = await CreateUserAndProfileAsync(db);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Contemporary Flow",
            Capacity = 25,
            WorkshopDate = DateTime.UtcNow.AddDays(3),
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12)
        };
        db.Workshops.Add(workshop);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = profile.Id,
            Quantity = 2,
            TotalPrice = 2000m,
            GuestName = "Guest",
            GuestPhone = "9988776655",
            Status = WorkshopBookingStatus.PendingPayment,
            IdempotencyKey = Guid.NewGuid().ToString(),
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);

        var tx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = PaymentPurpose.WorkshopBooking,
            ReferenceId = booking.Id,
            Amount = 2000m,
            Currency = "INR",
            Status = PaymentStatus.OrderCreated,
            RazorpayOrderId = "order_flow_888",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.PaymentTransactions.Add(tx);
        await db.SaveChangesAsync();

        var payload = @"
        {
            ""id"": ""evt_flow_1"",
            ""event"": ""payment.captured"",
            ""payload"": {
                ""payment"": {
                    ""entity"": {
                        ""id"": ""pay_flow_888"",
                        ""order_id"": ""order_flow_888"",
                        ""amount"": 200000,
                        ""currency"": ""INR"",
                        ""status"": ""captured""
                    }
                }
            }
        }";
        var sig = ComputeSignature(payload, WebhookSecret);

        // Step 1: Webhook arrives first
        var webhookRes = await service.ProcessWebhookEventAsync(payload, sig);
        Assert.Equal("Fulfilled", webhookRes.Status);
        Assert.Equal(1, ticketService.IssueCallCount);

        // Step 2: Browser verify arrives later
        var verifyRes = await service.FulfillWorkshopPaymentAsync(
            tx,
            "pay_flow_888",
            "sig_browser",
            source: "FrontendVerify");

        Assert.NotNull(verifyRes);
        Assert.Equal(WorkshopBookingStatus.Confirmed, verifyRes.Status);
        // Does NOT re-issue tickets
        Assert.Equal(1, ticketService.IssueCallCount);
    }
}
