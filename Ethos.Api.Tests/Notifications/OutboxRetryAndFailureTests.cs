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

public class OutboxRetryAndFailureTests
{
    private AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    private class MockMsg91TransientService : IMsg91WhatsAppService
    {
        public Task<Msg91DispatchResult> SendBookingConfirmedAsync(BookingConfirmedData data, string recipientPhone, CancellationToken cancellationToken = default) =>
            Task.FromResult(Msg91DispatchResult.Transient(503, "MSG91 Gateway Unavailable"));

        public Task<Msg91DispatchResult> SendTicketPdfAsync(TicketPdfData data, string recipientPhone, CancellationToken cancellationToken = default) =>
            Task.FromResult(Msg91DispatchResult.Transient(503, "MSG91 Gateway Unavailable"));

        public bool TryNormalizePhoneNumber(string? rawPhone, out string normalizedPhone, out string? failureReason, string defaultCountryCode = "91")
        {
            normalizedPhone = "919876543210";
            failureReason = null;
            return true;
        }

        public string BuildBookingConfirmedJson(BookingConfirmedData data, string recipientPhone) => "{}";
        public string BuildTicketPdfJson(TicketPdfData data, string recipientPhone) => "{}";
    }

    private class DummyTicketPdfService : ITicketPdfService
    {
        public Task<TicketPdfResult> GetOrCreateTicketPdfAsync(WorkshopTicket ticket, Workshop workshop, WorkshopBooking booking, string? rawQrToken = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TicketPdfResult(true, "tickets/sample.pdf", "https://media.ethosdancestudio.com/tickets/sample.pdf", "hash"));
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenTransientFailureOccurs_IncrementsAttemptsAndSetsNextAttemptBackoff()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var msg91 = new MockMsg91TransientService();
        var pdfService = new DummyTicketPdfService();
        var options = Options.Create(new Msg91Options { BatchSize = 10, MaxRetryAttempts = 3 });

        var dispatcher = new WhatsAppOutboxDispatcher(db, msg91, pdfService, options, NullLogger<WhatsAppOutboxDispatcher>.Instance);

        var bookingId = Guid.NewGuid();
        var workshop = new Workshop { Id = Guid.NewGuid(), Title = "Hip Hop", WorkshopDate = DateTime.UtcNow.AddDays(1), StartTime = TimeSpan.FromHours(18), EndTime = TimeSpan.FromHours(20) };
        var booking = new WorkshopBooking { Id = bookingId, Workshop = workshop, GuestName = "Maya", GuestPhone = "9876543210" };

        db.Workshops.Add(workshop);
        db.WorkshopBookings.Add(booking);

        var notification = new WhatsAppNotification
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            NotificationType = WhatsAppNotificationType.BookingConfirmed,
            RecipientPhone = "9876543210",
            IdempotencyKey = $"wapp_booking_{bookingId}",
            Status = WhatsAppNotificationStatus.Pending,
            Attempts = 0
        };
        db.WhatsAppNotifications.Add(notification);
        await db.SaveChangesAsync();

        // Run 1st attempt
        await dispatcher.ProcessPendingBatchAsync("worker-1");

        var record1 = await db.WhatsAppNotifications.FindAsync(notification.Id);
        Assert.NotNull(record1);
        Assert.Equal(WhatsAppNotificationStatus.Failed, record1.Status);
        Assert.Equal(1, record1.Attempts);
        Assert.NotNull(record1.NextAttemptAt);
        Assert.True(record1.NextAttemptAt > DateTime.UtcNow);

        // Advance NextAttemptAt to test 2nd attempt
        record1.NextAttemptAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        await dispatcher.ProcessPendingBatchAsync("worker-1");

        var record2 = await db.WhatsAppNotifications.FindAsync(notification.Id);
        Assert.NotNull(record2);
        Assert.Equal(2, record2.Attempts);
        Assert.NotNull(record2.NextAttemptAt);

        // Advance NextAttemptAt to test 3rd (and final) attempt
        record2.NextAttemptAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        await dispatcher.ProcessPendingBatchAsync("worker-1");

        var record3 = await db.WhatsAppNotifications.FindAsync(notification.Id);
        Assert.NotNull(record3);
        Assert.Equal(3, record3.Attempts);
        // Exceeded MaxRetryAttempts: NextAttemptAt must be null (no infinite retry)
        Assert.Null(record3.NextAttemptAt);
        Assert.Equal(WhatsAppNotificationStatus.Failed, record3.Status);
    }

    [Fact]
    public async Task PaymentAndBooking_RemainConfirmed_WhenWhatsAppOutboxFails()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var msg91 = new MockMsg91TransientService();
        var pdfService = new DummyTicketPdfService();
        var options = Options.Create(new Msg91Options { BatchSize = 10, MaxRetryAttempts = 3 });

        var dispatcher = new WhatsAppOutboxDispatcher(db, msg91, pdfService, options, NullLogger<WhatsAppOutboxDispatcher>.Instance);

        var bookingId = Guid.NewGuid();
        var workshop = new Workshop { Id = Guid.NewGuid(), Title = "Hip Hop", WorkshopDate = DateTime.UtcNow.AddDays(1), StartTime = TimeSpan.FromHours(18), EndTime = TimeSpan.FromHours(20) };
        var booking = new WorkshopBooking
        {
            Id = bookingId,
            Workshop = workshop,
            GuestName = "Maya",
            GuestPhone = "9876543210",
            Status = WorkshopBookingStatus.Confirmed // Booking already confirmed
        };
        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Paid,
            Amount = 1000
        };

        db.Workshops.Add(workshop);
        db.WorkshopBookings.Add(booking);
        db.PaymentTransactions.Add(transaction);

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

        // Process outbox (which encounters transient failure)
        await dispatcher.ProcessPendingBatchAsync("worker-1");

        // Assert booking and payment are still 100% intact and confirmed!
        var verifiedBooking = await db.WorkshopBookings.FindAsync(bookingId);
        Assert.NotNull(verifiedBooking);
        Assert.Equal(WorkshopBookingStatus.Confirmed, verifiedBooking.Status);

        var verifiedTx = await db.PaymentTransactions.FindAsync(transaction.Id);
        Assert.NotNull(verifiedTx);
        Assert.Equal(PaymentStatus.Paid, verifiedTx.Status);
    }
}
