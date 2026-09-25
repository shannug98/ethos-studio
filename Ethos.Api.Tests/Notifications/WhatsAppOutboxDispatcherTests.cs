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

        public List<BookingConfirmedData> DispatchedBookings { get; } = new();
        public List<TicketPdfData> DispatchedTickets { get; } = new();

        public Task<Msg91DispatchResult> SendBookingConfirmedAsync(BookingConfirmedData data, string recipientPhone, CancellationToken cancellationToken = default)
        {
            DispatchedBookings.Add(data);
            return Task.FromResult(BookingHandler(data, recipientPhone));
        }

        public Task<Msg91DispatchResult> SendTicketPdfAsync(TicketPdfData data, string recipientPhone, CancellationToken cancellationToken = default)
        {
            DispatchedTickets.Add(data);
            return Task.FromResult(TicketHandler(data, recipientPhone));
        }

        public Task<Msg91DispatchResult> SendAdminPasswordResetAsync(string resetUrl, string recipientPhone, CancellationToken cancellationToken = default) =>
            Task.FromResult(Msg91DispatchResult.Accepted("msg_reset_123", "req_reset_123"));

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
        public Func<WorkshopTicket, Workshop, WorkshopBooking, TicketPdfResult>? Handler { get; set; }

        public Task<TicketPdfResult> GetOrCreateTicketPdfAsync(
            WorkshopTicket ticket,
            Workshop workshop,
            WorkshopBooking booking,
            string? rawQrToken = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Handler != null)
            {
                return Task.FromResult(Handler(ticket, workshop, booking));
            }

            return Task.FromResult(new TicketPdfResult(
                Success: true,
                StorageKey: $"tickets/{ticket.TicketNumber}.pdf",
                SignedHttpsUrl: $"https://media.ethosdancestudio.com/tickets/{ticket.TicketNumber}.pdf?token=valid_signed_jwt",
                FileHash: "dummyhash123"));
        }
    }

    [Fact]
    public void ResolveWhatsAppLocation_Permutations_FormatsExpectedBrandingAndAddress()
    {
        // 1. Default venue and city
        var w1 = new Workshop { Venue = "Ethos Dance Studio", City = "Hyderabad" };
        Assert.Equal("Ethos Dance Studio, Hyderabad", WhatsAppOutboxDispatcher.ResolveWhatsAppLocation(w1));

        // 2. Specific venue with address
        var w2 = new Workshop { Venue = "Main Studio", VenueAddress = "Road 36, Jubilee Hills, Hyderabad" };
        Assert.Equal("Ethos Dance Studio, Main Studio, Road 36, Jubilee Hills, Hyderabad", WhatsAppOutboxDispatcher.ResolveWhatsAppLocation(w2));

        // 3. Ethos branded venue with city
        var w3 = new Workshop { Venue = "Ethos Dance Studio, Jubilee Hills", City = "Hyderabad" };
        Assert.Equal("Ethos Dance Studio, Jubilee Hills, Hyderabad", WhatsAppOutboxDispatcher.ResolveWhatsAppLocation(w3));

        // 4. Null venue with address
        var w4 = new Workshop { Venue = null!, VenueAddress = "Kavuri Hills, Hyderabad" };
        Assert.Equal("Ethos Dance Studio, Kavuri Hills, Hyderabad", WhatsAppOutboxDispatcher.ResolveWhatsAppLocation(w4));

        // 5. Already fully qualified location
        var w5 = new Workshop { Venue = "Ethos Dance Studio", VenueAddress = "Ethos Dance Studio, Madhapur, Hyderabad" };
        Assert.Equal("Ethos Dance Studio, Madhapur, Hyderabad", WhatsAppOutboxDispatcher.ResolveWhatsAppLocation(w5));
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_BookingConfirmed_PropagatesAuthoritativeLocation()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var msg91 = new MockMsg91Service();
        var pdfService = new MockTicketPdfService();
        var options = Options.Create(new Msg91Options { BatchSize = 10, MaxRetryAttempts = 3 });

        var dispatcher = new WhatsAppOutboxDispatcher(db, msg91, pdfService, options, NullLogger<WhatsAppOutboxDispatcher>.Instance);

        var bookingId = Guid.NewGuid();
        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Contemporary Dance",
            WorkshopDate = new DateTime(2026, 11, 15),
            StartTime = new TimeSpan(16, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            Venue = "Ethos Dance Studio",
            VenueAddress = "Jubilee Hills, Hyderabad"
        };
        var booking = new WorkshopBooking
        {
            Id = bookingId,
            Workshop = workshop,
            GuestName = "Ananya Singh",
            GuestPhone = "9876543210"
        };

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
        Assert.Single(msg91.DispatchedBookings);

        var dispatched = msg91.DispatchedBookings[0];
        Assert.Equal("Ananya Singh", dispatched.AttendeeName);
        Assert.Equal("Contemporary Dance", dispatched.WorkshopTitle);
        Assert.Equal("15 November 2026", dispatched.WorkshopDate);
        Assert.Equal("4:00 PM – 6:00 PM", dispatched.WorkshopTime);
        Assert.Equal("Ethos Dance Studio, Jubilee Hills, Hyderabad", dispatched.Location);
        Assert.Equal("BK-" + bookingId.ToString()[..8].ToUpperInvariant(), dispatched.BookingId);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_TicketPdf_UsesSessionSpecificDateTimeAndAuthoritativeLocation()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var msg91 = new MockMsg91Service();
        var pdfService = new MockTicketPdfService();
        var options = Options.Create(new Msg91Options { BatchSize = 10, MaxRetryAttempts = 3 });

        var dispatcher = new WhatsAppOutboxDispatcher(db, msg91, pdfService, options, NullLogger<WhatsAppOutboxDispatcher>.Instance);

        var workshopId = Guid.NewGuid();
        var workshop = new Workshop
        {
            Id = workshopId,
            Title = "Masterclass Weekend",
            WorkshopDate = new DateTime(2026, 10, 1),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Venue = "Ethos Dance Studio",
            City = "Hyderabad"
        };
        db.Workshops.Add(workshop);

        // Session 1: Saturday evening
        var session1 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            Title = "Session 1 - Foundation",
            SessionDate = new DateTime(2026, 10, 10),
            StartTime = new TimeSpan(17, 0, 0),
            EndTime = new TimeSpan(18, 0, 0)
        };
        // Session 2: Sunday evening
        var session2 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            Title = "Session 2 - Advanced",
            SessionDate = new DateTime(2026, 10, 11),
            StartTime = new TimeSpan(19, 0, 0),
            EndTime = new TimeSpan(20, 0, 0)
        };
        db.WorkshopSessions.AddRange(session1, session2);

        var bookingId = Guid.NewGuid();
        var booking = new WorkshopBooking
        {
            Id = bookingId,
            WorkshopId = workshopId,
            Workshop = workshop,
            GuestName = "Vikram Patel",
            GuestPhone = "9876543210"
        };
        db.WorkshopBookings.Add(booking);

        // Ticket 1 in Session 1
        var ticket1 = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            WorkshopBookingId = bookingId,
            WorkshopBooking = booking,
            Workshop = workshop,
            WorkshopSessionId = session1.Id,
            WorkshopSession = session1,
            TicketNumber = "ETHOS-TKT-S1",
            AttendeeName = "Vikram Patel",
            QrTokenHash = "hash1"
        };

        // Ticket 2 in Session 2
        var ticket2 = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            WorkshopBookingId = bookingId,
            WorkshopBooking = booking,
            Workshop = workshop,
            WorkshopSessionId = session2.Id,
            WorkshopSession = session2,
            TicketNumber = "ETHOS-TKT-S2",
            AttendeeName = "Vikram Patel",
            QrTokenHash = "hash2"
        };

        // Ticket 3 with No Session (fallback to workshop root date/time)
        var ticket3 = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            WorkshopBookingId = bookingId,
            WorkshopBooking = booking,
            Workshop = workshop,
            WorkshopSessionId = null,
            WorkshopSession = null,
            TicketNumber = "ETHOS-TKT-ROOT",
            AttendeeName = "Vikram Patel",
            QrTokenHash = "hash3"
        };

        db.WorkshopTickets.AddRange(ticket1, ticket2, ticket3);

        db.WhatsAppNotifications.AddRange(
            new WhatsAppNotification
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                WorkshopTicketId = ticket1.Id,
                WorkshopTicket = ticket1,
                WorkshopBooking = booking,
                NotificationType = WhatsAppNotificationType.TicketPdf,
                RecipientPhone = "9876543210",
                IdempotencyKey = $"wapp_ticket_{ticket1.Id}",
                Status = WhatsAppNotificationStatus.Pending
            },
            new WhatsAppNotification
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                WorkshopTicketId = ticket2.Id,
                WorkshopTicket = ticket2,
                WorkshopBooking = booking,
                NotificationType = WhatsAppNotificationType.TicketPdf,
                RecipientPhone = "9876543210",
                IdempotencyKey = $"wapp_ticket_{ticket2.Id}",
                Status = WhatsAppNotificationStatus.Pending
            },
            new WhatsAppNotification
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                WorkshopTicketId = ticket3.Id,
                WorkshopTicket = ticket3,
                WorkshopBooking = booking,
                NotificationType = WhatsAppNotificationType.TicketPdf,
                RecipientPhone = "9876543210",
                IdempotencyKey = $"wapp_ticket_{ticket3.Id}",
                Status = WhatsAppNotificationStatus.Pending
            }
        );

        await db.SaveChangesAsync();

        var processed = await dispatcher.ProcessPendingBatchAsync("worker-1");

        Assert.Equal(3, processed);
        Assert.Equal(3, msg91.DispatchedTickets.Count);

        // Ticket 1: Session 1 date and time
        var t1Data = msg91.DispatchedTickets.Single(t => t.FileName == "ETHOS-TKT-S1.pdf");
        Assert.Equal("10 October 2026", t1Data.WorkshopDate);
        Assert.Equal("5:00 PM – 6:00 PM", t1Data.WorkshopTime);
        Assert.Equal("Ethos Dance Studio, Hyderabad", t1Data.Location);
        Assert.Equal("BK-" + bookingId.ToString()[..8].ToUpperInvariant(), t1Data.BookingId);

        // Ticket 2: Session 2 date and time
        var t2Data = msg91.DispatchedTickets.Single(t => t.FileName == "ETHOS-TKT-S2.pdf");
        Assert.Equal("11 October 2026", t2Data.WorkshopDate);
        Assert.Equal("7:00 PM – 8:00 PM", t2Data.WorkshopTime);
        Assert.Equal("Ethos Dance Studio, Hyderabad", t2Data.Location);

        // Ticket 3: Fallback root workshop date and time
        var t3Data = msg91.DispatchedTickets.Single(t => t.FileName == "ETHOS-TKT-ROOT.pdf");
        Assert.Equal("01 October 2026", t3Data.WorkshopDate);
        Assert.Equal("10:00 AM – 12:00 PM", t3Data.WorkshopTime);
        Assert.Equal("Ethos Dance Studio, Hyderabad", t3Data.Location);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenPdfServiceFailsOrReturnsNonHttps_FailsNotificationAndRetriesLater()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var msg91 = new MockMsg91Service();
        var pdfService = new MockTicketPdfService
        {
            Handler = (t, w, b) => new TicketPdfResult(
                Success: false,
                StorageKey: string.Empty,
                SignedHttpsUrl: null,
                FileHash: string.Empty,
                ErrorMessage: "Cloudflare R2 is unreachable or signed URL is non-HTTPS.")
        };
        var options = Options.Create(new Msg91Options { BatchSize = 10, MaxRetryAttempts = 3 });

        var dispatcher = new WhatsAppOutboxDispatcher(db, msg91, pdfService, options, NullLogger<WhatsAppOutboxDispatcher>.Instance);

        var bookingId = Guid.NewGuid();
        var workshop = new Workshop { Id = Guid.NewGuid(), Title = "Hip Hop", WorkshopDate = DateTime.UtcNow.AddDays(2), StartTime = TimeSpan.FromHours(18), EndTime = TimeSpan.FromHours(20) };
        var booking = new WorkshopBooking { Id = bookingId, Workshop = workshop, GuestName = "Test Guest", GuestPhone = "9876543210" };
        var ticket = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            WorkshopBookingId = bookingId,
            WorkshopBooking = booking,
            Workshop = workshop,
            TicketNumber = "ETHOS-TKT-ERR",
            AttendeeName = "Test Guest",
            QrTokenHash = "hash"
        };

        db.Workshops.Add(workshop);
        db.WorkshopBookings.Add(booking);
        db.WorkshopTickets.Add(ticket);

        var notification = new WhatsAppNotification
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            WorkshopTicketId = ticket.Id,
            WorkshopTicket = ticket,
            WorkshopBooking = booking,
            NotificationType = WhatsAppNotificationType.TicketPdf,
            RecipientPhone = "9876543210",
            IdempotencyKey = $"wapp_ticket_{ticket.Id}",
            Status = WhatsAppNotificationStatus.Pending
        };
        db.WhatsAppNotifications.Add(notification);
        await db.SaveChangesAsync();

        var processed = await dispatcher.ProcessPendingBatchAsync("worker-1");

        // Record failed and scheduled for retry
        var updated = await db.WhatsAppNotifications.FindAsync(notification.Id);
        Assert.NotNull(updated);
        Assert.Equal(WhatsAppNotificationStatus.Failed, updated.Status);
        Assert.NotNull(updated.NextAttemptAt);
        Assert.Contains("Cloudflare R2 is unreachable", updated.LastError);
        Assert.Empty(msg91.DispatchedTickets); // MSG91 never called with invalid/missing PDF
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

    [Fact]
    public async Task ProcessPendingBatchAsync_StudentBooking_DispatchesStudentUserFullNameAsAttendeeName()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var msg91 = new MockMsg91Service();
        var pdfService = new MockTicketPdfService();
        var options = Options.Create(new Msg91Options());

        var dispatcher = new WhatsAppOutboxDispatcher(db, msg91, pdfService, options, NullLogger<WhatsAppOutboxDispatcher>.Instance);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Masterclass",
            WorkshopDate = DateTime.Today.AddDays(5),
            StartTime = new TimeSpan(18, 0, 0),
            EndTime = new TimeSpan(20, 0, 0),
            Venue = "Ethos Dance Studio",
            City = "Hyderabad"
        };
        db.Workshops.Add(workshop);

        var role = new Role { Id = Guid.NewGuid(), Code = "STUDENT", Name = "Student" };
        db.Roles.Add(role);

        var user = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "STU-001",
            FullName = "Rahul Sharma",
            Phone = "9876543210"
        };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        db.Users.Add(user);

        var studentProfile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user
        };
        db.StudentProfiles.Add(studentProfile);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = studentProfile.Id,
            StudentProfile = studentProfile,
            GuestName = null, // Authenticated student booking
            GuestPhone = null,
            PassName = "Solo Pass",
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);

        var notification = new WhatsAppNotification
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            NotificationType = WhatsAppNotificationType.BookingConfirmed,
            RecipientPhone = "9876543210",
            IdempotencyKey = $"wapp_booking_{booking.Id}",
            Status = WhatsAppNotificationStatus.Pending
        };
        db.WhatsAppNotifications.Add(notification);
        await db.SaveChangesAsync();

        await dispatcher.ProcessPendingBatchAsync("worker-1");

        Assert.Single(msg91.DispatchedBookings);
        Assert.Equal("Rahul Sharma", msg91.DispatchedBookings[0].AttendeeName);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_GuestBooking_DispatchesGuestNameAsAttendeeName()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var msg91 = new MockMsg91Service();
        var pdfService = new MockTicketPdfService();
        var options = Options.Create(new Msg91Options());

        var dispatcher = new WhatsAppOutboxDispatcher(db, msg91, pdfService, options, NullLogger<WhatsAppOutboxDispatcher>.Instance);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Masterclass",
            WorkshopDate = DateTime.Today.AddDays(5),
            StartTime = new TimeSpan(18, 0, 0),
            EndTime = new TimeSpan(20, 0, 0),
            Venue = "Ethos Dance Studio",
            City = "Hyderabad"
        };
        db.Workshops.Add(workshop);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = Guid.Empty,
            GuestName = "Guest Person",
            GuestPhone = "9876543210",
            PassName = "Solo Pass",
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);

        var notification = new WhatsAppNotification
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            NotificationType = WhatsAppNotificationType.BookingConfirmed,
            RecipientPhone = "9876543210",
            IdempotencyKey = $"wapp_booking_{booking.Id}",
            Status = WhatsAppNotificationStatus.Pending
        };
        db.WhatsAppNotifications.Add(notification);
        await db.SaveChangesAsync();

        await dispatcher.ProcessPendingBatchAsync("worker-1");

        Assert.Single(msg91.DispatchedBookings);
        Assert.Equal("Guest Person", msg91.DispatchedBookings[0].AttendeeName);
    }
}
