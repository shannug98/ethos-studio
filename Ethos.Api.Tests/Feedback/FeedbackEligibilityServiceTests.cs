using Ethos.Api.Application.Feedback;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ethos.Api.Tests.Feedback;

public class FeedbackEligibilityServiceTests
{
    private IConfiguration CreateTestConfiguration()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "TicketSecurity:SecretKey", "test_ticket_security_secret_key_1234567890" }
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    private FeedbackEligibilityService CreateService(AppDbContext db)
    {
        return new FeedbackEligibilityService(db, NullLogger<FeedbackEligibilityService>.Instance, CreateTestConfiguration());
    }

    private AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    private (Workshop, User, StudentProfile) SeedBaseWorkshopAndStudent(AppDbContext db, string title = "Urban Dance Masterclass")
    {
        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = title,
            WorkshopDate = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Timezone = "Asia/Kolkata",
            Status = WorkshopStatus.Published,
            Capacity = 50,
            Price = 1500,
            Venue = "Ethos Studio A"
        };
        db.Workshops.Add(workshop);

        var role = new Role { Id = Guid.NewGuid(), Code = "STUDENT", Name = "Student" };
        db.Roles.Add(role);

        var user = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = $"ETHOS-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            FullName = "Rohan Sharma",
            Email = "rohan.sharma@gmail.com",
            Phone = "+919876543210",
            IsActive = true
        };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });
        db.Users.Add(user);

        var student = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user
        };
        db.StudentProfiles.Add(student);

        return (workshop, user, student);
    }

    [Fact]
    public async Task SoloPass_Attended_GeneratesTokenAndAttendedOutbox()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, user, student) = SeedBaseWorkshopAndStudent(db);

        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session 1 - Foundation",
            SessionDate = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            IsActive = true
        };
        db.WorkshopSessions.Add(session);
        workshop.Sessions.Add(session);

        var passType = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "Solo Pass",
            SessionsIncluded = 1,
            WorkshopSessionId = session.Id,
            Price = 800
        };
        db.WorkshopPassTypes.Add(passType);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = student.Id,
            Status = WorkshopBookingStatus.Confirmed,
            WorkshopPassTypeId = passType.Id,
            WorkshopPassType = passType,
            GuestName = "Rohan Sharma",
            GuestPhone = "+919876543210",
            BookedAt = DateTime.UtcNow.AddDays(-1)
        };

        var bookingSession = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopSessionId = session.Id,
            WorkshopSession = session,
            Status = WorkshopBookingSessionStatus.Booked
        };
        booking.BookingSessions.Add(bookingSession);

        var ticket = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopId = workshop.Id,
            UserId = user.Id,
            TicketNumber = "TKT-001",
            QrTokenHash = "hash123",
            AttendeeName = "Rohan Sharma",
            AttendeePhone = "+919876543210",
            IsPrimaryAttendee = true,
            Status = TicketStatus.Issued,
            CheckedInAt = DateTime.UtcNow.Date.AddHours(10).AddMinutes(15) // Checked in!
        };
        booking.Tickets.Add(ticket);

        var attendance = new WorkshopAttendance
        {
            Id = Guid.NewGuid(),
            WorkshopTicketId = ticket.Id,
            WorkshopId = workshop.Id,
            CheckedInByUserId = Guid.NewGuid(),
            FirstCheckedInAt = DateTime.UtcNow.Date.AddHours(10).AddMinutes(15),
            Method = CheckInMethod.QrScan
        };
        ticket.Attendance = attendance;
        db.WorkshopAttendances.Add(attendance);

        db.WorkshopBookings.Add(booking);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Evaluate after session end time
        var evaluationTime = session.GetSessionEndUtc(workshop.Timezone).AddMinutes(10);
        var result = await service.EvaluateWorkshopBookingsAsync(workshop.Id, evaluationTime);

        Assert.Equal(1, result.TotalBookingsEvaluated);
        Assert.Equal(1, result.EligibleAttendedCount);
        Assert.Equal(0, result.EligibleNoShowCount);
        Assert.Equal(0, result.ExcludedCount);
        Assert.Equal(1, result.TokensGeneratedCount);
        Assert.Equal(1, result.NotificationsQueuedCount);

        var token = await db.WorkshopFeedbackTokens.FirstOrDefaultAsync(t => t.WorkshopBookingId == booking.Id);
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token.TokenHash));

        var notification = await db.WhatsAppNotifications.FirstOrDefaultAsync(n => n.BookingId == booking.Id);
        Assert.NotNull(notification);
        Assert.Equal(WhatsAppNotificationType.FeedbackAttended, notification.NotificationType);
        Assert.Equal("+919876543210", notification.RecipientPhone);
        Assert.StartsWith($"feedback:{workshop.Id}:{booking.Id}:attended:v", notification.IdempotencyKey);
    }

    [Fact]
    public async Task SoloPass_NoShow_GeneratesTokenAndNoShowOutbox()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, user, student) = SeedBaseWorkshopAndStudent(db);

        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session 1 - Foundation",
            SessionDate = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            IsActive = true
        };
        db.WorkshopSessions.Add(session);
        workshop.Sessions.Add(session);

        var passType = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "Solo Pass",
            SessionsIncluded = 1,
            WorkshopSessionId = session.Id,
            Price = 800
        };
        db.WorkshopPassTypes.Add(passType);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = student.Id,
            Status = WorkshopBookingStatus.Confirmed,
            WorkshopPassTypeId = passType.Id,
            WorkshopPassType = passType,
            GuestName = "Aarav Gupta",
            GuestPhone = "+919876543211",
            BookedAt = DateTime.UtcNow.AddDays(-1)
        };

        var bookingSession = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopSessionId = session.Id,
            WorkshopSession = session,
            Status = WorkshopBookingSessionStatus.Booked
        };
        booking.BookingSessions.Add(bookingSession);

        var ticket = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopId = workshop.Id,
            UserId = user.Id,
            TicketNumber = "TKT-002",
            QrTokenHash = "hash456",
            AttendeeName = "Aarav Gupta",
            AttendeePhone = "+919876543211",
            IsPrimaryAttendee = true,
            Status = TicketStatus.Issued,
            CheckedInAt = null // No check in!
        };
        booking.Tickets.Add(ticket);

        db.WorkshopBookings.Add(booking);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var evaluationTime = session.GetSessionEndUtc(workshop.Timezone).AddMinutes(10);
        var result = await service.EvaluateWorkshopBookingsAsync(workshop.Id, evaluationTime);

        Assert.Equal(1, result.TotalBookingsEvaluated);
        Assert.Equal(0, result.EligibleAttendedCount);
        Assert.Equal(1, result.EligibleNoShowCount);
        Assert.Equal(1, result.NotificationsQueuedCount);

        var notification = await db.WhatsAppNotifications.FirstOrDefaultAsync(n => n.BookingId == booking.Id);
        Assert.NotNull(notification);
        Assert.Equal(WhatsAppNotificationType.FeedbackNoShow, notification.NotificationType);
        Assert.StartsWith($"feedback:{workshop.Id}:{booking.Id}:noshow:v", notification.IdempotencyKey);
    }

    [Fact]
    public async Task DualPass_EvaluatedOnlyAfterLatestSessionEnds()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, user, student) = SeedBaseWorkshopAndStudent(db);

        var session1 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Morning Routine",
            SessionDate = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            IsActive = true
        };

        var session2 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Afternoon Flow",
            SessionDate = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(15, 30, 0),
            IsActive = true
        };

        db.WorkshopSessions.AddRange(session1, session2);
        workshop.Sessions.Add(session1);
        workshop.Sessions.Add(session2);

        var passType = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "Dual Pass",
            SessionsIncluded = 2,
            Price = 1500
        };
        db.WorkshopPassTypes.Add(passType);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = student.Id,
            Status = WorkshopBookingStatus.Confirmed,
            WorkshopPassTypeId = passType.Id,
            WorkshopPassType = passType,
            GuestName = "Kavya Nair",
            GuestPhone = "+919876543212"
        };

        booking.BookingSessions.Add(new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopSessionId = session1.Id,
            WorkshopSession = session1,
            Status = WorkshopBookingSessionStatus.Booked
        });
        booking.BookingSessions.Add(new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopSessionId = session2.Id,
            WorkshopSession = session2,
            Status = WorkshopBookingSessionStatus.Booked
        });

        booking.Tickets.Add(new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopId = workshop.Id,
            UserId = user.Id,
            TicketNumber = "TKT-003",
            QrTokenHash = "hash789",
            AttendeeName = "Kavya Nair",
            AttendeePhone = "+919876543212",
            IsPrimaryAttendee = true,
            CheckedInAt = DateTime.UtcNow.Date.AddHours(10).AddMinutes(10) // Checked in for Session 1
        });

        db.WorkshopBookings.Add(booking);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // 1. Evaluate after Session 1 ends (e.g. 12:00 PM), but before Session 2 ends (3:30 PM)
        var timeBetweenSessions = session1.GetSessionEndUtc(workshop.Timezone).AddMinutes(30);
        var intermediateResult = await service.EvaluateWorkshopBookingsAsync(workshop.Id, timeBetweenSessions);

        Assert.Equal(1, intermediateResult.TotalBookingsEvaluated);
        Assert.Equal(1, intermediateResult.PendingNotEndedCount);
        Assert.Equal(0, intermediateResult.EligibleAttendedCount);
        Assert.Equal(0, intermediateResult.NotificationsQueuedCount);

        // 2. Evaluate after Session 2 ends (e.g. 4:00 PM)
        var timeAfterAllSessions = session2.GetSessionEndUtc(workshop.Timezone).AddMinutes(30);
        var finalResult = await service.EvaluateWorkshopBookingsAsync(workshop.Id, timeAfterAllSessions);

        Assert.Equal(1, finalResult.TotalBookingsEvaluated);
        Assert.Equal(0, finalResult.PendingNotEndedCount);
        Assert.Equal(1, finalResult.EligibleAttendedCount);
        Assert.Equal(1, finalResult.NotificationsQueuedCount);
    }

    [Fact]
    public async Task Exclusions_Cancelled_Refunded_And_TestBookings_AreExcluded()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, user, student) = SeedBaseWorkshopAndStudent(db);

        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session 1",
            SessionDate = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            IsActive = true
        };
        db.WorkshopSessions.Add(session);
        workshop.Sessions.Add(session);

        // 1. Cancelled booking
        var cancelledBooking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = student.Id,
            Status = WorkshopBookingStatus.Cancelled,
            GuestName = "Cancelled User",
            GuestPhone = "+919876543201"
        };
        cancelledBooking.BookingSessions.Add(new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = cancelledBooking.Id,
            WorkshopSessionId = session.Id,
            WorkshopSession = session,
            Status = WorkshopBookingSessionStatus.Cancelled
        });

        // 2. Refunded booking
        var refundedBooking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = student.Id,
            Status = WorkshopBookingStatus.Confirmed,
            GuestName = "Refunded User",
            GuestPhone = "+919876543202"
        };
        refundedBooking.BookingSessions.Add(new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = refundedBooking.Id,
            WorkshopSessionId = session.Id,
            WorkshopSession = session,
            Status = WorkshopBookingSessionStatus.Booked
        });
        db.PaymentRefunds.Add(new PaymentRefund
        {
            Id = Guid.NewGuid(),
            BookingId = refundedBooking.Id,
            Status = RefundStatus.Processed,
            Reason = "Customer request"
        });

        // 3. Test booking
        var testBooking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = student.Id,
            Status = WorkshopBookingStatus.Confirmed,
            GuestEmail = "mock-tester@test.com",
            GuestPhone = "+919876543203"
        };
        testBooking.BookingSessions.Add(new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = testBooking.Id,
            WorkshopSessionId = session.Id,
            WorkshopSession = session,
            Status = WorkshopBookingSessionStatus.Booked
        });

        // 4. Pending Payment booking
        var pendingBooking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = student.Id,
            Status = WorkshopBookingStatus.PendingPayment,
            GuestName = "Pending User",
            GuestPhone = "+919876543204"
        };
        pendingBooking.BookingSessions.Add(new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = pendingBooking.Id,
            WorkshopSessionId = session.Id,
            WorkshopSession = session,
            Status = WorkshopBookingSessionStatus.Booked
        });

        db.WorkshopBookings.AddRange(cancelledBooking, refundedBooking, testBooking, pendingBooking);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var evaluationTime = session.GetSessionEndUtc(workshop.Timezone).AddMinutes(10);
        var result = await service.EvaluateWorkshopBookingsAsync(workshop.Id, evaluationTime);

        Assert.Equal(4, result.TotalBookingsEvaluated);
        Assert.Equal(4, result.ExcludedCount);
        Assert.Equal(0, result.EligibleAttendedCount);
        Assert.Equal(0, result.EligibleNoShowCount);
        Assert.Equal(0, result.NotificationsQueuedCount);
    }

    [Fact]
    public async Task RepeatedExecution_IsStrictlyIdempotent()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, user, student) = SeedBaseWorkshopAndStudent(db);

        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session 1",
            SessionDate = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            IsActive = true
        };
        db.WorkshopSessions.Add(session);
        workshop.Sessions.Add(session);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = student.Id,
            Status = WorkshopBookingStatus.Confirmed,
            GuestName = "Idempotent User",
            GuestPhone = "+919876543299"
        };
        booking.BookingSessions.Add(new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopSessionId = session.Id,
            WorkshopSession = session,
            Status = WorkshopBookingSessionStatus.Booked
        });
        booking.Tickets.Add(new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopId = workshop.Id,
            UserId = user.Id,
            TicketNumber = "TKT-IDEMP",
            QrTokenHash = "hash-idemp",
            AttendeeName = "Idempotent User",
            AttendeePhone = "+919876543299",
            IsPrimaryAttendee = true,
            CheckedInAt = DateTime.UtcNow.Date.AddHours(10)
        });

        db.WorkshopBookings.Add(booking);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var evaluationTime = session.GetSessionEndUtc(workshop.Timezone).AddMinutes(10);

        // First run
        var run1 = await service.EvaluateWorkshopBookingsAsync(workshop.Id, evaluationTime);
        Assert.Equal(1, run1.NotificationsQueuedCount);
        Assert.Equal(1, run1.TokensGeneratedCount);

        // Second run
        var run2 = await service.EvaluateWorkshopBookingsAsync(workshop.Id, evaluationTime);
        Assert.Equal(0, run2.NotificationsQueuedCount); // No duplicate notifications queued
        Assert.Equal(0, run2.TokensGeneratedCount);     // No duplicate tokens generated

        var notificationCount = await db.WhatsAppNotifications.CountAsync(n => n.BookingId == booking.Id);
        Assert.Equal(1, notificationCount);

        var tokenCount = await db.WorkshopFeedbackTokens.CountAsync(t => t.WorkshopBookingId == booking.Id);
        Assert.Equal(1, tokenCount);
    }

    [Fact]
    public async Task ConfigCutoffUtc_LocksSettingAndFreezesActiveFormVersion()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, user, student) = SeedBaseWorkshopAndStudent(db);

        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session 1",
            SessionDate = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            IsActive = true
        };
        db.WorkshopSessions.Add(session);
        workshop.Sessions.Add(session);

        var setting = new WorkshopFeedbackSetting
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            IsFeedbackEnabled = true,
            IsLocked = false,
            ConfigCutoffUtc = DateTime.UtcNow.Date.AddHours(9) // 9:00 AM UTC
        };
        var version = new FeedbackFormVersion
        {
            Id = Guid.NewGuid(),
            WorkshopFeedbackSettingId = setting.Id,
            VersionNumber = 1,
            IsFrozen = false
        };
        setting.ActiveVersionId = version.Id;
        setting.ActiveVersion = version;
        setting.FormVersions.Add(version);

        db.WorkshopFeedbackSettings.Add(setting);
        db.FeedbackFormVersions.Add(version);
        workshop.FeedbackSetting = setting;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var evaluationTime = DateTime.UtcNow.Date.AddHours(9).AddMinutes(5); // Past ConfigCutoffUtc

        await service.EvaluateWorkshopBookingsAsync(workshop.Id, evaluationTime);

        Assert.True(setting.IsLocked);
        Assert.NotNull(setting.LockedAtUtc);
        Assert.True(version.IsFrozen);
        Assert.NotNull(version.FrozenAtUtc);
    }

    [Fact]
    public async Task TrioPass_EvaluatedOnlyAfterThirdSessionEnds()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, user, student) = SeedBaseWorkshopAndStudent(db);

        var s1 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session 1",
            SessionDate = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            IsActive = true
        };
        var s2 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session 2",
            SessionDate = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(12, 0, 0),
            EndTime = new TimeSpan(13, 30, 0),
            IsActive = true
        };
        var s3 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session 3",
            SessionDate = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(15, 0, 0),
            EndTime = new TimeSpan(16, 30, 0),
            IsActive = true
        };

        db.WorkshopSessions.AddRange(s1, s2, s3);
        workshop.Sessions.Add(s1);
        workshop.Sessions.Add(s2);
        workshop.Sessions.Add(s3);

        var passType = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "Trio Pass",
            SessionsIncluded = 3,
            Price = 2200
        };
        db.WorkshopPassTypes.Add(passType);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = student.Id,
            Status = WorkshopBookingStatus.Confirmed,
            WorkshopPassTypeId = passType.Id,
            WorkshopPassType = passType,
            GuestName = "Trio Student",
            GuestPhone = "+919876543233"
        };
        booking.BookingSessions.Add(new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = s1.Id, WorkshopSession = s1, Status = WorkshopBookingSessionStatus.Booked });
        booking.BookingSessions.Add(new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = s2.Id, WorkshopSession = s2, Status = WorkshopBookingSessionStatus.Booked });
        booking.BookingSessions.Add(new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = s3.Id, WorkshopSession = s3, Status = WorkshopBookingSessionStatus.Booked });

        booking.Tickets.Add(new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopId = workshop.Id,
            UserId = user.Id,
            TicketNumber = "TKT-TRIO",
            QrTokenHash = "hash-trio",
            AttendeeName = "Trio Student",
            AttendeePhone = "+919876543233",
            CheckedInAt = DateTime.UtcNow.Date.AddHours(10)
        });

        db.WorkshopBookings.Add(booking);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Before s3 ends
        var timeBeforeS3 = s2.GetSessionEndUtc(workshop.Timezone).AddMinutes(30);
        var r1 = await service.EvaluateWorkshopBookingsAsync(workshop.Id, timeBeforeS3);
        Assert.Equal(1, r1.PendingNotEndedCount);
        Assert.Equal(0, r1.NotificationsQueuedCount);

        // After s3 ends
        var timeAfterS3 = s3.GetSessionEndUtc(workshop.Timezone).AddMinutes(15);
        var r2 = await service.EvaluateWorkshopBookingsAsync(workshop.Id, timeAfterS3);
        Assert.Equal(0, r2.PendingNotEndedCount);
        Assert.Equal(1, r2.EligibleAttendedCount);
        Assert.Equal(1, r2.NotificationsQueuedCount);
    }

    [Fact]
    public async Task AllWorkshopsPass_EvaluatedAfterFinalWorkshopSession()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (workshop, user, student) = SeedBaseWorkshopAndStudent(db);

        var s1 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session 1",
            SessionDate = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            IsActive = true
        };
        var s2 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session 2",
            SessionDate = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(16, 0, 0),
            EndTime = new TimeSpan(17, 30, 0),
            IsActive = true
        };
        db.WorkshopSessions.AddRange(s1, s2);
        workshop.Sessions.Add(s1);
        workshop.Sessions.Add(s2);

        var passType = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "All Access Pass",
            SessionsIncluded = null, // All Workshops
            Price = 3000
        };
        db.WorkshopPassTypes.Add(passType);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = student.Id,
            Status = WorkshopBookingStatus.Confirmed,
            WorkshopPassTypeId = passType.Id,
            WorkshopPassType = passType,
            GuestName = "All Access Student",
            GuestPhone = "+919876543299"
        };
        booking.Tickets.Add(new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopId = workshop.Id,
            UserId = user.Id,
            TicketNumber = "TKT-ALL",
            QrTokenHash = "hash-all",
            AttendeeName = "All Access Student",
            AttendeePhone = "+919876543299",
            CheckedInAt = DateTime.UtcNow.Date.AddHours(16).AddMinutes(5)
        });

        db.WorkshopBookings.Add(booking);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Before s2 ends
        var r1 = await service.EvaluateWorkshopBookingsAsync(workshop.Id, s1.GetSessionEndUtc(workshop.Timezone).AddMinutes(30));
        Assert.Equal(1, r1.PendingNotEndedCount);
        Assert.Equal(0, r1.NotificationsQueuedCount);

        // After s2 ends
        var r2 = await service.EvaluateWorkshopBookingsAsync(workshop.Id, s2.GetSessionEndUtc(workshop.Timezone).AddMinutes(15));
        Assert.Equal(0, r2.PendingNotEndedCount);
        Assert.Equal(1, r2.EligibleAttendedCount);
        Assert.Equal(1, r2.NotificationsQueuedCount);
    }

    [Fact]
    public async Task ProcessCompletedWorkshopsAsync_AggregatesAcrossEligibleWorkshops()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (w1, u1, s1) = SeedBaseWorkshopAndStudent(db, "Workshop 1");
        var (w2, u2, s2) = SeedBaseWorkshopAndStudent(db, "Workshop 2");

        var session1 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = w1.Id,
            Title = "W1 Session",
            SessionDate = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            IsActive = true
        };
        var session2 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = w2.Id,
            Title = "W2 Session",
            SessionDate = DateTime.UtcNow.Date,
            StartTime = new TimeSpan(11, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            IsActive = true
        };
        db.WorkshopSessions.AddRange(session1, session2);
        w1.Sessions.Add(session1);
        w2.Sessions.Add(session2);

        var b1 = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = w1.Id,
            StudentProfileId = s1.Id,
            Status = WorkshopBookingStatus.Confirmed,
            GuestPhone = "+919876543201"
        };
        b1.BookingSessions.Add(new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = b1.Id, WorkshopSessionId = session1.Id, WorkshopSession = session1, Status = WorkshopBookingSessionStatus.Booked });
        b1.Tickets.Add(new WorkshopTicket { Id = Guid.NewGuid(), WorkshopBookingId = b1.Id, WorkshopId = w1.Id, UserId = u1.Id, TicketNumber = "T1", QrTokenHash = "q1", AttendeeName = "User 1", CheckedInAt = DateTime.UtcNow.Date.AddHours(9) });

        var b2 = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = w2.Id,
            StudentProfileId = s2.Id,
            Status = WorkshopBookingStatus.Confirmed,
            GuestPhone = "+919876543202"
        };
        b2.BookingSessions.Add(new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = b2.Id, WorkshopSessionId = session2.Id, WorkshopSession = session2, Status = WorkshopBookingSessionStatus.Booked });
        b2.Tickets.Add(new WorkshopTicket { Id = Guid.NewGuid(), WorkshopBookingId = b2.Id, WorkshopId = w2.Id, UserId = u2.Id, TicketNumber = "T2", QrTokenHash = "q2", AttendeeName = "User 2" /* No check in */ });

        db.WorkshopBookings.AddRange(b1, b2);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var evaluationTime = session2.GetSessionEndUtc(w2.Timezone).AddMinutes(30);

        var result = await service.ProcessCompletedWorkshopsAsync(evaluationTime);

        Assert.True(result.WorkshopsProcessed >= 2);
        Assert.Equal(2, result.TotalBookingsEvaluated);
        Assert.Equal(1, result.EligibleAttendedCount);
        Assert.Equal(1, result.EligibleNoShowCount);
        Assert.Equal(2, result.TokensGeneratedCount);
        Assert.Equal(2, result.NotificationsQueuedCount);
    }
}
