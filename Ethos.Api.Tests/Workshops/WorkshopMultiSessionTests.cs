using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Trainers;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Exceptions;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class WorkshopMultiSessionTests
{
    private class DummyAuditService : IAdminAuditService
    {
        public void AddAuditLog(
            Guid adminUserId, string actionType, string entityType, Guid entityId,
            string? reason, string category = "OPERATIONS", Guid? adminDeviceId = null,
            Guid? adminSessionId = null, string? traceId = null, string? ipAddress = null,
            string? userAgent = null, string? metadataJson = null)
        {
        }

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

    private class DummyTicketPdfService : ITicketPdfService
    {
        public Task<TicketPdfResult> GetOrCreateTicketPdfAsync(
            WorkshopTicket ticket, Workshop workshop, WorkshopBooking booking,
            string? rawQrToken = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new TicketPdfResult(true, "storage/key", "https://cdn.ethosdance.com/tickets/test.pdf", "filehash"));
    }

    private class DummyTrainerPermissionService : ITrainerPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid trainerProfileId, string permissionCode, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private IConfiguration CreateTestConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TicketSecurity:SecretKey"] = "test-secret-key-12345678901234567890123456789012"
            })
            .Build();
    }

    private static (User user, StudentProfile profile, PaymentTransaction tx) SeedUserAndTransaction(AppDbContext db)
    {
        var role = db.Roles.FirstOrDefault(r => r.Code == "STUDENT");
        if (role == null)
        {
            role = new Role { Id = Guid.NewGuid(), Code = "STUDENT", Name = "Student" };
            db.Roles.Add(role);
        }

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            FullName = "Test Student",
            Phone = "+919876543210",
            Email = "student@example.com",
            CustomerCode = "CUST-" + Guid.NewGuid().ToString("N")[..8]
        };
        user.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id, Role = role });
        db.Users.Add(user);

        var profile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = user
        };
        db.StudentProfiles.Add(profile);

        var tx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = 500,
            Currency = "INR",
            Status = PaymentStatus.Paid,
            Purpose = PaymentPurpose.WorkshopBooking
        };
        db.PaymentTransactions.Add(tx);
        return (user, profile, tx);
    }

    [Fact]
    public void Session_GetBookingCutoffUtc_PrefersSessionCutoffOverWorkshop()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
        var sessionDate = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified);
        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            SessionDate = sessionDate,
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(16, 0, 0),
            BookingCutoffTime = new TimeSpan(12, 0, 0),
            Workshop = new Workshop
            {
                Id = Guid.NewGuid(),
                WorkshopDate = sessionDate,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(20, 0, 0),
                BookingCutoffTime = new TimeSpan(9, 0, 0)
            }
        };

        var cutoffUtc = session.GetBookingCutoffUtc("Asia/Kolkata");

        var expectedLocal = sessionDate.Date + new TimeSpan(12, 0, 0);
        var expectedUtc = TimeZoneInfo.ConvertTimeToUtc(expectedLocal, tz);
        Assert.Equal(expectedUtc, cutoffUtc);
    }

    [Fact]
    public void Session_GetBookingCutoffUtc_FallsBackToWorkshopCutoffWhenSessionNull()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
        var sessionDate = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified);
        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            SessionDate = sessionDate,
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(16, 0, 0),
            BookingCutoffTime = null,
            Workshop = new Workshop
            {
                Id = Guid.NewGuid(),
                WorkshopDate = sessionDate,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(20, 0, 0),
                BookingCutoffTime = new TimeSpan(9, 0, 0)
            }
        };

        var cutoffUtc = session.GetBookingCutoffUtc("Asia/Kolkata");

        var expectedLocal = sessionDate.Date + new TimeSpan(9, 0, 0);
        var expectedUtc = TimeZoneInfo.ConvertTimeToUtc(expectedLocal, tz);
        Assert.Equal(expectedUtc, cutoffUtc);
    }

    [Fact]
    public void Session_GetBookingCutoffUtc_FallsBackToSessionStartTimeWhenBothNull()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
        var sessionDate = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified);
        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            SessionDate = sessionDate,
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(16, 0, 0),
            BookingCutoffTime = null,
            Workshop = new Workshop
            {
                Id = Guid.NewGuid(),
                WorkshopDate = sessionDate,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(20, 0, 0),
                BookingCutoffTime = null
            }
        };

        var cutoffUtc = session.GetBookingCutoffUtc("Asia/Kolkata");

        var expectedLocal = sessionDate.Date + new TimeSpan(14, 0, 0);
        var expectedUtc = TimeZoneInfo.ConvertTimeToUtc(expectedLocal, tz);
        Assert.Equal(expectedUtc, cutoffUtc);
    }

    [Theory]
    [InlineData("10:00", "12:00", "11:00", "13:00", true)]   // Overlaps
    [InlineData("10:00", "12:00", "12:00", "14:00", false)]  // Back-to-back, no overlap
    [InlineData("10:00", "12:00", "08:00", "10:00", false)]  // Back-to-back, no overlap
    [InlineData("10:00", "12:00", "10:15", "11:45", true)]   // Contained inside, overlaps
    [InlineData("10:00", "12:00", "09:00", "13:00", true)]   // Fully encompasses, overlaps
    public void Session_TimeOverlapCheck_ValidatesCorrectly(string s1, string e1, string s2, string e2, bool shouldOverlap)
    {
        var start1 = TimeSpan.Parse(s1);
        var end1 = TimeSpan.Parse(e1);
        var start2 = TimeSpan.Parse(s2);
        var end2 = TimeSpan.Parse(e2);

        var overlaps = start1 < end2 && start2 < end1;
        Assert.Equal(shouldOverlap, overlaps);
    }

    [Fact]
    public async Task ValidateTicket_ReplacedTicket_ReturnsReplacedStatus()
    {
        using var db = CreateInMemoryDbContext();
        var workshopId = Guid.NewGuid();
        var trainerProfileId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var (user, profile, tx) = SeedUserAndTransaction(db);

        var trainer = new TrainerProfile
        {
            Id = trainerProfileId,
            UserId = userId,
            TrainerCode = "TR01",
            FullName = "Test Trainer",
            Status = TrainerStatus.Active
        };
        db.TrainerProfiles.Add(trainer);

        var workshop = new Workshop
        {
            Id = workshopId,
            TrainerProfileId = trainerProfileId,
            Title = "Masterclass",
            WorkshopDate = DateTime.UtcNow.Date.AddDays(1),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Status = WorkshopStatus.Published
        };
        db.Workshops.Add(workshop);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            StudentProfileId = profile.Id,
            StudentProfile = profile,
            TotalPrice = 500,
            Status = WorkshopBookingStatus.Confirmed,
            Workshop = workshop
        };
        db.WorkshopBookings.Add(booking);

        var replacedTicket = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            WorkshopBookingId = booking.Id,
            UserId = user.Id,
            PaymentTransactionId = tx.Id,
            TicketNumber = "TKT-REPLACED-123",
            QrTokenHash = "testhash",
            Status = TicketStatus.Replaced,
            IssuedAt = DateTime.UtcNow.AddDays(-1),
            AttendeeName = "Jane Doe",
            Workshop = workshop,
            WorkshopBooking = booking
        };
        db.WorkshopTickets.Add(replacedTicket);
        await db.SaveChangesAsync();

        var ticketService = new WorkshopTicketService(db, CreateTestConfiguration());
        var trainerWorkshopService = new TrainerWorkshopService(db, new DummyTrainerPermissionService(), ticketService);
        var req = new Ethos.Api.Contracts.Trainers.TicketValidationRequest { TokenOrNumber = "TKT-REPLACED-123" };
        var result = await trainerWorkshopService.ValidateTicketAsync(userId, workshopId, req, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("Replaced", result.ValidationStatus);
        Assert.Equal(TicketStatus.Replaced, result.Status);
    }

    [Fact]
    public async Task ModifyWorkshopBookingSession_OverallPass_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext();
        var adminUserId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var passTypeId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();

        var (user, profile, tx) = SeedUserAndTransaction(db);

        var trainer = new TrainerProfile
        {
            Id = trainerId,
            UserId = Guid.NewGuid(),
            TrainerCode = "TR09",
            FullName = "Test Trainer",
            Status = TrainerStatus.Active
        };
        db.TrainerProfiles.Add(trainer);

        var workshop = new Workshop
        {
            Id = workshopId,
            TrainerProfileId = trainerId,
            Title = "Festival Workshop",
            WorkshopDate = DateTime.UtcNow.Date.AddDays(5),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            Status = WorkshopStatus.Published
        };
        db.Workshops.Add(workshop);

        var passType = new WorkshopPassType
        {
            Id = passTypeId,
            WorkshopId = workshopId,
            Name = "Festival Overall Pass",
            SessionsIncluded = null, // Overall pass covers everything
            Price = 2000
        };
        db.WorkshopPassTypes.Add(passType);

        var booking = new WorkshopBooking
        {
            Id = bookingId,
            WorkshopId = workshopId,
            StudentProfileId = profile.Id,
            StudentProfile = profile,
            WorkshopPassTypeId = passTypeId,
            TotalPrice = 2000,
            Status = WorkshopBookingStatus.Confirmed,
            Workshop = workshop
        };
        db.WorkshopBookings.Add(booking);
        await db.SaveChangesAsync();

        var ticketService = new WorkshopTicketService(db, CreateTestConfiguration());
        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), ticketService, new DummyTicketPdfService());

        var req = new AdminModifyBookingSessionRequest
        {
            CurrentTicketId = Guid.NewGuid(),
            ReplacementSessionId = Guid.NewGuid()
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adminBookingService.ModifyWorkshopBookingSessionAsync(bookingId, adminUserId, req, CancellationToken.None)
        );

        Assert.Contains("Overall Pass", ex.Message);
    }

    [Fact]
    public async Task ModifyWorkshopBookingSession_CutoffClosed_ThrowsWithoutOverride()
    {
        using var db = CreateInMemoryDbContext();
        var adminUserId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var passTypeId = Guid.NewGuid();
        var currentSessionId = Guid.NewGuid();
        var replacementSessionId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();

        var (user, profile, tx) = SeedUserAndTransaction(db);

        var trainer = new TrainerProfile
        {
            Id = trainerId,
            UserId = Guid.NewGuid(),
            TrainerCode = "TR10",
            FullName = "Test Trainer",
            Status = TrainerStatus.Active
        };
        db.TrainerProfiles.Add(trainer);

        var workshop = new Workshop
        {
            Id = workshopId,
            TrainerProfileId = trainerId,
            Title = "Multi Session Workshop",
            WorkshopDate = DateTime.UtcNow.Date.AddDays(2),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            Status = WorkshopStatus.Published
        };
        db.Workshops.Add(workshop);

        var passType = new WorkshopPassType
        {
            Id = passTypeId,
            WorkshopId = workshopId,
            Name = "2-Session Bundle",
            SessionsIncluded = 2,
            Price = 500
        };
        db.WorkshopPassTypes.Add(passType);

        var sessionCurrent = new WorkshopSession
        {
            Id = currentSessionId,
            WorkshopId = workshopId,
            TrainerProfileId = trainerId,
            Title = "Session 1",
            SessionDate = DateTime.UtcNow.Date.AddDays(2),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Capacity = 30,
            IsActive = true
        };

        var sessionExtra = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            TrainerProfileId = trainerId,
            Title = "Session Extra",
            SessionDate = DateTime.UtcNow.Date.AddDays(2),
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(16, 0, 0),
            Capacity = 30,
            IsActive = true
        };

        // Past cutoff session
        var sessionReplacement = new WorkshopSession
        {
            Id = replacementSessionId,
            WorkshopId = workshopId,
            TrainerProfileId = trainerId,
            Title = "Session 2 - Cutoff Past",
            SessionDate = DateTime.UtcNow.Date.AddDays(-1),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            BookingCutoffTime = new TimeSpan(8, 0, 0),
            Capacity = 30,
            IsActive = true
        };

        db.WorkshopSessions.AddRange(sessionCurrent, sessionReplacement, sessionExtra);

        var booking = new WorkshopBooking
        {
            Id = bookingId,
            WorkshopId = workshopId,
            StudentProfileId = profile.Id,
            StudentProfile = profile,
            WorkshopPassTypeId = passTypeId,
            TotalPrice = 500,
            Status = WorkshopBookingStatus.Confirmed,
            Workshop = workshop
        };
        db.WorkshopBookings.Add(booking);

        var ticket = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            WorkshopBookingId = bookingId,
            WorkshopSessionId = currentSessionId,
            UserId = user.Id,
            PaymentTransactionId = tx.Id,
            TicketNumber = "TKT-ORIG-1",
            QrTokenHash = "hash1",
            AttendeeName = "Test Attendee",
            Status = TicketStatus.Issued,
            IssuedAt = DateTime.UtcNow.AddDays(-2),
            Workshop = workshop,
            WorkshopBooking = booking
        };
        db.WorkshopTickets.Add(ticket);

        var bookingSession = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = bookingId,
            WorkshopSessionId = currentSessionId,
            WorkshopTicketId = ticket.Id,
            Status = WorkshopBookingSessionStatus.Booked
        };
        var bookingSessionExtra = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = bookingId,
            WorkshopSessionId = sessionExtra.Id,
            Status = WorkshopBookingSessionStatus.Booked
        };
        db.WorkshopBookingSessions.AddRange(bookingSession, bookingSessionExtra);
        await db.SaveChangesAsync();

        var ticketService = new WorkshopTicketService(db, CreateTestConfiguration());
        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), ticketService, new DummyTicketPdfService());

        var req = new AdminModifyBookingSessionRequest
        {
            CurrentTicketId = ticket.Id,
            ReplacementSessionId = replacementSessionId,
            OverrideCutoff = false
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adminBookingService.ModifyWorkshopBookingSessionAsync(bookingId, adminUserId, req, CancellationToken.None)
        );

        Assert.Contains("booking cutoff", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ModifyWorkshopBookingSession_CutoffClosed_ThrowsIfOverrideWithoutReason()
    {
        using var db = CreateInMemoryDbContext();
        var adminUserId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var passTypeId = Guid.NewGuid();
        var currentSessionId = Guid.NewGuid();
        var replacementSessionId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();

        var (user, profile, tx) = SeedUserAndTransaction(db);

        var trainer = new TrainerProfile
        {
            Id = trainerId,
            UserId = Guid.NewGuid(),
            TrainerCode = "TR11",
            FullName = "Test Trainer",
            Status = TrainerStatus.Active
        };
        db.TrainerProfiles.Add(trainer);

        var workshop = new Workshop
        {
            Id = workshopId,
            TrainerProfileId = trainerId,
            Title = "Multi Session Workshop",
            WorkshopDate = DateTime.UtcNow.Date.AddDays(2),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            Status = WorkshopStatus.Published
        };
        db.Workshops.Add(workshop);

        var passType = new WorkshopPassType
        {
            Id = passTypeId,
            WorkshopId = workshopId,
            Name = "2-Session Bundle",
            SessionsIncluded = 2,
            Price = 500
        };
        db.WorkshopPassTypes.Add(passType);

        var sessionCurrent = new WorkshopSession
        {
            Id = currentSessionId,
            WorkshopId = workshopId,
            TrainerProfileId = trainerId,
            Title = "Session 1",
            SessionDate = DateTime.UtcNow.Date.AddDays(2),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Capacity = 30,
            IsActive = true
        };

        var sessionExtra = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            TrainerProfileId = trainerId,
            Title = "Session Extra",
            SessionDate = DateTime.UtcNow.Date.AddDays(2),
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(16, 0, 0),
            Capacity = 30,
            IsActive = true
        };

        var sessionReplacement = new WorkshopSession
        {
            Id = replacementSessionId,
            WorkshopId = workshopId,
            TrainerProfileId = trainerId,
            Title = "Session 2 - Cutoff Past",
            SessionDate = DateTime.UtcNow.Date.AddDays(-1),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            BookingCutoffTime = new TimeSpan(8, 0, 0),
            Capacity = 30,
            IsActive = true
        };

        db.WorkshopSessions.AddRange(sessionCurrent, sessionReplacement, sessionExtra);

        var booking = new WorkshopBooking
        {
            Id = bookingId,
            WorkshopId = workshopId,
            StudentProfileId = profile.Id,
            StudentProfile = profile,
            WorkshopPassTypeId = passTypeId,
            TotalPrice = 500,
            Status = WorkshopBookingStatus.Confirmed,
            Workshop = workshop
        };
        db.WorkshopBookings.Add(booking);

        var ticket = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            WorkshopBookingId = bookingId,
            WorkshopSessionId = currentSessionId,
            UserId = user.Id,
            PaymentTransactionId = tx.Id,
            TicketNumber = "TKT-ORIG-2",
            QrTokenHash = "hash2",
            AttendeeName = "Test Attendee",
            Status = TicketStatus.Issued,
            IssuedAt = DateTime.UtcNow.AddDays(-2),
            Workshop = workshop,
            WorkshopBooking = booking
        };
        db.WorkshopTickets.Add(ticket);

        var bookingSession = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = bookingId,
            WorkshopSessionId = currentSessionId,
            WorkshopTicketId = ticket.Id,
            Status = WorkshopBookingSessionStatus.Booked
        };
        var bookingSessionExtra = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = bookingId,
            WorkshopSessionId = sessionExtra.Id,
            Status = WorkshopBookingSessionStatus.Booked
        };
        db.WorkshopBookingSessions.AddRange(bookingSession, bookingSessionExtra);
        await db.SaveChangesAsync();

        var ticketService = new WorkshopTicketService(db, CreateTestConfiguration());
        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), ticketService, new DummyTicketPdfService());

        var req = new AdminModifyBookingSessionRequest
        {
            CurrentTicketId = ticket.Id,
            ReplacementSessionId = replacementSessionId,
            OverrideCutoff = true,
            OverrideReason = "   " // Empty reason
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adminBookingService.ModifyWorkshopBookingSessionAsync(bookingId, adminUserId, req, CancellationToken.None)
        );

        Assert.Contains("reason", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ModifyWorkshopBookingSession_Success_AtomicallySwapsSessionAndInvalidatesOldTicket()
    {
        using var db = CreateInMemoryDbContext();
        var adminUserId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var passTypeId = Guid.NewGuid();
        var currentSessionId = Guid.NewGuid();
        var replacementSessionId = Guid.NewGuid();
        var trainerProfileId = Guid.NewGuid();

        var (user, profile, tx) = SeedUserAndTransaction(db);

        var trainer = new TrainerProfile
        {
            Id = trainerProfileId,
            UserId = Guid.NewGuid(),
            TrainerCode = "TR12",
            FullName = "Master Choreographer",
            Status = TrainerStatus.Active
        };
        db.TrainerProfiles.Add(trainer);

        var passType = new WorkshopPassType
        {
            Id = passTypeId,
            WorkshopId = workshopId,
            Name = "2-Session Bundle",
            SessionsIncluded = 2,
            Price = 699
        };
        db.WorkshopPassTypes.Add(passType);

        var sessionCurrent = new WorkshopSession
        {
            Id = currentSessionId,
            WorkshopId = workshopId,
            Title = "Urban Hip Hop Intro",
            SessionDate = DateTime.UtcNow.Date.AddDays(3),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            TrainerProfileId = trainerProfileId,
            Capacity = 25,
            IsActive = true
        };

        var sessionExtra = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            Title = "Session Extra",
            SessionDate = DateTime.UtcNow.Date.AddDays(3),
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(16, 0, 0),
            TrainerProfileId = trainerProfileId,
            Capacity = 25,
            IsActive = true
        };

        var sessionReplacement = new WorkshopSession
        {
            Id = replacementSessionId,
            WorkshopId = workshopId,
            Title = "Contemporary Flow",
            SessionDate = DateTime.UtcNow.Date.AddDays(4),
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(16, 0, 0),
            TrainerProfileId = trainerProfileId,
            Capacity = 25,
            IsActive = true
        };

        var workshop = new Workshop
        {
            Id = workshopId,
            Title = "Ethos Winter Festival",
            WorkshopDate = DateTime.UtcNow.Date.AddDays(3),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            TrainerProfileId = trainerProfileId,
            Status = WorkshopStatus.Published,
            Capacity = 50,
            Price = 699
        };

        db.Workshops.Add(workshop);
        db.WorkshopSessions.AddRange(sessionCurrent, sessionReplacement, sessionExtra);

        var booking = new WorkshopBooking
        {
            Id = bookingId,
            WorkshopId = workshopId,
            StudentProfileId = profile.Id,
            StudentProfile = profile,
            WorkshopPassTypeId = passTypeId,
            TotalPrice = 699,
            GuestName = "Aarav Sharma",
            GuestPhone = "9876543210",
            GuestEmail = "aarav@example.com",
            Status = WorkshopBookingStatus.Confirmed,
            Workshop = workshop
        };
        db.WorkshopBookings.Add(booking);

        var oldTicket = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            WorkshopBookingId = bookingId,
            WorkshopSessionId = currentSessionId,
            UserId = user.Id,
            PaymentTransactionId = tx.Id,
            TicketNumber = "TKT-SWAP-OLD",
            QrTokenHash = "oldhash",
            AttendeeName = "Aarav Sharma",
            Status = TicketStatus.Issued,
            IssuedAt = DateTime.UtcNow.AddDays(-1),
            Workshop = workshop,
            WorkshopBooking = booking
        };
        db.WorkshopTickets.Add(oldTicket);

        var oldBookingSession = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = bookingId,
            WorkshopSessionId = currentSessionId,
            WorkshopTicketId = oldTicket.Id,
            Status = WorkshopBookingSessionStatus.Booked
        };
        var extraBookingSession = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = bookingId,
            WorkshopSessionId = sessionExtra.Id,
            Status = WorkshopBookingSessionStatus.Booked
        };
        db.WorkshopBookingSessions.AddRange(oldBookingSession, extraBookingSession);
        await db.SaveChangesAsync();

        var ticketService = new WorkshopTicketService(db, CreateTestConfiguration());
        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), ticketService, new DummyTicketPdfService());

        var req = new AdminModifyBookingSessionRequest
        {
            CurrentTicketId = oldTicket.Id,
            ReplacementSessionId = replacementSessionId,
            OverrideCutoff = false
        };

        var response = await adminBookingService.ModifyWorkshopBookingSessionAsync(bookingId, adminUserId, req, CancellationToken.None);

        Assert.Equal(oldTicket.Id, response.OldTicketId);
        Assert.False(string.IsNullOrWhiteSpace(response.NewTicketNumber));
        Assert.Equal(replacementSessionId, response.ReplacementSessionId);

        // Verify Old Session and Ticket state
        Assert.Equal(WorkshopBookingSessionStatus.Replaced, oldBookingSession.Status);
        Assert.Equal(TicketStatus.Replaced, oldTicket.Status);

        // Verify New Booking Session created
        var newBookingSession = await db.WorkshopBookingSessions
            .FirstOrDefaultAsync(bs => bs.WorkshopBookingId == bookingId && bs.WorkshopSessionId == replacementSessionId);
        Assert.NotNull(newBookingSession);
        Assert.Equal(WorkshopBookingSessionStatus.Booked, newBookingSession.Status);
        Assert.Equal(currentSessionId, newBookingSession.OriginalSessionId);

        // Verify New Ticket created and linked
        var newTicket = await db.WorkshopTickets
            .FirstOrDefaultAsync(t => t.Id == newBookingSession.WorkshopTicketId);
        Assert.NotNull(newTicket);
        Assert.Equal(TicketStatus.Issued, newTicket.Status);
        Assert.Equal(replacementSessionId, newTicket.WorkshopSessionId);
        Assert.Equal(response.NewTicketNumber, newTicket.TicketNumber);
    }

    [Fact]
    public async Task ModifyWorkshopBookingSession_ResolvesByCurrentSessionId_WhenTicketIdNull()
    {
        using var db = CreateInMemoryDbContext();
        var adminUserId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var currentSessionId = Guid.NewGuid();
        var replacementSessionId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();

        var (user, profile, tx) = SeedUserAndTransaction(db);

        var trainer = new TrainerProfile
        {
            Id = trainerId,
            UserId = Guid.NewGuid(),
            TrainerCode = "TR15",
            FullName = "Test Trainer",
            Status = TrainerStatus.Active
        };
        db.TrainerProfiles.Add(trainer);

        var workshop = new Workshop
        {
            Id = workshopId,
            TrainerProfileId = trainerId,
            Title = "Resolution Test",
            WorkshopDate = DateTime.UtcNow.Date.AddDays(5),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            Status = WorkshopStatus.Published
        };
        db.Workshops.Add(workshop);

        var curSession = new WorkshopSession
        {
            Id = currentSessionId,
            WorkshopId = workshopId,
            TrainerProfileId = trainerId,
            Title = "Session A",
            SessionDate = DateTime.UtcNow.Date.AddDays(5),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Capacity = 20,
            IsActive = true
        };
        var repSession = new WorkshopSession
        {
            Id = replacementSessionId,
            WorkshopId = workshopId,
            TrainerProfileId = trainerId,
            Title = "Session B",
            SessionDate = DateTime.UtcNow.Date.AddDays(5),
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(16, 0, 0),
            Capacity = 20,
            IsActive = true
        };
        db.WorkshopSessions.AddRange(curSession, repSession);

        var booking = new WorkshopBooking
        {
            Id = bookingId,
            WorkshopId = workshopId,
            StudentProfileId = profile.Id,
            StudentProfile = profile,
            Quantity = 1,
            TotalPrice = 500,
            Status = WorkshopBookingStatus.Confirmed,
            Workshop = workshop
        };
        db.WorkshopBookings.Add(booking);

        var oldTicket = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            WorkshopBookingId = bookingId,
            WorkshopSessionId = currentSessionId,
            UserId = user.Id,
            PaymentTransactionId = tx.Id,
            TicketNumber = "TKT-SWAP-BY-SESSION",
            QrTokenHash = "testhash",
            Status = TicketStatus.Issued,
            AttendeeName = "Jane Doe",
            AttendeePhone = "+919876543210"
        };
        db.WorkshopTickets.Add(oldTicket);

        // Pre-existing TicketPdf
        var oldPdf = new TicketPdf
        {
            Id = Guid.NewGuid(),
            TicketId = oldTicket.Id,
            StorageKey = "tickets/old.pdf",
            FileHash = "hash123",
            FileSizeBytes = 1024
        };
        db.TicketPdfs.Add(oldPdf);

        var oldBookingSession = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = bookingId,
            WorkshopSessionId = currentSessionId,
            WorkshopTicketId = oldTicket.Id,
            Status = WorkshopBookingSessionStatus.Booked
        };
        db.WorkshopBookingSessions.Add(oldBookingSession);
        await db.SaveChangesAsync();

        var ticketService = new WorkshopTicketService(db, CreateTestConfiguration());
        var adminBookingService = new AdminBookingService(
            db, new DummyAuditService(), ticketService, new DummyTicketPdfService());

        var req = new AdminModifyBookingSessionRequest
        {
            CurrentTicketId = null,
            CurrentSessionId = currentSessionId,
            ReplacementSessionId = replacementSessionId,
            OverrideCutoff = false
        };

        var response = await adminBookingService.ModifyWorkshopBookingSessionAsync(bookingId, adminUserId, req, CancellationToken.None);

        Assert.Equal(oldTicket.Id, response.OldTicketId);
        Assert.Equal(TicketStatus.Replaced, oldTicket.Status);

        // Verify old TicketPdf record was purged
        var remainingPdf = await db.TicketPdfs.FirstOrDefaultAsync(p => p.TicketId == oldTicket.Id);
        Assert.Null(remainingPdf);
    }

    [Fact]
    public void AdminCreateWorkshopRequest_DeserializesStatus_FromIntegerAndString()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // 1. Integer status 3 (Approved)
        var jsonInt3 = "{\"title\":\"Tollywood Workshop\",\"danceStyle\":\"Tollywood\",\"level\":\"Open Level\",\"status\":3}";
        var reqInt3 = System.Text.Json.JsonSerializer.Deserialize<AdminCreateWorkshopRequest>(jsonInt3, options);
        Assert.NotNull(reqInt3);
        Assert.Equal(WorkshopStatus.Approved, reqInt3.Status);

        // 2. Integer status 1 (Draft)
        var jsonInt1 = "{\"title\":\"Tollywood Workshop\",\"danceStyle\":\"Tollywood\",\"level\":\"Open Level\",\"status\":1}";
        var reqInt1 = System.Text.Json.JsonSerializer.Deserialize<AdminCreateWorkshopRequest>(jsonInt1, options);
        Assert.NotNull(reqInt1);
        Assert.Equal(WorkshopStatus.Draft, reqInt1.Status);

        // 3. String status "Approved"
        var jsonStrApproved = "{\"title\":\"Tollywood Workshop\",\"danceStyle\":\"Tollywood\",\"level\":\"Open Level\",\"status\":\"Approved\"}";
        var reqStrApproved = System.Text.Json.JsonSerializer.Deserialize<AdminCreateWorkshopRequest>(jsonStrApproved, options);
        Assert.NotNull(reqStrApproved);
        Assert.Equal(WorkshopStatus.Approved, reqStrApproved.Status);

        // 4. Case-insensitive string status "draft"
        var jsonStrDraft = "{\"title\":\"Tollywood Workshop\",\"danceStyle\":\"Tollywood\",\"level\":\"Open Level\",\"status\":\"draft\"}";
        var reqStrDraft = System.Text.Json.JsonSerializer.Deserialize<AdminCreateWorkshopRequest>(jsonStrDraft, options);
        Assert.NotNull(reqStrDraft);
        Assert.Equal(WorkshopStatus.Draft, reqStrDraft.Status);
    }

    [Fact]
    public async Task AdminWorkshopService_CreateWorkshop_SupportsMultiDateMultiSessionAndPassTypes()
    {
        var db = CreateInMemoryDbContext();
        var adminUserId = Guid.NewGuid();

        // Seed trainer
        var trainerProfile = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TrainerCode = "TR99",
            FullName = "SL LEAD Trainer",
            Status = TrainerStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        db.TrainerProfiles.Add(trainerProfile);
        await db.SaveChangesAsync();

        var adminWorkshopService = new AdminWorkshopService(db, new DummyAuditService());

        var session1Id = Guid.NewGuid();
        var createReq = new AdminCreateWorkshopRequest
        {
            Title = "Tollywood Workshop",
            DanceStyle = "Tollywood",
            Level = "All Levels",
            Venue = "Studio A",
            Price = 500,
            Capacity = 30,
            TrainerProfileId = trainerProfile.Id,
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new AdminWorkshopSessionItem
                {
                    Id = session1Id,
                    Title = "Session 1",
                    SessionDate = new DateTime(2026, 9, 19),
                    StartTime = new TimeSpan(18, 0, 0),
                    EndTime = new TimeSpan(19, 30, 0),
                    TrainerProfileId = null, // Fallback to workshop lead trainer
                    Capacity = 30
                },
                new AdminWorkshopSessionItem
                {
                    Title = "Session 2",
                    SessionDate = new DateTime(2026, 9, 19),
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(11, 30, 0),
                    TrainerProfileId = trainerProfile.Id,
                    Capacity = 30
                },
                new AdminWorkshopSessionItem
                {
                    Title = "Session 3",
                    SessionDate = new DateTime(2026, 9, 20),
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(11, 30, 0),
                    TrainerProfileId = trainerProfile.Id,
                    Capacity = 30
                }
            },
            PassTypes = new List<AdminWorkshopPassTypeItem>
            {
                new AdminWorkshopPassTypeItem { Name = "Solo Pass", Price = 500, WorkshopSessionId = session1Id, SessionsIncluded = 1, TotalQuantity = 100 },
                new AdminWorkshopPassTypeItem { Name = "Duo Pass", Price = 900, SessionsIncluded = 2, TotalQuantity = 100 },
                new AdminWorkshopPassTypeItem { Name = "Trio Pass", Price = 1250, SessionsIncluded = 3, TotalQuantity = 100 },
                new AdminWorkshopPassTypeItem { Name = "Overall Pass", Price = 1600, SessionsIncluded = null, TotalQuantity = 100 }
            }
        };

        var result = await adminWorkshopService.CreateWorkshopAsync(adminUserId, createReq, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Tollywood Workshop", result.Title);

        // Verify persisted sessions
        var savedSessions = await db.WorkshopSessions.Where(s => s.WorkshopId == result.Id).OrderBy(s => s.DisplayOrder).ToListAsync();
        Assert.Equal(3, savedSessions.Count);
        // Verify session 1 inherited trainer
        Assert.Equal(trainerProfile.Id, savedSessions[0].TrainerProfileId);

        // Verify persisted pass types
        var savedPassTypes = await db.WorkshopPassTypes.Where(p => p.WorkshopId == result.Id).OrderBy(p => p.DisplayOrder).ToListAsync();
        Assert.Equal(4, savedPassTypes.Count);
        Assert.Null(savedPassTypes.First(p => p.Name == "Overall Pass").SessionsIncluded);
    }

    [Fact]
    public async Task SessionCapacityReduction_SafeguardsAgainstReducingBelowConfirmedBookings()
    {
        var db = CreateInMemoryDbContext();
        var adminWorkshopService = new AdminWorkshopService(db, new DummyAuditService());

        var adminUserId = Guid.NewGuid();
        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Capacity Safety Workshop",
            Status = WorkshopStatus.Published,
            Capacity = 30,
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        db.Workshops.Add(workshop);

        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session 1",
            SessionDate = DateTime.UtcNow.AddDays(7),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Capacity = 30,
            IsActive = true
        };
        db.WorkshopSessions.Add(session);

        // Seed 25 confirmed booking sessions
        for (int i = 0; i < 25; i++)
        {
            var booking = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                StudentProfileId = Guid.NewGuid(),
                GuestName = $"Attendee {i}",
                GuestPhone = "9999999999",
                GuestEmail = $"att{i}@ethos.com",
                Status = WorkshopBookingStatus.Confirmed,
                BookedAt = DateTime.UtcNow
            };
            db.WorkshopBookings.Add(booking);

            var bookingSession = new WorkshopBookingSession
            {
                Id = Guid.NewGuid(),
                WorkshopBookingId = booking.Id,
                WorkshopSessionId = session.Id,
                Status = WorkshopBookingSessionStatus.Booked,
                CreatedAt = DateTime.UtcNow
            };
            db.WorkshopBookingSessions.Add(bookingSession);
        }
        await db.SaveChangesAsync();

        // 1. Attempt capacity = 10 (below 25 confirmed bookings) -> Expected: Rejected
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            adminWorkshopService.UpdateSessionAsync(workshop.Id, session.Id, 10, CancellationToken.None));
        Assert.Equal("CANNOT_REDUCE_CAPACITY", ex.Code);
        Assert.Contains("Cannot reduce capacity to 10", ex.Message);
        Assert.Contains("25 active reservations", ex.Message);

        // 2. Attempt capacity = 25 (exactly equal to 25 confirmed bookings) -> Expected: Accepted
        await adminWorkshopService.UpdateSessionAsync(workshop.Id, session.Id, 25, CancellationToken.None);
        var updatedSession = await db.WorkshopSessions.FindAsync(session.Id);
        Assert.Equal(25, updatedSession!.Capacity);

        // 3. Attempt capacity = 30 (above 25 confirmed bookings) -> Expected: Accepted
        await adminWorkshopService.UpdateSessionAsync(workshop.Id, session.Id, 30, CancellationToken.None);
        updatedSession = await db.WorkshopSessions.FindAsync(session.Id);
        Assert.Equal(30, updatedSession!.Capacity);

        // 4. Attempt capacity = 40 (well above 25 confirmed bookings) -> Expected: Accepted
        await adminWorkshopService.UpdateSessionAsync(workshop.Id, session.Id, 40, CancellationToken.None);
        updatedSession = await db.WorkshopSessions.FindAsync(session.Id);
        Assert.Equal(40, updatedSession!.Capacity);
    }

    [Fact]
    public async Task UpdateWorkshopAsync_SafeguardsAgainstReducingSessionCapacityBelowConfirmedBookings()
    {
        var db = CreateInMemoryDbContext();
        var adminWorkshopService = new AdminWorkshopService(db, new DummyAuditService());

        var adminUserId = Guid.NewGuid();
        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Workshop Capacity Wizard Safety",
            Status = WorkshopStatus.Published,
            Capacity = 30,
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        db.Workshops.Add(workshop);

        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session 1",
            SessionDate = DateTime.UtcNow.AddDays(7),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Capacity = 30,
            IsActive = true
        };
        db.WorkshopSessions.Add(session);

        // Seed 25 confirmed bookings
        for (int i = 0; i < 25; i++)
        {
            var booking = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                StudentProfileId = Guid.NewGuid(),
                GuestName = $"Attendee {i}",
                GuestPhone = "9999999999",
                GuestEmail = $"att{i}@ethos.com",
                Status = WorkshopBookingStatus.Confirmed,
                BookedAt = DateTime.UtcNow
            };
            db.WorkshopBookings.Add(booking);

            var bookingSession = new WorkshopBookingSession
            {
                Id = Guid.NewGuid(),
                WorkshopBookingId = booking.Id,
                WorkshopSessionId = session.Id,
                Status = WorkshopBookingSessionStatus.Booked,
                CreatedAt = DateTime.UtcNow
            };
            db.WorkshopBookingSessions.Add(bookingSession);
        }
        await db.SaveChangesAsync();

        var updateReq = new AdminUpdateWorkshopRequest
        {
            Title = "Workshop Capacity Wizard Safety",
            DanceStyle = "Hip Hop",
            Level = "Open Level",
            Venue = "Ethos Studio",
            City = "Hyderabad",
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Capacity = 30,
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new AdminWorkshopSessionItem
                {
                    Id = session.Id,
                    Title = "Session 1",
                    SessionDate = DateTime.UtcNow.AddDays(7),
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(11, 30, 0),
                    Capacity = 10 // Attempt to reduce below 25
                }
            }
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            adminWorkshopService.UpdateWorkshopAsync(workshop.Id, adminUserId, updateReq, CancellationToken.None));
        Assert.Equal("CANNOT_REDUCE_CAPACITY", ex.Code);
        Assert.Contains("Cannot reduce capacity to 10", ex.Message);
        Assert.Contains("25 active reservations", ex.Message);
    }

    [Fact]
    public async Task UpdateSessionCapacity_CaseA_Confirmed10_PendingExpired5_Requested10_Allows()
    {
        var db = CreateInMemoryDbContext();
        var adminWorkshopService = new AdminWorkshopService(db, new DummyAuditService());

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Workshop Case A",
            Status = WorkshopStatus.Published,
            Capacity = 20,
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        db.Workshops.Add(workshop);

        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session A",
            SessionDate = DateTime.UtcNow.AddDays(7),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Capacity = 20,
            IsActive = true
        };
        db.WorkshopSessions.Add(session);

        // 10 Confirmed bookings
        for (int i = 0; i < 10; i++)
        {
            var booking = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                StudentProfileId = Guid.NewGuid(),
                Status = WorkshopBookingStatus.Confirmed,
                BookedAt = DateTime.UtcNow
            };
            db.WorkshopBookings.Add(booking);
            db.WorkshopBookingSessions.Add(new WorkshopBookingSession
            {
                Id = Guid.NewGuid(),
                WorkshopBookingId = booking.Id,
                WorkshopSessionId = session.Id,
                Status = WorkshopBookingSessionStatus.Booked
            });
        }

        // 5 PendingPayment bookings expired in the past
        for (int i = 0; i < 5; i++)
        {
            var booking = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                StudentProfileId = Guid.NewGuid(),
                Status = WorkshopBookingStatus.PendingPayment,
                ReservationExpiresAt = DateTime.UtcNow.AddMinutes(-10), // Expired
                BookedAt = DateTime.UtcNow.AddMinutes(-20)
            };
            db.WorkshopBookings.Add(booking);
            db.WorkshopBookingSessions.Add(new WorkshopBookingSession
            {
                Id = Guid.NewGuid(),
                WorkshopBookingId = booking.Id,
                WorkshopSessionId = session.Id,
                Status = WorkshopBookingSessionStatus.Booked
            });
        }

        await db.SaveChangesAsync();

        // Requested capacity = 10 -> Expected: ALLOW
        await adminWorkshopService.UpdateSessionAsync(workshop.Id, session.Id, 10, CancellationToken.None);

        var updated = await db.WorkshopSessions.FindAsync(session.Id);
        Assert.NotNull(updated);
        Assert.Equal(10, updated!.Capacity);
    }

    [Fact]
    public async Task UpdateSessionCapacity_CaseB_Confirmed10_PendingActive5_Requested10_Rejects()
    {
        var db = CreateInMemoryDbContext();
        var adminWorkshopService = new AdminWorkshopService(db, new DummyAuditService());

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Workshop Case B",
            Status = WorkshopStatus.Published,
            Capacity = 20,
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        db.Workshops.Add(workshop);

        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session B",
            SessionDate = DateTime.UtcNow.AddDays(7),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Capacity = 20,
            IsActive = true
        };
        db.WorkshopSessions.Add(session);

        // 10 Confirmed bookings
        for (int i = 0; i < 10; i++)
        {
            var booking = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                StudentProfileId = Guid.NewGuid(),
                Status = WorkshopBookingStatus.Confirmed,
                BookedAt = DateTime.UtcNow
            };
            db.WorkshopBookings.Add(booking);
            db.WorkshopBookingSessions.Add(new WorkshopBookingSession
            {
                Id = Guid.NewGuid(),
                WorkshopBookingId = booking.Id,
                WorkshopSessionId = session.Id,
                Status = WorkshopBookingSessionStatus.Booked
            });
        }

        // 5 PendingPayment bookings expiring in future
        for (int i = 0; i < 5; i++)
        {
            var booking = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                StudentProfileId = Guid.NewGuid(),
                Status = WorkshopBookingStatus.PendingPayment,
                ReservationExpiresAt = DateTime.UtcNow.AddMinutes(15), // Future
                BookedAt = DateTime.UtcNow
            };
            db.WorkshopBookings.Add(booking);
            db.WorkshopBookingSessions.Add(new WorkshopBookingSession
            {
                Id = Guid.NewGuid(),
                WorkshopBookingId = booking.Id,
                WorkshopSessionId = session.Id,
                Status = WorkshopBookingSessionStatus.Booked
            });
        }

        await db.SaveChangesAsync();

        // Requested capacity = 10 -> Expected: REJECT (15 active reservations)
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            adminWorkshopService.UpdateSessionAsync(workshop.Id, session.Id, 10, CancellationToken.None));

        Assert.Equal("CANNOT_REDUCE_CAPACITY", ex.Code);
        Assert.Contains("Cannot reduce capacity to 10", ex.Message);
        Assert.Contains("15 active reservations", ex.Message);
    }

    [Fact]
    public async Task UpdateSessionCapacity_CaseC_Confirmed10_Requested10_Allows()
    {
        var db = CreateInMemoryDbContext();
        var adminWorkshopService = new AdminWorkshopService(db, new DummyAuditService());

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Workshop Case C",
            Status = WorkshopStatus.Published,
            Capacity = 20,
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        db.Workshops.Add(workshop);

        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session C",
            SessionDate = DateTime.UtcNow.AddDays(7),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Capacity = 20,
            IsActive = true
        };
        db.WorkshopSessions.Add(session);

        // 10 Confirmed bookings
        for (int i = 0; i < 10; i++)
        {
            var booking = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                StudentProfileId = Guid.NewGuid(),
                Status = WorkshopBookingStatus.Confirmed,
                BookedAt = DateTime.UtcNow
            };
            db.WorkshopBookings.Add(booking);
            db.WorkshopBookingSessions.Add(new WorkshopBookingSession
            {
                Id = Guid.NewGuid(),
                WorkshopBookingId = booking.Id,
                WorkshopSessionId = session.Id,
                Status = WorkshopBookingSessionStatus.Booked
            });
        }

        await db.SaveChangesAsync();

        // Requested capacity = 10 -> Expected: ALLOW
        await adminWorkshopService.UpdateSessionAsync(workshop.Id, session.Id, 10, CancellationToken.None);

        var updated = await db.WorkshopSessions.FindAsync(session.Id);
        Assert.NotNull(updated);
        Assert.Equal(10, updated!.Capacity);
    }

    [Fact]
    public async Task UpdateSessionCapacity_CaseD_Confirmed10_Requested9_Rejects()
    {
        var db = CreateInMemoryDbContext();
        var adminWorkshopService = new AdminWorkshopService(db, new DummyAuditService());

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Workshop Case D",
            Status = WorkshopStatus.Published,
            Capacity = 20,
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        db.Workshops.Add(workshop);

        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session D",
            SessionDate = DateTime.UtcNow.AddDays(7),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Capacity = 20,
            IsActive = true
        };
        db.WorkshopSessions.Add(session);

        // 10 Confirmed bookings
        for (int i = 0; i < 10; i++)
        {
            var booking = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                StudentProfileId = Guid.NewGuid(),
                Status = WorkshopBookingStatus.Confirmed,
                BookedAt = DateTime.UtcNow
            };
            db.WorkshopBookings.Add(booking);
            db.WorkshopBookingSessions.Add(new WorkshopBookingSession
            {
                Id = Guid.NewGuid(),
                WorkshopBookingId = booking.Id,
                WorkshopSessionId = session.Id,
                Status = WorkshopBookingSessionStatus.Booked
            });
        }

        await db.SaveChangesAsync();

        // Requested capacity = 9 -> Expected: REJECT (10 active reservations)
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            adminWorkshopService.UpdateSessionAsync(workshop.Id, session.Id, 9, CancellationToken.None));

        Assert.Equal("CANNOT_REDUCE_CAPACITY", ex.Code);
        Assert.Contains("Cannot reduce capacity to 9", ex.Message);
        Assert.Contains("10 active reservations", ex.Message);
    }

    [Fact]
    public async Task UpdateSessionCapacity_CaseE_ConcurrentAdminUpdates_CannotBypassCapacityGuard()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var workshopId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        using (var setupDb = new AppDbContext(options))
        {
            var workshop = new Workshop
            {
                Id = workshopId,
                Title = "Workshop Case E Concurrent",
                Status = WorkshopStatus.Published,
                Capacity = 30,
                WorkshopDate = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };
            setupDb.Workshops.Add(workshop);

            var session = new WorkshopSession
            {
                Id = sessionId,
                WorkshopId = workshopId,
                Title = "Session E",
                SessionDate = DateTime.UtcNow.AddDays(7),
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(11, 30, 0),
                Capacity = 30,
                IsActive = true
            };
            setupDb.WorkshopSessions.Add(session);

            // 15 active reservations (10 confirmed + 5 future pending)
            for (int i = 0; i < 10; i++)
            {
                var booking = new WorkshopBooking
                {
                    Id = Guid.NewGuid(),
                    WorkshopId = workshopId,
                    StudentProfileId = Guid.NewGuid(),
                    Status = WorkshopBookingStatus.Confirmed,
                    BookedAt = DateTime.UtcNow
                };
                setupDb.WorkshopBookings.Add(booking);
                setupDb.WorkshopBookingSessions.Add(new WorkshopBookingSession
                {
                    Id = Guid.NewGuid(),
                    WorkshopBookingId = booking.Id,
                    WorkshopSessionId = sessionId,
                    Status = WorkshopBookingSessionStatus.Booked
                });
            }

            for (int i = 0; i < 5; i++)
            {
                var booking = new WorkshopBooking
                {
                    Id = Guid.NewGuid(),
                    WorkshopId = workshopId,
                    StudentProfileId = Guid.NewGuid(),
                    Status = WorkshopBookingStatus.PendingPayment,
                    ReservationExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    BookedAt = DateTime.UtcNow
                };
                setupDb.WorkshopBookings.Add(booking);
                setupDb.WorkshopBookingSessions.Add(new WorkshopBookingSession
                {
                    Id = Guid.NewGuid(),
                    WorkshopBookingId = booking.Id,
                    WorkshopSessionId = sessionId,
                    Status = WorkshopBookingSessionStatus.Booked
                });
            }

            await setupDb.SaveChangesAsync();
        }

        // Run 20 concurrent admin capacity updates with requested capacities:
        // Some valid (15, 16, 20, 25), some invalid (< 15: 5, 8, 10, 12, 14)
        var requestedCapacities = new[] { 5, 8, 10, 12, 14, 15, 16, 17, 18, 20, 22, 25, 6, 9, 11, 13, 15, 16, 19, 21 };
        int rejectionCount = 0;
        int successCount = 0;

        var tasks = requestedCapacities.Select(async cap =>
        {
            using var taskDb = new AppDbContext(options);
            var service = new AdminWorkshopService(taskDb, new DummyAuditService());
            try
            {
                await service.UpdateSessionAsync(workshopId, sessionId, cap, CancellationToken.None);
                Interlocked.Increment(ref successCount);
            }
            catch (BusinessRuleException ex) when (ex.Code == "CANNOT_REDUCE_CAPACITY")
            {
                Interlocked.Increment(ref rejectionCount);
            }
        });

        await Task.WhenAll(tasks);

        var expectedRejections = requestedCapacities.Count(c => c < 15);
        var expectedSuccesses = requestedCapacities.Count(c => c >= 15);
        Assert.Equal(expectedRejections, rejectionCount);
        Assert.Equal(expectedSuccesses, successCount);

        // Final capacity in DB must NEVER be less than 15
        using (var verifyDb = new AppDbContext(options))
        {
            var finalSession = await verifyDb.WorkshopSessions.FindAsync(sessionId);
            Assert.NotNull(finalSession);
            Assert.True(finalSession!.Capacity >= 15, $"Final capacity {finalSession.Capacity} was reduced below the 15 active reservations!");
        }
    }

    [Fact]
    public async Task UpdateWorkshopCapacity_SafeguardsAgainstReducingWorkshopCapacityBelowActiveBookings()
    {
        var db = CreateInMemoryDbContext();
        var adminWorkshopService = new AdminWorkshopService(db, new DummyAuditService());

        var adminUserId = Guid.NewGuid();
        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Single-Session Workshop Capacity Guard",
            Status = WorkshopStatus.Published,
            Capacity = 50,
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            StartUtc = DateTime.UtcNow.AddDays(7),
            EndUtc = DateTime.UtcNow.AddDays(7).AddHours(2),
            CreatedAt = DateTime.UtcNow
        };
        db.Workshops.Add(workshop);

        // 20 confirmed bookings with quantity = 1
        for (int i = 0; i < 20; i++)
        {
            db.WorkshopBookings.Add(new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                StudentProfileId = Guid.NewGuid(),
                Quantity = 1,
                Status = WorkshopBookingStatus.Confirmed,
                BookedAt = DateTime.UtcNow
            });
        }

        // 1 confirmed booking with quantity = 2 (total confirmed seats = 22)
        db.WorkshopBookings.Add(new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = Guid.NewGuid(),
            Quantity = 2,
            Status = WorkshopBookingStatus.Confirmed,
            BookedAt = DateTime.UtcNow
        });

        // 5 expired pending bookings (should NOT consume capacity)
        for (int i = 0; i < 5; i++)
        {
            db.WorkshopBookings.Add(new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                StudentProfileId = Guid.NewGuid(),
                Quantity = 1,
                Status = WorkshopBookingStatus.PendingPayment,
                ReservationExpiresAt = DateTime.UtcNow.AddMinutes(-15),
                BookedAt = DateTime.UtcNow.AddMinutes(-25)
            });
        }

        await db.SaveChangesAsync();

        var updateReqBelow = new AdminUpdateWorkshopRequest
        {
            Title = "Single-Session Workshop Capacity Guard",
            DanceStyle = "Contemporary",
            Level = "Intermediate",
            Venue = "Main Studio",
            City = "Hyderabad",
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Capacity = 20 // Below the 22 active reservations
        };

        // 1. Attempt capacity = 20 -> Expected: Rejected
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            adminWorkshopService.UpdateWorkshopAsync(workshop.Id, adminUserId, updateReqBelow, CancellationToken.None));
        Assert.Equal("CANNOT_REDUCE_CAPACITY", ex.Code);
        Assert.Contains("Cannot reduce workshop capacity to 20", ex.Message);
        Assert.Contains("22 active reservations", ex.Message);

        // 2. Attempt capacity = 22 -> Expected: Allowed (exact boundary)
        var updateReqEqual = new AdminUpdateWorkshopRequest
        {
            Title = "Single-Session Workshop Capacity Guard",
            DanceStyle = "Contemporary",
            Level = "Intermediate",
            Venue = "Main Studio",
            City = "Hyderabad",
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Capacity = 22
        };
        await adminWorkshopService.UpdateWorkshopAsync(workshop.Id, adminUserId, updateReqEqual, CancellationToken.None);
        var updatedWorkshop = await db.Workshops.FindAsync(workshop.Id);
        Assert.NotNull(updatedWorkshop);
        Assert.Equal(22, updatedWorkshop!.Capacity);

        // 3. Attempt capacity = 30 -> Expected: Allowed (above active reservations)
        var updateReqAbove = new AdminUpdateWorkshopRequest
        {
            Title = "Single-Session Workshop Capacity Guard",
            DanceStyle = "Contemporary",
            Level = "Intermediate",
            Venue = "Main Studio",
            City = "Hyderabad",
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Capacity = 30
        };
        await adminWorkshopService.UpdateWorkshopAsync(workshop.Id, adminUserId, updateReqAbove, CancellationToken.None);
        updatedWorkshop = await db.Workshops.FindAsync(workshop.Id);
        Assert.NotNull(updatedWorkshop);
        Assert.Equal(30, updatedWorkshop!.Capacity);
    }

    public sealed class PostgresIntegrationFactAttribute : FactAttribute
    {
        public PostgresIntegrationFactAttribute()
        {
            var pgConnection = Environment.GetEnvironmentVariable("ETHOS_TEST_POSTGRES")
                               ?? Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION");
            if (string.IsNullOrWhiteSpace(pgConnection))
            {
                Skip = "Requires live PostgreSQL instance via ETHOS_TEST_POSTGRES or POSTGRES_TEST_CONNECTION environment variable. Row-level FOR UPDATE serialization is verified in staging smoke tests.";
            }
        }
    }

    [PostgresIntegrationFact]
    public async Task UpdateSessionCapacity_PostgreSql_Locking_IntegrationTest()
    {
        var pgConnection = Environment.GetEnvironmentVariable("ETHOS_TEST_POSTGRES")
                           ?? Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION")!;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(pgConnection)
            .Options;

        var workshopId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();

        // 1. Setup workshop and session with 10 confirmed bookings
        using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var trainerProfile = await db.TrainerProfiles.FirstOrDefaultAsync();
            if (trainerProfile == null)
            {
                var tUser = new User
                {
                    Id = Guid.NewGuid(),
                    CustomerCode = "TRN-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                    FullName = "Test Trainer",
                    Phone = $"9188{Random.Shared.Next(10000000, 99999999)}",
                    Email = $"trainer_{Guid.NewGuid():N}@ethos.test",
                    PasswordHash = "hash",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.Users.Add(tUser);

                var tier = await db.TrainerTiers.FirstOrDefaultAsync() ?? new TrainerTier { Id = Guid.NewGuid(), Name = "Core", Code = "CORE" };
                if (db.Entry(tier).State == EntityState.Detached) db.TrainerTiers.Add(tier);

                trainerProfile = new TrainerProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = tUser.Id,
                    CurrentTierId = tier.Id,
                    TrainerCode = "TRN-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                    FullName = "Test Trainer",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.TrainerProfiles.Add(trainerProfile);
                await db.SaveChangesAsync();
            }
            trainerId = trainerProfile.Id;

            var studentProfile = await db.StudentProfiles.FirstOrDefaultAsync();
            if (studentProfile == null)
            {
                var sUser = new User
                {
                    Id = Guid.NewGuid(),
                    CustomerCode = "STU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                    FullName = "Test Student",
                    Phone = $"9199{Random.Shared.Next(10000000, 99999999)}",
                    Email = $"student_{Guid.NewGuid():N}@ethos.test",
                    PasswordHash = "hash",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.Users.Add(sUser);

                studentProfile = new StudentProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = sUser.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.StudentProfiles.Add(studentProfile);
                await db.SaveChangesAsync();
            }

            var workshop = new Workshop
            {
                Id = workshopId,
                Title = "PG Concurrency Workshop",
                Status = WorkshopStatus.Published,
                Capacity = 20,
                WorkshopDate = DateTime.UtcNow.AddDays(5),
                CreatedAt = DateTime.UtcNow
            };
            db.Workshops.Add(workshop);

            var session = new WorkshopSession
            {
                Id = sessionId,
                WorkshopId = workshopId,
                Title = "PG Session",
                SessionDate = DateTime.UtcNow.AddDays(5),
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 30, 0),
                TrainerProfileId = trainerId,
                Capacity = 20,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.WorkshopSessions.Add(session);

            for (int i = 0; i < 10; i++)
            {
                var b = new WorkshopBooking
                {
                    Id = Guid.NewGuid(),
                    WorkshopId = workshopId,
                    StudentProfileId = studentProfile.Id,
                    Status = WorkshopBookingStatus.Confirmed,
                    BookedAt = DateTime.UtcNow
                };
                db.WorkshopBookings.Add(b);
                db.WorkshopBookingSessions.Add(new WorkshopBookingSession
                {
                    Id = Guid.NewGuid(),
                    WorkshopBookingId = b.Id,
                    WorkshopSessionId = sessionId,
                    Status = WorkshopBookingSessionStatus.Booked
                });
            }

            await db.SaveChangesAsync();
        }

        // 2. Multi-connection concurrency test:
        // Connection A acquires FOR UPDATE lock, updates capacity to 15, and holds lock.
        // Connection B attempts FOR UPDATE on the same row, is physically blocked/serialized by PostgreSQL,
        // and upon Connection A's commit, unblocks, observes committed capacity = 15, and tests capacity reduction guard.
        var lockAcquiredTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = new System.Diagnostics.Stopwatch();
        int observedCapacityByConnectionB = 0;
        Exception? connectionBException = null;

        var taskA = Task.Run(async () =>
        {
            using var dbA = new AppDbContext(options);
            using var txA = await dbA.Database.BeginTransactionAsync();

            // Acquire exclusive FOR UPDATE lock on session
            await dbA.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM workshop_sessions WHERE \"Id\" = {sessionId} FOR UPDATE");

            var sessionA = await dbA.WorkshopSessions.SingleAsync(s => s.Id == sessionId);

            // Signal to Connection B that lock is held
            lockAcquiredTcs.SetResult(true);

            // Allow Connection B time to reach and block on FOR UPDATE in PostgreSQL
            await Task.Delay(150);

            // Connection A updates capacity to 15 (>= 10 booked seats)
            sessionA.Capacity = 15;
            sessionA.UpdatedAt = DateTime.UtcNow;
            await dbA.SaveChangesAsync();

            // Hold lock for another 250ms to demonstrate physical serialization
            await Task.Delay(250);

            await txA.CommitAsync();
        });

        var taskB = Task.Run(async () =>
        {
            // Wait until Connection A has acquired the row lock
            await lockAcquiredTcs.Task;

            using var dbB = new AppDbContext(options);
            using var txB = await dbB.Database.BeginTransactionAsync();

            stopwatch.Start();

            // This call physically blocks inside PostgreSQL until Connection A commits
            await dbB.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM workshop_sessions WHERE \"Id\" = {sessionId} FOR UPDATE");

            stopwatch.Stop();

            // Connection B unblocks after Connection A committed
            var sessionB = await dbB.WorkshopSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
            observedCapacityByConnectionB = sessionB.Capacity;

            // Connection B verifies capacity reduction guard: reducing below 10 must throw
            var serviceB = new AdminWorkshopService(dbB, new DummyAuditService());
            try
            {
                await serviceB.UpdateSessionAsync(workshopId, sessionId, 8, CancellationToken.None);
            }
            catch (Exception ex)
            {
                connectionBException = ex;
            }

            await txB.CommitAsync();
        });

        try
        {
            await Task.WhenAll(taskA, taskB);

            // Verification:
            // 1. Connection B was physically blocked for at least 200ms
            Assert.True(stopwatch.ElapsedMilliseconds >= 200,
                $"Connection B should have been blocked waiting for Connection A's lock. Elapsed: {stopwatch.ElapsedMilliseconds}ms");

            // 2. Connection B observed the updated capacity committed by Connection A
            Assert.Equal(15, observedCapacityByConnectionB);

            // 3. Reducing capacity below 10 booked seats threw CANNOT_REDUCE_CAPACITY
            Assert.NotNull(connectionBException);
            var bre = Assert.IsType<BusinessRuleException>(connectionBException);
            Assert.Equal("CANNOT_REDUCE_CAPACITY", bre.Code);
        }
        finally
        {
            using var cleanupDb = new AppDbContext(options);
            var bookingSessions = await cleanupDb.WorkshopBookingSessions.Where(bs => bs.WorkshopSessionId == sessionId).ToListAsync();
            cleanupDb.WorkshopBookingSessions.RemoveRange(bookingSessions);
            var bookings = await cleanupDb.WorkshopBookings.Where(b => b.WorkshopId == workshopId).ToListAsync();
            cleanupDb.WorkshopBookings.RemoveRange(bookings);
            var sessions = await cleanupDb.WorkshopSessions.Where(s => s.WorkshopId == workshopId).ToListAsync();
            cleanupDb.WorkshopSessions.RemoveRange(sessions);
            var ws = await cleanupDb.Workshops.FindAsync(workshopId);
            if (ws != null) cleanupDb.Workshops.Remove(ws);
            await cleanupDb.SaveChangesAsync();
        }
    }
}
