using Ethos.Api.Application.Common;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Notifications;

public class WhatsAppOutboxDispatcherTests
{
    private AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    private class MockMsg91Service : IMsg91WhatsAppService
    {
        public Func<BookingConfirmedData, string, Msg91DispatchResult> BookingHandler { get; set; } =
            (_, _) => Msg91DispatchResult.Accepted("msg_123", "req_123");

        public Func<TicketPdfData, string, Msg91DispatchResult> TicketHandler { get; set; } =
            (_, _) => Msg91DispatchResult.Accepted("msg_456", "req_456");

        public Task<Msg91DispatchResult> SendBookingConfirmedAsync(BookingConfirmedData data, string recipientPhone, CancellationToken cancellationToken = default) =>
            Task.FromResult(BookingHandler(data, recipientPhone));

        public Task<Msg91DispatchResult> SendTicketPdfAsync(TicketPdfData data, string recipientPhone, CancellationToken cancellationToken = default) =>
            Task.FromResult(TicketHandler(data, recipientPhone));

        public bool TryNormalizePhoneNumber(string? rawPhone, out string normalizedPhone, out string? failureReason, string defaultCountryCode = "91")
        {
            normalizedPhone = "919876543210";
            failureReason = null;
            return true;
        }

        public string BuildBookingConfirmedJson(BookingConfirmedData data, string recipientPhone) => "{}";
        public string BuildTicketPdfJson(TicketPdfData data, string recipientPhone) => "{}";
    }

    private class MockTicketPdfService : ITicketPdfService
    {
        public int CallCount { get; private set; }

        public Task<TicketPdfResult> GetOrCreateTicketPdfAsync(
            WorkshopTicket ticket,
            Workshop workshop,
            WorkshopBooking booking,
            string? rawQrToken = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new TicketPdfResult(
                Success: true,
                StorageKey: $"tickets/{ticket.TicketNumber}.pdf",
                SignedHttpsUrl: $"https://media.ethosdancestudio.com/tickets/{ticket.TicketNumber}.pdf?token=valid_signed_jwt",
                FileHash: "dummyhash123"));
        }
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_SuccessfullyDispatchesAndMarksSent()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var msg91 = new MockMsg91Service();
        var pdfService = new MockTicketPdfService();
        var options = Options.Create(new Msg91Options { BatchSize = 10, MaxRetryAttempts = 3 });

        var dispatcher = new WhatsAppOutboxDispatcher(db, msg91, pdfService, options, NullLogger<WhatsAppOutboxDispatcher>.Instance);

        var bookingId = Guid.NewGuid();
        var workshop = new Workshop { Id = Guid.NewGuid(), Title = "Salsa Masterclass", WorkshopDate = DateTime.UtcNow.AddDays(2), StartTime = TimeSpan.FromHours(19), EndTime = TimeSpan.FromHours(21) };
        var booking = new WorkshopBooking { Id = bookingId, Workshop = workshop, GuestName = "Priya Rao", GuestPhone = "9876543210" };

        db.Workshops.Add(workshop);
        db.WorkshopBookings.Add(booking);

        var notification = new WhatsAppNotification
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            NotificationType = WhatsAppNotificationType.BookingConfirmed,
            RecipientPhone = "9876543210",
            IdempotencyKey = $"wapp_booking_{bookingId}",
            Status = WhatsAppNotificationStatus.Pending
        };
        db.WhatsAppNotifications.Add(notification);
        await db.SaveChangesAsync();

        var processed = await dispatcher.ProcessPendingBatchAsync("worker-1");

        Assert.Equal(1, processed);

        var updated = await db.WhatsAppNotifications.FindAsync(notification.Id);
        Assert.NotNull(updated);
        Assert.Equal(WhatsAppNotificationStatus.Sent, updated.Status);
        Assert.NotNull(updated.SentAt);
        Assert.Equal("msg_123", updated.ProviderMessageId);
        Assert.Equal("req_123", updated.ProviderRequestId);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenProviderTimesOut_SetsAmbiguousTimeoutAndDoesNotRetryImmediately()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var msg91 = new MockMsg91Service
        {
            BookingHandler = (_, _) => Msg91DispatchResult.Timeout("Provider HTTP request timed out after 15s")
        };
        var pdfService = new MockTicketPdfService();
        var options = Options.Create(new Msg91Options { BatchSize = 10, MaxRetryAttempts = 3 });

        var dispatcher = new WhatsAppOutboxDispatcher(db, msg91, pdfService, options, NullLogger<WhatsAppOutboxDispatcher>.Instance);

        var bookingId = Guid.NewGuid();
        var workshop = new Workshop { Id = Guid.NewGuid(), Title = "Contemporary", WorkshopDate = DateTime.UtcNow.AddDays(3), StartTime = TimeSpan.FromHours(17), EndTime = TimeSpan.FromHours(19) };
        var booking = new WorkshopBooking { Id = bookingId, Workshop = workshop, GuestName = "Aarav", GuestPhone = "9876543210" };

        db.Workshops.Add(workshop);
        db.WorkshopBookings.Add(booking);

        var notification = new WhatsAppNotification
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            NotificationType = WhatsAppNotificationType.BookingConfirmed,
            RecipientPhone = "9876543210",
            IdempotencyKey = $"wapp_booking_{bookingId}",
            Status = WhatsAppNotificationStatus.Pending
        };
        db.WhatsAppNotifications.Add(notification);
        await db.SaveChangesAsync();

        await dispatcher.ProcessPendingBatchAsync("worker-1");

        var updated = await db.WhatsAppNotifications.FindAsync(notification.Id);
        Assert.NotNull(updated);
        // Requirement 3: AmbiguousTimeout must NOT be automatically retried as normal failure
        Assert.Equal(WhatsAppNotificationStatus.AmbiguousTimeout, updated.Status);
        Assert.Null(updated.NextAttemptAt);
        Assert.Contains("timed out", updated.LastError);
    }

    [Fact]
    public async Task RecoverAbandonedLeasesAsync_RecoversStaleSendingRecords()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var msg91 = new MockMsg91Service();
        var pdfService = new MockTicketPdfService();
        var options = Options.Create(new Msg91Options { MaxRetryAttempts = 3 });

        var dispatcher = new WhatsAppOutboxDispatcher(db, msg91, pdfService, options, NullLogger<WhatsAppOutboxDispatcher>.Instance);

        var staleNotification = new WhatsAppNotification
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            NotificationType = WhatsAppNotificationType.BookingConfirmed,
            RecipientPhone = "9876543210",
            IdempotencyKey = "wapp_booking_stale_1",
            Status = WhatsAppNotificationStatus.Sending,
            LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Expired 5 minutes ago!
            LockedByWorkerId = "dead-worker",
            Attempts = 1
        };

        db.WhatsAppNotifications.Add(staleNotification);
        await db.SaveChangesAsync();

        var recovered = await dispatcher.RecoverAbandonedLeasesAsync();

        Assert.Equal(1, recovered);

        var reloaded = await db.WhatsAppNotifications.FindAsync(staleNotification.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(WhatsAppNotificationStatus.Pending, reloaded.Status);
        Assert.Null(reloaded.LeaseExpiresAt);
        Assert.Null(reloaded.LockedByWorkerId);
    }
}
