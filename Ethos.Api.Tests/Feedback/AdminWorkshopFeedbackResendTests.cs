using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Feedback;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Feedback;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ethos.Api.Tests.Feedback;

public class AdminWorkshopFeedbackResendTests
{
    private sealed class DummyAuditService : IAdminAuditService
    {
        public void AddAuditLog(
            Guid adminUserId, string actionType, string entityType, Guid entityId,
            string? reason, string category = "OPERATIONS", Guid? adminDeviceId = null,
            Guid? adminSessionId = null, string? traceId = null, string? ipAddress = null,
            string? userAgent = null, string? metadataJson = null) { }

        public Task LogActionAsync(
            Guid adminUserId, string actionType, string category, string entityType,
            Guid entityId, bool success = true, string? outcomeCode = null, string? reason = null,
            Guid? adminDeviceId = null, Guid? adminSessionId = null, string? traceId = null,
            string? requestId = null, string? ipAddress = null, string? userAgent = null,
            object? metadata = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task LogSecurityEventAsync(
            string eventType, string severity, string? ipAddress, string? userAgent,
            Guid? userId = null, Guid? adminDeviceId = null, Guid? adminSessionId = null,
            string? traceId = null, string? maskedPhone = null, object? details = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PagedResult<AdminAuditLogResponse>> GetAuditLogsAsync(
            int page, int pageSize, string? category = null, string? actionType = null,
            string? entityType = null, Guid? entityId = null, Guid? adminUserId = null,
            string? traceId = null, DateTime? startDate = null, DateTime? endDate = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<AdminAuditLogResponse> { Items = new List<AdminAuditLogResponse>(), Page = 1, PageSize = 10, TotalCount = 0 });

        public Task<AdminAuditLogResponse?> GetAuditLogByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<AdminAuditLogResponse?>(null);

        public Task<PagedResult<AdminSecurityEventResponse>> GetSecurityEventsAsync(
            int page, int pageSize, string? eventType = null, string? severity = null,
            Guid? userId = null, string? traceId = null, DateTime? startDate = null,
            DateTime? endDate = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<AdminSecurityEventResponse> { Items = new List<AdminSecurityEventResponse>(), Page = 1, PageSize = 10, TotalCount = 0 });

        public Task<AdminSecurityEventResponse?> GetSecurityEventByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<AdminSecurityEventResponse?>(null);
    }

    private static IConfiguration CreateTestConfiguration()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "TicketSecurity:SecretKey", "test_ticket_security_secret_key_1234567890" }
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    private static AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    private static (Workshop Workshop, WorkshopBooking AttendedBooking, WorkshopBooking NoShowBooking) SeedWorkshopWithBookings(AppDbContext db)
    {
        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Contemporary Masterclass",
            WorkshopDate = DateTime.UtcNow.Date.AddDays(-1),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Status = WorkshopStatus.Completed,
            Capacity = 50,
            Price = 1500,
            Venue = "Ethos Studio"
        };
        db.Workshops.Add(workshop);

        // Booking 1: Attended
        var attendedBooking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            GuestName = "Rahul Sharma",
            GuestPhone = "+919876543210",
            Status = WorkshopBookingStatus.Confirmed,
            BookedAt = DateTime.UtcNow.AddDays(-2)
        };
        var attendedTicket = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = attendedBooking.Id,
            WorkshopId = workshop.Id,
            TicketNumber = "TKT-ATT-01",
            QrTokenHash = "qr_token_hash_att_01",
            AttendeeName = "Rahul Sharma",
            AttendeePhone = "+919876543210",
            Status = TicketStatus.Issued,
            CheckedInAt = DateTime.UtcNow.AddDays(-1).AddHours(10), // Checked in!
            Attendance = new WorkshopAttendance { Id = Guid.NewGuid(), FirstCheckedInAt = DateTime.UtcNow.AddDays(-1).AddHours(10) }
        };
        attendedBooking.Tickets.Add(attendedTicket);
        db.WorkshopBookings.Add(attendedBooking);

        // Booking 2: No-Show
        var noShowBooking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            GuestName = "Priya Patel",
            GuestPhone = "+919876543211",
            Status = WorkshopBookingStatus.Confirmed,
            BookedAt = DateTime.UtcNow.AddDays(-2)
        };
        var noShowTicket = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = noShowBooking.Id,
            WorkshopId = workshop.Id,
            TicketNumber = "TKT-NOSHOW-01",
            QrTokenHash = "qr_token_hash_noshow_01",
            AttendeeName = "Priya Patel",
            AttendeePhone = "+919876543211",
            Status = TicketStatus.Issued,
            CheckedInAt = null // Not checked in!
        };
        noShowBooking.Tickets.Add(noShowTicket);
        db.WorkshopBookings.Add(noShowBooking);

        db.SaveChanges();
        return (workshop, attendedBooking, noShowBooking);
    }

    [Fact]
    public async Task IndividualResend_AttendedParticipant_QueuesAttendedNotificationAndReusesToken()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, attendedBooking, noShowBooking) = SeedWorkshopWithBookings(db);
        var service = new AdminWorkshopService(db, new DummyAuditService(), configuration: CreateTestConfiguration());

        var response = await service.ResendWorkshopFeedbackAsync(workshop.Id, new AdminResendFeedbackRequest
        {
            Audience = "All",
            RecipientBookingIds = new List<Guid> { attendedBooking.Id }
        }, CancellationToken.None);

        Assert.Equal(1, response.QueuedCount);
        Assert.Equal(0, response.SkippedCount);

        var notifications = await db.WhatsAppNotifications.ToListAsync();
        Assert.Single(notifications);
        Assert.Equal(attendedBooking.Id, notifications[0].BookingId);
        Assert.Equal(WhatsAppNotificationType.FeedbackAttended, notifications[0].NotificationType);
        Assert.Equal("+919876543210", notifications[0].RecipientPhone);

        var token = await db.WorkshopFeedbackTokens.FirstOrDefaultAsync(t => t.WorkshopBookingId == attendedBooking.Id);
        Assert.NotNull(token);
        Assert.Equal(FeedbackAudienceType.Attended, token.AudienceType);
    }

    [Fact]
    public async Task IndividualResend_NoShowParticipant_QueuesNoShowNotificationAndReusesToken()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, attendedBooking, noShowBooking) = SeedWorkshopWithBookings(db);
        var service = new AdminWorkshopService(db, new DummyAuditService(), configuration: CreateTestConfiguration());

        var response = await service.ResendWorkshopFeedbackAsync(workshop.Id, new AdminResendFeedbackRequest
        {
            Audience = "All",
            RecipientBookingIds = new List<Guid> { noShowBooking.Id }
        }, CancellationToken.None);

        Assert.Equal(1, response.QueuedCount);
        Assert.Equal(0, response.SkippedCount);

        var notifications = await db.WhatsAppNotifications.ToListAsync();
        Assert.Single(notifications);
        Assert.Equal(noShowBooking.Id, notifications[0].BookingId);
        Assert.Equal(WhatsAppNotificationType.FeedbackNoShow, notifications[0].NotificationType);
        Assert.Equal("+919876543211", notifications[0].RecipientPhone);

        var token = await db.WorkshopFeedbackTokens.FirstOrDefaultAsync(t => t.WorkshopBookingId == noShowBooking.Id);
        Assert.NotNull(token);
        Assert.Equal(FeedbackAudienceType.NoShow, token.AudienceType);
    }

    [Fact]
    public async Task BulkResend_BothAudience_QueuesTailoredNotificationsForEachAudience()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, attendedBooking, noShowBooking) = SeedWorkshopWithBookings(db);
        var service = new AdminWorkshopService(db, new DummyAuditService(), configuration: CreateTestConfiguration());

        var response = await service.ResendWorkshopFeedbackAsync(workshop.Id, new AdminResendFeedbackRequest
        {
            Audience = "All"
        }, CancellationToken.None);

        Assert.Equal(2, response.QueuedCount);
        Assert.Equal(0, response.SkippedCount);

        var notifications = await db.WhatsAppNotifications.ToListAsync();
        Assert.Equal(2, notifications.Count);

        var attNotif = notifications.FirstOrDefault(n => n.BookingId == attendedBooking.Id);
        Assert.NotNull(attNotif);
        Assert.Equal(WhatsAppNotificationType.FeedbackAttended, attNotif.NotificationType);

        var noShowNotif = notifications.FirstOrDefault(n => n.BookingId == noShowBooking.Id);
        Assert.NotNull(noShowNotif);
        Assert.Equal(WhatsAppNotificationType.FeedbackNoShow, noShowNotif.NotificationType);
    }

    [Fact]
    public async Task BulkResend_AttendedOnly_QueuesOnlyAttendedAndSkipsNoShow()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, attendedBooking, noShowBooking) = SeedWorkshopWithBookings(db);
        var service = new AdminWorkshopService(db, new DummyAuditService(), configuration: CreateTestConfiguration());

        var response = await service.ResendWorkshopFeedbackAsync(workshop.Id, new AdminResendFeedbackRequest
        {
            Audience = "Attended"
        }, CancellationToken.None);

        Assert.Equal(1, response.QueuedCount);
        Assert.Equal(1, response.SkippedCount);

        var notifications = await db.WhatsAppNotifications.ToListAsync();
        Assert.Single(notifications);
        Assert.Equal(attendedBooking.Id, notifications[0].BookingId);
        Assert.Equal(WhatsAppNotificationType.FeedbackAttended, notifications[0].NotificationType);
    }

    [Fact]
    public async Task BulkResend_NoShowOnly_QueuesOnlyNoShowAndSkipsAttended()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, attendedBooking, noShowBooking) = SeedWorkshopWithBookings(db);
        var service = new AdminWorkshopService(db, new DummyAuditService(), configuration: CreateTestConfiguration());

        var response = await service.ResendWorkshopFeedbackAsync(workshop.Id, new AdminResendFeedbackRequest
        {
            Audience = "NoShow"
        }, CancellationToken.None);

        Assert.Equal(1, response.QueuedCount);
        Assert.Equal(1, response.SkippedCount);

        var notifications = await db.WhatsAppNotifications.ToListAsync();
        Assert.Single(notifications);
        Assert.Equal(noShowBooking.Id, notifications[0].BookingId);
        Assert.Equal(WhatsAppNotificationType.FeedbackNoShow, notifications[0].NotificationType);
    }

    [Fact]
    public async Task Resend_AlreadySubmittedBooking_IsSkipped()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, attendedBooking, _) = SeedWorkshopWithBookings(db);

        // Record a submitted feedback for attendedBooking
        db.WorkshopFeedbacks.Add(new WorkshopFeedback
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            WorkshopBookingId = attendedBooking.Id,
            Rating = 5,
            SubmittedAt = DateTime.UtcNow,
            IsValid = true
        });
        db.SaveChanges();

        var service = new AdminWorkshopService(db, new DummyAuditService(), configuration: CreateTestConfiguration());

        var response = await service.ResendWorkshopFeedbackAsync(workshop.Id, new AdminResendFeedbackRequest
        {
            Audience = "All",
            RecipientBookingIds = new List<Guid> { attendedBooking.Id }
        }, CancellationToken.None);

        Assert.Equal(0, response.QueuedCount);
        Assert.Equal(1, response.SkippedCount);

        var notifications = await db.WhatsAppNotifications.ToListAsync();
        Assert.Empty(notifications);
    }
}
