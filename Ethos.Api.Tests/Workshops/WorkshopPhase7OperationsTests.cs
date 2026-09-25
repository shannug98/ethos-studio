using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Common;
using Ethos.Api.Application.Finance;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Application.Payments;
using Ethos.Api.Application.Students;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Notifications;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Controllers.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class WorkshopPhase7OperationsTests
{
    private class DummyAuditService : IAdminAuditService
    {
        public List<AdminAction> LoggedActions { get; } = new();

        public void AddAuditLog(
            Guid adminUserId, string actionType, string entityType, Guid entityId,
            string? reason, string category = "OPERATIONS", Guid? adminDeviceId = null,
            Guid? adminSessionId = null, string? traceId = null, string? ipAddress = null,
            string? userAgent = null, string? metadataJson = null)
        {
            LoggedActions.Add(new AdminAction
            {
                Id = Guid.NewGuid(),
                AdminUserId = adminUserId,
                ActionType = actionType,
                EntityType = entityType,
                EntityId = entityId,
                Reason = reason,
                MetadataJson = metadataJson,
                CreatedAt = DateTime.UtcNow
            });
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

    private class MockTicketPdfService : ITicketPdfService
    {
        public Task<TicketPdfResult> GetOrCreateTicketPdfAsync(WorkshopTicket ticket, Workshop workshop, WorkshopBooking booking, string? rawQrToken = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new TicketPdfResult(true, "tickets/t1.pdf", "https://s3.example.com/tickets/t1.pdf", "hash123", null));
    }

    private class MockAuthService : IAdminAuthorizationService
    {
        public bool AllowAll { get; set; } = true;

        public Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default)
            => Task.FromResult(AllowAll);

        public Task<bool> HasAnyPermissionAsync(Guid userId, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default)
            => Task.FromResult(AllowAll);

        public Task<bool> HasAllPermissionsAsync(Guid userId, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default)
            => Task.FromResult(AllowAll);

        public Task<AdminAuthorizationResult> AuthorizeActionAsync(
            ClaimsPrincipal user, string permissionCode, string? resourceType = null,
            Guid? resourceId = null, HttpContext? httpContext = null, CancellationToken cancellationToken = default)
        {
            if (AllowAll) return Task.FromResult(new AdminAuthorizationResult { Success = true, StatusCode = 200, AdminUserId = Guid.NewGuid() });
            return Task.FromResult(new AdminAuthorizationResult { Success = false, StatusCode = 403, ErrorMessage = "Access denied: Missing BookingCorrect permission" });
        }
    }

    private class MockRefundService : IRefundService
    {
        public Task<RefundResult> RefundPaymentAsync(Guid paymentId, string reason, Guid adminUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(new RefundResult { Success = true });

        public Task<RefundResult> ProcessRefundJobAsync(Guid refundJobId, CancellationToken cancellationToken = default)
            => Task.FromResult(new RefundResult { Success = true });
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options);
        db.Roles.Add(new Role { Id = Guid.NewGuid(), Code = "STUDENT", Name = "Student" });
        db.Roles.Add(new Role { Id = Guid.NewGuid(), Code = "TRAINER", Name = "Trainer" });
        db.SaveChanges();
        return db;
    }

    private IConfiguration CreateConfiguration()
    {
        var dict = new Dictionary<string, string?>
        {
            ["TicketSecurity:SecretKey"] = "ETHOS_SUPER_SECRET_HMAC_KEY_FOR_TESTING_PURPOSES_12345"
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private StudentProfile CreateStudent(AppDbContext db, string name = "Student User")
    {
        var role = db.Roles.First(r => r.Code == "STUDENT");
        var u = new User
        {
            Id = Guid.NewGuid(),
            FullName = name,
            Phone = $"+9198{Random.Shared.Next(10000000, 99999999)}",
            Email = "student@ethos.test",
            CustomerCode = "STU" + Guid.NewGuid().ToString("N")[..5],
            CreatedAt = DateTime.UtcNow
        };
        u.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = role.Id, Role = role });
        db.Users.Add(u);

        var sp = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = u.Id,
            User = u,
            CreatedAt = DateTime.UtcNow
        };
        db.StudentProfiles.Add(sp);
        db.SaveChanges();
        return sp;
    }

    private TrainerProfile CreateTrainer(AppDbContext db, string name = "Lead Instructor")
    {
        var role = db.Roles.First(r => r.Code == "TRAINER");
        var u = new User
        {
            Id = Guid.NewGuid(),
            FullName = name,
            Phone = $"+9198{Random.Shared.Next(10000000, 99999999)}",
            CustomerCode = "TRN" + Guid.NewGuid().ToString("N")[..5],
            CreatedAt = DateTime.UtcNow
        };
        u.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = role.Id, Role = role });
        db.Users.Add(u);

        var tp = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            UserId = u.Id,
            TrainerCode = "TRN" + Guid.NewGuid().ToString("N")[..6],
            FullName = name,
            Status = TrainerStatus.Active,
            Bio = "Instructor bio",
            CreatedAt = DateTime.UtcNow
        };
        db.TrainerProfiles.Add(tp);
        db.SaveChanges();
        return tp;
    }

    private Workshop CreateWorkshop(AppDbContext db, TrainerProfile tp)
    {
        var w = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Ethos Masterclass Series",
            Description = "Comprehensive dance workshop",
            Status = WorkshopStatus.Published,
            WorkshopDate = DateTime.UtcNow.AddDays(7).Date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            Venue = "Main Arena",
            Price = 1499m,
            Capacity = 50,
            TrainerProfileId = tp.Id,
            Timezone = "Asia/Kolkata",
            CreatedAt = DateTime.UtcNow
        };
        db.Workshops.Add(w);
        db.SaveChanges();
        return w;
    }

    private WorkshopSession CreateSession(AppDbContext db, Workshop w, TrainerProfile tp, string title, TimeSpan start, TimeSpan end, int capacity = 30, DateTime? date = null, TimeSpan? cutoffTime = null)
    {
        var s = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = w.Id,
            TrainerProfileId = tp.Id,
            Title = title,
            SessionDate = date ?? w.WorkshopDate,
            StartTime = start,
            EndTime = end,
            Capacity = capacity,
            BookingCutoffTime = cutoffTime,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.WorkshopSessions.Add(s);
        db.SaveChanges();
        return s;
    }

    // ==========================================
    // TEST 1: Multi-Session Ticket Issuance
    // ==========================================
    [Fact]
    public async Task Test01_MultiSessionTicketIssuance_CreatesTicketsPerSessionWithUniqueQrAndLinks()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);

        var s1 = CreateSession(db, w, tp, "Hip Hop Foundations", new TimeSpan(10, 0, 0), new TimeSpan(12, 0, 0));
        var s2 = CreateSession(db, w, tp, "Contemporary Flow", new TimeSpan(13, 0, 0), new TimeSpan(15, 0, 0));

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = w.Id,
            StudentProfileId = sp.Id,
            Status = WorkshopBookingStatus.Confirmed,
            Quantity = 1,
            TotalPrice = 1200m,
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);

        var bs1 = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopSessionId = s1.Id,
            Status = WorkshopBookingSessionStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };
        var bs2 = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopSessionId = s2.Id,
            Status = WorkshopBookingSessionStatus.Booked,
            CreatedAt = DateTime.UtcNow.AddSeconds(1)
        };
        db.WorkshopBookingSessions.AddRange(bs1, bs2);

        var tx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = sp.UserId,
            Amount = 1200m,
            Status = PaymentStatus.Paid,
            CreatedAt = DateTime.UtcNow
        };
        db.PaymentTransactions.Add(tx);
        await db.SaveChangesAsync();

        var ticketService = new WorkshopTicketService(db, config);
        var responses = await ticketService.IssueTicketsForBookingAsync(booking, tx, sp.User);

        Assert.Equal(2, responses.Count);
        Assert.Equal(s1.Id, responses[0].WorkshopSessionId);
        Assert.Equal(s2.Id, responses[1].WorkshopSessionId);
        Assert.EndsWith("-01", responses[0].TicketNumber);
        Assert.EndsWith("-02", responses[1].TicketNumber);
        Assert.NotNull(responses[0].QrToken);
        Assert.NotNull(responses[1].QrToken);
        Assert.NotEqual(responses[0].QrToken, responses[1].QrToken);

        // WorkshopBookingSessions linked to tickets
        var savedBs1 = await db.WorkshopBookingSessions.FindAsync(bs1.Id);
        var savedBs2 = await db.WorkshopBookingSessions.FindAsync(bs2.Id);
        Assert.Equal(responses[0].Id, savedBs1!.WorkshopTicketId);
        Assert.Equal(responses[1].Id, savedBs2!.WorkshopTicketId);
    }

    // ==========================================
    // TEST 2: Admin Session Transfer Success Path
    // ==========================================
    [Fact]
    public async Task Test02_AdminSessionTransfer_SuccessPath_UpdatesEntitlementAndIssuesReplacementTicket()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);

        var sA = CreateSession(db, w, tp, "Session A", new TimeSpan(10, 0, 0), new TimeSpan(11, 30, 0));
        var sB = CreateSession(db, w, tp, "Session B", new TimeSpan(12, 0, 0), new TimeSpan(13, 30, 0));
        var sC = CreateSession(db, w, tp, "Session C", new TimeSpan(14, 0, 0), new TimeSpan(15, 30, 0), capacity: 10);

        var pass = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = w.Id,
            Name = "2-Session Bundle",
            SessionsIncluded = 2,
            Price = 1499m,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(pass);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = w.Id,
            StudentProfileId = sp.Id,
            WorkshopPassTypeId = pass.Id,
            PassName = pass.Name,
            SessionsIncludedCount = 2,
            Status = WorkshopBookingStatus.Confirmed,
            TotalPrice = 1499m,
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);

        var bsA = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sA.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        var bsB = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sB.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        db.WorkshopBookingSessions.AddRange(bsA, bsB);

        var tx = new PaymentTransaction { Id = Guid.NewGuid(), UserId = sp.UserId, Amount = 1499m, Status = PaymentStatus.Paid, CreatedAt = DateTime.UtcNow };
        db.PaymentTransactions.Add(tx);

        var tA = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = $"ETHOS-WKS-{booking.Id.ToString()[..8].ToUpperInvariant()}-01", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = sA.Id, UserId = sp.UserId, PaymentTransactionId = tx.Id, AttendeeName = "Student A", Status = TicketStatus.Issued, QrTokenHash = "H1", IssuedAt = DateTime.UtcNow };
        var tB = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = $"ETHOS-WKS-{booking.Id.ToString()[..8].ToUpperInvariant()}-02", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = sB.Id, UserId = sp.UserId, PaymentTransactionId = tx.Id, AttendeeName = "Student B", Status = TicketStatus.Issued, QrTokenHash = "H2", IssuedAt = DateTime.UtcNow };
        db.WorkshopTickets.AddRange(tA, tB);
        bsA.WorkshopTicketId = tA.Id;
        bsB.WorkshopTicketId = tB.Id;
        await db.SaveChangesAsync();

        var auditService = new DummyAuditService();
        var ticketService = new WorkshopTicketService(db, config);
        var adminBookingService = new AdminBookingService(db, auditService, ticketService, new MockTicketPdfService());

        var adminId = Guid.NewGuid();
        var result = await adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, adminId, new AdminModifyBookingSessionRequest
        {
            CurrentTicketId = tB.Id,
            ReplacementSessionId = sC.Id
        }, CancellationToken.None);

        Assert.Equal(booking.Id, result.BookingId);
        Assert.Equal(tB.Id, result.OldTicketId);
        Assert.NotEqual(tB.Id, result.NewTicketId);
        Assert.EndsWith("-02-R01", result.NewTicketNumber);

        // Old ticket marked Replaced
        var oldTicket = await db.WorkshopTickets.FindAsync(tB.Id);
        Assert.Equal(TicketStatus.Replaced, oldTicket!.Status);

        // Old booking session marked Replaced
        var oldBs = await db.WorkshopBookingSessions.FindAsync(bsB.Id);
        Assert.Equal(WorkshopBookingSessionStatus.Replaced, oldBs!.Status);

        // New ticket issued
        var newTicket = await db.WorkshopTickets.FindAsync(result.NewTicketId);
        Assert.Equal(TicketStatus.Issued, newTicket!.Status);
        Assert.Equal(sC.Id, newTicket.WorkshopSessionId);
    }

    // ==========================================
    // TEST 3: All-Access Pass Rejection
    // ==========================================
    [Fact]
    public async Task Test03_AdminSessionTransfer_FailsFor_AllAccessPass()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);

        var s1 = CreateSession(db, w, tp, "Session 1", new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));
        var s2 = CreateSession(db, w, tp, "Session 2", new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0));

        var pass = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = w.Id,
            Name = "All-Access Pass",
            SessionsIncluded = null, // All-Access
            Price = 2999m,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(pass);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = w.Id,
            StudentProfileId = sp.Id,
            WorkshopPassTypeId = pass.Id,
            PassName = pass.Name,
            Status = WorkshopBookingStatus.Confirmed,
            TotalPrice = 2999m,
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);

        var bs1 = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = s1.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        db.WorkshopBookingSessions.Add(bs1);

        var t1 = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = "ETHOS-WKS-ALLACC01-01", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = s1.Id, UserId = sp.UserId, AttendeeName = "Student User", Status = TicketStatus.Issued, QrTokenHash = "H1", IssuedAt = DateTime.UtcNow };
        db.WorkshopTickets.Add(t1);
        bs1.WorkshopTicketId = t1.Id;
        await db.SaveChangesAsync();

        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), new WorkshopTicketService(db, config), new MockTicketPdfService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, Guid.NewGuid(), new AdminModifyBookingSessionRequest
            {
                CurrentTicketId = t1.Id,
                ReplacementSessionId = s2.Id
            }, CancellationToken.None));

        Assert.Contains("All-Access", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ==========================================
    // TEST 4: Single-Session Pass Rejection (Option A)
    // ==========================================
    [Fact]
    public async Task Test04_AdminSessionTransfer_FailsFor_SingleSessionPass_OptionA()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);

        var s1 = CreateSession(db, w, tp, "Single Session 1", new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));
        var s2 = CreateSession(db, w, tp, "Single Session 2", new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0));

        var pass = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = w.Id,
            WorkshopSessionId = s1.Id,
            Name = "Saturday 10 AM Pass",
            SessionsIncluded = 1,
            Price = 499m,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(pass);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = w.Id,
            StudentProfileId = sp.Id,
            WorkshopPassTypeId = pass.Id,
            PassName = pass.Name,
            SessionsIncludedCount = 1,
            Status = WorkshopBookingStatus.Confirmed,
            TotalPrice = 499m,
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);

        var bs1 = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = s1.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        db.WorkshopBookingSessions.Add(bs1);

        var t1 = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = "ETHOS-WKS-SINGLE01-01", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = s1.Id, UserId = sp.UserId, AttendeeName = "Student User", Status = TicketStatus.Issued, QrTokenHash = "H1", IssuedAt = DateTime.UtcNow };
        db.WorkshopTickets.Add(t1);
        bs1.WorkshopTicketId = t1.Id;
        await db.SaveChangesAsync();

        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), new WorkshopTicketService(db, config), new MockTicketPdfService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, Guid.NewGuid(), new AdminModifyBookingSessionRequest
            {
                CurrentTicketId = t1.Id,
                ReplacementSessionId = s2.Id
            }, CancellationToken.None));

        Assert.Contains("Single-Session", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ==========================================
    // TEST 5: Capacity Exhaustion Rejection
    // ==========================================
    [Fact]
    public async Task Test05_AdminSessionTransfer_FailsWhen_ReplacementSessionCapacityExhausted()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);
        var otherStudent = CreateStudent(db, "Other Student");

        var sA = CreateSession(db, w, tp, "Session A", new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));
        var sB = CreateSession(db, w, tp, "Session B", new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0));
        var sC = CreateSession(db, w, tp, "Session C (Full)", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), capacity: 1);

        // Another student already took the 1 seat of session C
        var otherBooking = new WorkshopBooking { Id = Guid.NewGuid(), WorkshopId = w.Id, StudentProfileId = otherStudent.Id, Status = WorkshopBookingStatus.Confirmed, TotalPrice = 500m, BookedAt = DateTime.UtcNow };
        db.WorkshopBookings.Add(otherBooking);
        db.WorkshopBookingSessions.Add(new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = otherBooking.Id, WorkshopSessionId = sC.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow });

        var pass = new WorkshopPassType { Id = Guid.NewGuid(), WorkshopId = w.Id, Name = "2-Session Bundle", SessionsIncluded = 2, Price = 999m, IsActive = true };
        db.WorkshopPassTypes.Add(pass);

        var booking = new WorkshopBooking { Id = Guid.NewGuid(), WorkshopId = w.Id, StudentProfileId = sp.Id, WorkshopPassTypeId = pass.Id, SessionsIncludedCount = 2, Status = WorkshopBookingStatus.Confirmed, TotalPrice = 999m, BookedAt = DateTime.UtcNow };
        db.WorkshopBookings.Add(booking);

        var bsA = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sA.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        var bsB = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sB.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        db.WorkshopBookingSessions.AddRange(bsA, bsB);

        var tB = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = "ETHOS-WKS-CAP001-02", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = sB.Id, UserId = sp.UserId, AttendeeName = "Student User", Status = TicketStatus.Issued, QrTokenHash = "H1", IssuedAt = DateTime.UtcNow };
        db.WorkshopTickets.Add(tB);
        bsB.WorkshopTicketId = tB.Id;
        await db.SaveChangesAsync();

        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), new WorkshopTicketService(db, config), new MockTicketPdfService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, Guid.NewGuid(), new AdminModifyBookingSessionRequest
            {
                CurrentTicketId = tB.Id,
                ReplacementSessionId = sC.Id
            }, CancellationToken.None));

        Assert.Contains("maximum capacity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ==========================================
    // TEST 6: Cutoff Enforcement & Admin Override
    // ==========================================
    [Fact]
    public async Task Test06_AdminSessionTransfer_EnforcesCutoff()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);

        // Session in past with cutoff in past
        var pastDate = DateTime.UtcNow.AddDays(-1).Date;
        var sA = CreateSession(db, w, tp, "Session A", new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));
        var sB = CreateSession(db, w, tp, "Session B", new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0));
        var sPast = CreateSession(db, w, tp, "Past Session", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), date: pastDate, cutoffTime: new TimeSpan(13, 0, 0));

        var pass = new WorkshopPassType { Id = Guid.NewGuid(), WorkshopId = w.Id, Name = "2-Session Bundle", SessionsIncluded = 2, Price = 999m, IsActive = true };
        db.WorkshopPassTypes.Add(pass);

        var booking = new WorkshopBooking { Id = Guid.NewGuid(), WorkshopId = w.Id, StudentProfileId = sp.Id, WorkshopPassTypeId = pass.Id, SessionsIncludedCount = 2, Status = WorkshopBookingStatus.Confirmed, TotalPrice = 999m, BookedAt = DateTime.UtcNow };
        db.WorkshopBookings.Add(booking);

        var bsA = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sA.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        var bsB = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sB.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        db.WorkshopBookingSessions.AddRange(bsA, bsB);

        var tB = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = "ETHOS-WKS-CUT001-02", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = sB.Id, UserId = sp.UserId, AttendeeName = "Student User", Status = TicketStatus.Issued, QrTokenHash = "H1", IssuedAt = DateTime.UtcNow };
        db.WorkshopTickets.Add(tB);
        bsB.WorkshopTicketId = tB.Id;
        await db.SaveChangesAsync();

        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), new WorkshopTicketService(db, config), new MockTicketPdfService());

        // 1. Without override -> fails
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, Guid.NewGuid(), new AdminModifyBookingSessionRequest
            {
                CurrentTicketId = tB.Id,
                ReplacementSessionId = sPast.Id,
                OverrideCutoff = false
            }, CancellationToken.None));
        Assert.Contains("cutoff", ex1.Message, StringComparison.OrdinalIgnoreCase);

        // 2. With override but short reason -> fails
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, Guid.NewGuid(), new AdminModifyBookingSessionRequest
            {
                CurrentTicketId = tB.Id,
                ReplacementSessionId = sPast.Id,
                OverrideCutoff = true,
                OverrideReason = "bad"
            }, CancellationToken.None));
        Assert.Contains("at least 5 characters", ex2.Message, StringComparison.OrdinalIgnoreCase);

        // 3. With override and valid reason -> succeeds
        var res = await adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, Guid.NewGuid(), new AdminModifyBookingSessionRequest
        {
            CurrentTicketId = tB.Id,
            ReplacementSessionId = sPast.Id,
            OverrideCutoff = true,
            OverrideReason = "Customer was stuck in traffic, authorized by manager"
        }, CancellationToken.None);

        Assert.NotNull(res.NewTicketNumber);
    }

    // ==========================================
    // TEST 7: Bundle N Preservation
    // ==========================================
    [Fact]
    public async Task Test07_BundleReplacement_PreservesExactN()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);

        var sA = CreateSession(db, w, tp, "Session A", new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));
        var sB = CreateSession(db, w, tp, "Session B", new TimeSpan(11, 30, 0), new TimeSpan(12, 30, 0));
        var sC = CreateSession(db, w, tp, "Session C", new TimeSpan(13, 0, 0), new TimeSpan(14, 0, 0));
        var sD = CreateSession(db, w, tp, "Session D", new TimeSpan(15, 0, 0), new TimeSpan(16, 0, 0));

        var pass = new WorkshopPassType { Id = Guid.NewGuid(), WorkshopId = w.Id, Name = "3-Session Bundle", SessionsIncluded = 3, Price = 1800m, IsActive = true };
        db.WorkshopPassTypes.Add(pass);

        var booking = new WorkshopBooking { Id = Guid.NewGuid(), WorkshopId = w.Id, StudentProfileId = sp.Id, WorkshopPassTypeId = pass.Id, SessionsIncludedCount = 3, Status = WorkshopBookingStatus.Confirmed, TotalPrice = 1800m, BookedAt = DateTime.UtcNow };
        db.WorkshopBookings.Add(booking);

        var bsA = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sA.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        var bsB = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sB.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        var bsC = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sC.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        db.WorkshopBookingSessions.AddRange(bsA, bsB, bsC);

        var tC = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = "ETHOS-WKS-BND3-03", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = sC.Id, UserId = sp.UserId, AttendeeName = "Student User", Status = TicketStatus.Issued, QrTokenHash = "H1", IssuedAt = DateTime.UtcNow };
        db.WorkshopTickets.Add(tC);
        bsC.WorkshopTicketId = tC.Id;
        await db.SaveChangesAsync();

        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), new WorkshopTicketService(db, config), new MockTicketPdfService());

        var result = await adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, Guid.NewGuid(), new AdminModifyBookingSessionRequest
        {
            CurrentTicketId = tC.Id,
            ReplacementSessionId = sD.Id
        }, CancellationToken.None);

        // Verify active sessions after replacement
        var activeSessions = await db.WorkshopBookingSessions
            .Where(bs => bs.WorkshopBookingId == booking.Id && bs.Status == WorkshopBookingSessionStatus.Booked)
            .ToListAsync();

        Assert.Equal(3, activeSessions.Count);
        Assert.Contains(activeSessions, bs => bs.WorkshopSessionId == sA.Id);
        Assert.Contains(activeSessions, bs => bs.WorkshopSessionId == sB.Id);
        Assert.Contains(activeSessions, bs => bs.WorkshopSessionId == sD.Id);
        Assert.DoesNotContain(activeSessions, bs => bs.WorkshopSessionId == sC.Id);
    }

    // ==========================================
    // TEST 8: Bundle Duplicate Rejection
    // ==========================================
    [Fact]
    public async Task Test08_BundleReplacement_RejectsDuplicateSession()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);

        var sA = CreateSession(db, w, tp, "Session A", new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));
        var sB = CreateSession(db, w, tp, "Session B", new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0));
        var sC = CreateSession(db, w, tp, "Session C", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0));

        var pass = new WorkshopPassType { Id = Guid.NewGuid(), WorkshopId = w.Id, Name = "3-Session Bundle", SessionsIncluded = 3, Price = 1800m, IsActive = true };
        db.WorkshopPassTypes.Add(pass);

        var booking = new WorkshopBooking { Id = Guid.NewGuid(), WorkshopId = w.Id, StudentProfileId = sp.Id, WorkshopPassTypeId = pass.Id, SessionsIncludedCount = 3, Status = WorkshopBookingStatus.Confirmed, TotalPrice = 1800m, BookedAt = DateTime.UtcNow };
        db.WorkshopBookings.Add(booking);

        var bsA = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sA.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        var bsB = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sB.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        var bsC = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sC.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        db.WorkshopBookingSessions.AddRange(bsA, bsB, bsC);

        var tC = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = "ETHOS-WKS-DUP001-03", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = sC.Id, UserId = sp.UserId, AttendeeName = "Student User", Status = TicketStatus.Issued, QrTokenHash = "H1", IssuedAt = DateTime.UtcNow };
        db.WorkshopTickets.Add(tC);
        bsC.WorkshopTicketId = tC.Id;
        await db.SaveChangesAsync();

        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), new WorkshopTicketService(db, config), new MockTicketPdfService());

        // Attempt to replace session C with session B (which is already active in the bundle)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, Guid.NewGuid(), new AdminModifyBookingSessionRequest
            {
                CurrentTicketId = tC.Id,
                ReplacementSessionId = sB.Id
            }, CancellationToken.None));

        Assert.Contains("already booked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ==========================================
    // TEST 9: Bundle Overlap Rejection
    // ==========================================
    [Fact]
    public async Task Test09_BundleReplacement_RejectsOverlappingSessions()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);

        var sA = CreateSession(db, w, tp, "Morning Session A", new TimeSpan(10, 0, 0), new TimeSpan(12, 0, 0));
        var sB = CreateSession(db, w, tp, "Afternoon Session B", new TimeSpan(14, 0, 0), new TimeSpan(16, 0, 0));
        // Overlapping with A: 11:00 to 13:00 on the same date
        var sOverlap = CreateSession(db, w, tp, "Overlap Session", new TimeSpan(11, 0, 0), new TimeSpan(13, 0, 0));

        var pass = new WorkshopPassType { Id = Guid.NewGuid(), WorkshopId = w.Id, Name = "2-Session Bundle", SessionsIncluded = 2, Price = 1200m, IsActive = true };
        db.WorkshopPassTypes.Add(pass);

        var booking = new WorkshopBooking { Id = Guid.NewGuid(), WorkshopId = w.Id, StudentProfileId = sp.Id, WorkshopPassTypeId = pass.Id, SessionsIncludedCount = 2, Status = WorkshopBookingStatus.Confirmed, TotalPrice = 1200m, BookedAt = DateTime.UtcNow };
        db.WorkshopBookings.Add(booking);

        var bsA = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sA.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        var bsB = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sB.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        db.WorkshopBookingSessions.AddRange(bsA, bsB);

        var tB = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = "ETHOS-WKS-OVL001-02", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = sB.Id, UserId = sp.UserId, AttendeeName = "Student User", Status = TicketStatus.Issued, QrTokenHash = "H1", IssuedAt = DateTime.UtcNow };
        db.WorkshopTickets.Add(tB);
        bsB.WorkshopTicketId = tB.Id;
        await db.SaveChangesAsync();

        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), new WorkshopTicketService(db, config), new MockTicketPdfService());

        // Replacing session B with sOverlap creates bundle { sA, sOverlap } which overlaps
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, Guid.NewGuid(), new AdminModifyBookingSessionRequest
            {
                CurrentTicketId = tB.Id,
                ReplacementSessionId = sOverlap.Id
            }, CancellationToken.None));

        Assert.Contains("overlap", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ==========================================
    // TEST 10: Replacement Ticket Monotonic Numbering Lineage
    // ==========================================
    [Fact]
    public async Task Test10_ReplacementTicket_MonotonicNumbering_Lineage()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);

        var sA = CreateSession(db, w, tp, "Session A", new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));
        var sB = CreateSession(db, w, tp, "Session B", new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0));
        var sC = CreateSession(db, w, tp, "Session C", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0));
        var sD = CreateSession(db, w, tp, "Session D", new TimeSpan(16, 0, 0), new TimeSpan(17, 0, 0));

        var pass = new WorkshopPassType { Id = Guid.NewGuid(), WorkshopId = w.Id, Name = "2-Session Bundle", SessionsIncluded = 2, Price = 1000m, IsActive = true };
        db.WorkshopPassTypes.Add(pass);

        var booking = new WorkshopBooking { Id = Guid.NewGuid(), WorkshopId = w.Id, StudentProfileId = sp.Id, WorkshopPassTypeId = pass.Id, SessionsIncludedCount = 2, Status = WorkshopBookingStatus.Confirmed, TotalPrice = 1000m, BookedAt = DateTime.UtcNow };
        db.WorkshopBookings.Add(booking);

        var bsA = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sA.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        var bsB = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sB.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        db.WorkshopBookingSessions.AddRange(bsA, bsB);

        var originalTicketNum = $"ETHOS-WKS-{booking.Id.ToString()[..8].ToUpperInvariant()}-02";
        var tB = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = originalTicketNum, WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = sB.Id, UserId = sp.UserId, AttendeeName = "Student User", Status = TicketStatus.Issued, QrTokenHash = "H1", IssuedAt = DateTime.UtcNow };
        db.WorkshopTickets.Add(tB);
        bsB.WorkshopTicketId = tB.Id;
        await db.SaveChangesAsync();

        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), new WorkshopTicketService(db, config), new MockTicketPdfService());

        // 1. First replacement: B -> C => -R01
        var res1 = await adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, Guid.NewGuid(), new AdminModifyBookingSessionRequest
        {
            CurrentTicketId = tB.Id,
            ReplacementSessionId = sC.Id
        }, CancellationToken.None);
        Assert.Equal($"{originalTicketNum}-R01", res1.NewTicketNumber);

        // 2. Second replacement on same seat: C -> D => -R02
        var res2 = await adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, Guid.NewGuid(), new AdminModifyBookingSessionRequest
        {
            CurrentTicketId = res1.NewTicketId,
            ReplacementSessionId = sD.Id
        }, CancellationToken.None);
        Assert.Equal($"{originalTicketNum}-R02", res2.NewTicketNumber);
    }

    // ==========================================
    // TEST 11: Old QR Token Invalidation
    // ==========================================
    [Fact]
    public async Task Test11_OldQrToken_Invalidation_CheckInReturnsTicketReplaced()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        w.WorkshopDate = DateTime.UtcNow.Date;
        w.StartUtc = DateTime.UtcNow.AddMinutes(-10);
        w.EndUtc = DateTime.UtcNow.AddHours(2);
        await db.SaveChangesAsync();
        var sp = CreateStudent(db);

        var sA = CreateSession(db, w, tp, "Session A", new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));
        var sB = CreateSession(db, w, tp, "Session B", new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0));
        var sC = CreateSession(db, w, tp, "Session C", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0));

        var pass = new WorkshopPassType { Id = Guid.NewGuid(), WorkshopId = w.Id, Name = "2-Session Bundle", SessionsIncluded = 2, Price = 1000m, IsActive = true };
        db.WorkshopPassTypes.Add(pass);

        var booking = new WorkshopBooking { Id = Guid.NewGuid(), WorkshopId = w.Id, StudentProfileId = sp.Id, WorkshopPassTypeId = pass.Id, SessionsIncludedCount = 2, Status = WorkshopBookingStatus.Confirmed, TotalPrice = 1000m, BookedAt = DateTime.UtcNow };
        db.WorkshopBookings.Add(booking);

        var bsA = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sA.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        var bsB = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sB.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        db.WorkshopBookingSessions.AddRange(bsA, bsB);

        var ticketService = new WorkshopTicketService(db, config);

        var tB = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = "ETHOS-WKS-QR001-02", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = sB.Id, UserId = sp.UserId, AttendeeName = "Student User", Status = TicketStatus.Issued, IssuedAt = DateTime.UtcNow, Workshop = w };
        var oldQr = ticketService.DeriveQrToken(tB);
        tB.QrTokenHash = ticketService.ComputeTokenHash(oldQr);
        db.WorkshopTickets.Add(tB);
        bsB.WorkshopTicketId = tB.Id;
        await db.SaveChangesAsync();

        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), ticketService, new MockTicketPdfService());
        var replaceRes = await adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, Guid.NewGuid(), new AdminModifyBookingSessionRequest
        {
            CurrentTicketId = tB.Id,
            ReplacementSessionId = sC.Id,
            OverrideCutoff = true,
            OverrideReason = "Test QR invalidation"
        }, CancellationToken.None);

        var adminWorkshopService = new AdminWorkshopService(db, new DummyAuditService());

        // Check in old QR token
        var checkInOld = await adminWorkshopService.CheckInWorkshopTicketAsync(w.Id, Guid.NewGuid(), new AdminCheckInTicketRequest { QrToken = oldQr }, CancellationToken.None);
        Assert.False(checkInOld.Success);
        Assert.Equal("TICKET_REPLACED", checkInOld.Code);

        // Check in new ticket
        var newTicket = await db.WorkshopTickets.FindAsync(replaceRes.NewTicketId);
        var newQr = ticketService.DeriveQrToken(newTicket!);
        var checkInNew = await adminWorkshopService.CheckInWorkshopTicketAsync(w.Id, Guid.NewGuid(), new AdminCheckInTicketRequest { QrToken = newQr }, CancellationToken.None);
        Assert.True(checkInNew.Success);
        Assert.Equal("SUCCESS", checkInNew.Code);
    }

    // ==========================================
    // TEST 12: Replacement Rollback On Error
    // ==========================================
    [Fact]
    public async Task Test12_ReplacementRollback_OnError_PreservesOriginalState()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);

        var sA = CreateSession(db, w, tp, "Session A", new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));
        var sB = CreateSession(db, w, tp, "Session B", new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0));

        var pass = new WorkshopPassType { Id = Guid.NewGuid(), WorkshopId = w.Id, Name = "2-Session Bundle", SessionsIncluded = 2, Price = 1000m, IsActive = true };
        db.WorkshopPassTypes.Add(pass);

        var booking = new WorkshopBooking { Id = Guid.NewGuid(), WorkshopId = w.Id, StudentProfileId = sp.Id, WorkshopPassTypeId = pass.Id, SessionsIncludedCount = 2, Status = WorkshopBookingStatus.Confirmed, TotalPrice = 1000m, BookedAt = DateTime.UtcNow };
        db.WorkshopBookings.Add(booking);

        var bsA = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sA.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        var bsB = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sB.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        db.WorkshopBookingSessions.AddRange(bsA, bsB);

        var tB = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = "ETHOS-WKS-RB001-02", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = sB.Id, UserId = sp.UserId, AttendeeName = "Student User", Status = TicketStatus.Issued, QrTokenHash = "H1", IssuedAt = DateTime.UtcNow };
        db.WorkshopTickets.Add(tB);
        bsB.WorkshopTicketId = tB.Id;
        await db.SaveChangesAsync();

        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), new WorkshopTicketService(db, config), new MockTicketPdfService());

        // Attempt replace with invalid Guid -> throws
        await Assert.ThrowsAnyAsync<Exception>(() =>
            adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, Guid.NewGuid(), new AdminModifyBookingSessionRequest
            {
                CurrentTicketId = tB.Id,
                ReplacementSessionId = Guid.NewGuid() // Not found
            }, CancellationToken.None));

        // Original ticket remains Issued
        var ticket = await db.WorkshopTickets.FindAsync(tB.Id);
        Assert.Equal(TicketStatus.Issued, ticket!.Status);

        // Original booking session remains Booked
        var bs = await db.WorkshopBookingSessions.FindAsync(bsB.Id);
        Assert.Equal(WorkshopBookingSessionStatus.Booked, bs!.Status);
    }

    // ==========================================
    // TEST 13: Cutoff Audit Logging
    // ==========================================
    [Fact]
    public async Task Test13_CutoffAuditLogging_RecordsAdminOldNewSessionAndReason()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);

        var sA = CreateSession(db, w, tp, "Session A", new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));
        var sB = CreateSession(db, w, tp, "Session B", new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0));
        var sPast = CreateSession(db, w, tp, "Past Session", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), date: DateTime.UtcNow.AddDays(-2).Date);

        var pass = new WorkshopPassType { Id = Guid.NewGuid(), WorkshopId = w.Id, Name = "2-Session Bundle", SessionsIncluded = 2, Price = 1000m, IsActive = true };
        db.WorkshopPassTypes.Add(pass);

        var booking = new WorkshopBooking { Id = Guid.NewGuid(), WorkshopId = w.Id, StudentProfileId = sp.Id, WorkshopPassTypeId = pass.Id, SessionsIncludedCount = 2, Status = WorkshopBookingStatus.Confirmed, TotalPrice = 1000m, BookedAt = DateTime.UtcNow };
        db.WorkshopBookings.Add(booking);

        var bsA = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sA.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        var bsB = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sB.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        db.WorkshopBookingSessions.AddRange(bsA, bsB);

        var tB = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = "ETHOS-WKS-AUD001-02", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = sB.Id, UserId = sp.UserId, AttendeeName = "Student User", Status = TicketStatus.Issued, QrTokenHash = "H1", IssuedAt = DateTime.UtcNow };
        db.WorkshopTickets.Add(tB);
        bsB.WorkshopTicketId = tB.Id;
        await db.SaveChangesAsync();

        var auditService = new DummyAuditService();
        var adminBookingService = new AdminBookingService(db, auditService, new WorkshopTicketService(db, config), new MockTicketPdfService());

        var adminId = Guid.NewGuid();
        var overrideReason = "Special exception authorized by director";
        await adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, adminId, new AdminModifyBookingSessionRequest
        {
            CurrentTicketId = tB.Id,
            ReplacementSessionId = sPast.Id,
            OverrideCutoff = true,
            OverrideReason = overrideReason
        }, CancellationToken.None);

        var log = Assert.Single(auditService.LoggedActions);
        Assert.Equal("WORKSHOP_SESSION_MODIFIED", log.ActionType);
        Assert.Equal(adminId, log.AdminUserId);
        Assert.Equal(booking.Id, log.EntityId);
        Assert.Contains(overrideReason, log.Reason);
        Assert.NotNull(log.MetadataJson);
        Assert.Contains("OverrideCutoff\":true", log.MetadataJson);
    }

    // ==========================================
    // TEST 14: Historical Records Preserved (Not Deleted)
    // ==========================================
    [Fact]
    public async Task Test14_HistoricalRecords_Preserved_NotDeleted()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);

        var sA = CreateSession(db, w, tp, "Session A", new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));
        var sB = CreateSession(db, w, tp, "Session B", new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0));
        var sC = CreateSession(db, w, tp, "Session C", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0));

        var pass = new WorkshopPassType { Id = Guid.NewGuid(), WorkshopId = w.Id, Name = "2-Session Bundle", SessionsIncluded = 2, Price = 1000m, IsActive = true };
        db.WorkshopPassTypes.Add(pass);

        var booking = new WorkshopBooking { Id = Guid.NewGuid(), WorkshopId = w.Id, StudentProfileId = sp.Id, WorkshopPassTypeId = pass.Id, SessionsIncludedCount = 2, Status = WorkshopBookingStatus.Confirmed, TotalPrice = 1000m, BookedAt = DateTime.UtcNow };
        db.WorkshopBookings.Add(booking);

        var bsA = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sA.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        var bsB = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sB.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        db.WorkshopBookingSessions.AddRange(bsA, bsB);

        var tB = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = "ETHOS-WKS-HIST001-02", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = sB.Id, UserId = sp.UserId, AttendeeName = "Student User", Status = TicketStatus.Issued, QrTokenHash = "H1", IssuedAt = DateTime.UtcNow };
        db.WorkshopTickets.Add(tB);
        bsB.WorkshopTicketId = tB.Id;
        await db.SaveChangesAsync();

        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), new WorkshopTicketService(db, config), new MockTicketPdfService());

        await adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, Guid.NewGuid(), new AdminModifyBookingSessionRequest
        {
            CurrentTicketId = tB.Id,
            ReplacementSessionId = sC.Id
        }, CancellationToken.None);

        // Verify total tickets count in DB is 2 (old + new), not 1
        var allTickets = await db.WorkshopTickets.Where(t => t.WorkshopBookingId == booking.Id).ToListAsync();
        Assert.Equal(2, allTickets.Count);
        Assert.Contains(allTickets, t => t.Id == tB.Id && t.Status == TicketStatus.Replaced);
        Assert.Contains(allTickets, t => t.Status == TicketStatus.Issued && t.WorkshopSessionId == sC.Id);

        // Verify total booking sessions count in DB is 3 (A + old B + new C)
        var allBookingSessions = await db.WorkshopBookingSessions.Where(bs => bs.WorkshopBookingId == booking.Id).ToListAsync();
        Assert.Equal(3, allBookingSessions.Count);
    }

    // ==========================================
    // TEST 15: Pricing / Payment Immutability
    // ==========================================
    [Fact]
    public async Task Test15_PricingPayment_Immutability_Untouched()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);

        var sA = CreateSession(db, w, tp, "Session A", new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));
        var sB = CreateSession(db, w, tp, "Session B", new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0));
        var sC = CreateSession(db, w, tp, "Session C", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0));

        var pass = new WorkshopPassType { Id = Guid.NewGuid(), WorkshopId = w.Id, Name = "2-Session Bundle", SessionsIncluded = 2, Price = 1299m, IsActive = true };
        db.WorkshopPassTypes.Add(pass);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = w.Id,
            StudentProfileId = sp.Id,
            WorkshopPassTypeId = pass.Id,
            PassName = pass.Name,
            PassPrice = 1299m,
            TotalPrice = 1299m,
            PriceBreakdownJson = "{\"OriginalPrice\":1299}",
            SessionsIncludedCount = 2,
            Status = WorkshopBookingStatus.Confirmed,
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);

        var tx = new PaymentTransaction { Id = Guid.NewGuid(), UserId = sp.UserId, Amount = 1299m, Status = PaymentStatus.Paid, CreatedAt = DateTime.UtcNow };
        db.PaymentTransactions.Add(tx);
        booking.PaymentTransactionId = tx.Id;

        var bsA = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sA.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        var bsB = new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = booking.Id, WorkshopSessionId = sB.Id, Status = WorkshopBookingSessionStatus.Booked, CreatedAt = DateTime.UtcNow };
        db.WorkshopBookingSessions.AddRange(bsA, bsB);

        var tB = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = "ETHOS-WKS-PRC001-02", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = sB.Id, UserId = sp.UserId, PaymentTransactionId = tx.Id, AttendeeName = "Student User", Status = TicketStatus.Issued, QrTokenHash = "H1", IssuedAt = DateTime.UtcNow };
        db.WorkshopTickets.Add(tB);
        bsB.WorkshopTicketId = tB.Id;
        await db.SaveChangesAsync();

        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), new WorkshopTicketService(db, config), new MockTicketPdfService());

        await adminBookingService.ModifyWorkshopBookingSessionAsync(booking.Id, Guid.NewGuid(), new AdminModifyBookingSessionRequest
        {
            CurrentTicketId = tB.Id,
            ReplacementSessionId = sC.Id
        }, CancellationToken.None);

        var reloaded = await db.WorkshopBookings.FindAsync(booking.Id);
        Assert.Equal(1299m, reloaded!.TotalPrice);
        Assert.Equal(1299m, reloaded.PassPrice);
        Assert.Equal("{\"OriginalPrice\":1299}", reloaded.PriceBreakdownJson);
        Assert.Equal(tx.Id, reloaded.PaymentTransactionId);

        var reloadedTx = await db.PaymentTransactions.FindAsync(tx.Id);
        Assert.Equal(1299m, reloadedTx!.Amount);
    }

    // ==========================================
    // TEST 16: Admin Authorization Enforces BookingCorrect (403)
    // ==========================================
    [Fact]
    public async Task Test16_AdminAuthorization_EnforcesBookingCorrect()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);

        var sA = CreateSession(db, w, tp, "Session A", new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));
        var sB = CreateSession(db, w, tp, "Session B", new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0));

        var booking = new WorkshopBooking { Id = Guid.NewGuid(), WorkshopId = w.Id, StudentProfileId = sp.Id, Status = WorkshopBookingStatus.Confirmed, BookedAt = DateTime.UtcNow };
        db.WorkshopBookings.Add(booking);
        await db.SaveChangesAsync();

        var authService = new MockAuthService { AllowAll = false }; // Denies permission
        var adminBookingService = new AdminBookingService(db, new DummyAuditService(), new WorkshopTicketService(db, config), new MockTicketPdfService());

        var controller = new AdminBookingsController(adminBookingService, authService, new MockRefundService(), db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.ModifyWorkshopBookingSession(booking.Id, new AdminModifyBookingSessionRequest
        {
            CurrentSessionId = sA.Id,
            ReplacementSessionId = sB.Id
        }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    // ==========================================
    // TEST 17: Student Active Ticket Filtering Excludes Replaced & Cancelled
    // ==========================================
    [Fact]
    public async Task Test17_StudentActiveTicketFiltering_ExcludesReplacedAndCancelled()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var tp = CreateTrainer(db);
        var w = CreateWorkshop(db, tp);
        var sp = CreateStudent(db);

        var s1 = CreateSession(db, w, tp, "Active Session", new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));
        var sOld = CreateSession(db, w, tp, "Old Session", new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0));
        var sCanc = CreateSession(db, w, tp, "Cancelled Session", new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0));

        var booking = new WorkshopBooking { Id = Guid.NewGuid(), WorkshopId = w.Id, StudentProfileId = sp.Id, Status = WorkshopBookingStatus.Confirmed, BookedAt = DateTime.UtcNow };
        db.WorkshopBookings.Add(booking);

        var tActive = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = "ETHOS-WKS-ACT01-01", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = s1.Id, UserId = sp.UserId, AttendeeName = "Student User", Status = TicketStatus.Issued, QrTokenHash = "H1", IssuedAt = DateTime.UtcNow };
        var tReplaced = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = "ETHOS-WKS-REP01-02", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = sOld.Id, UserId = sp.UserId, AttendeeName = "Student User", Status = TicketStatus.Replaced, QrTokenHash = "H2", IssuedAt = DateTime.UtcNow };
        var tCancelled = new WorkshopTicket { Id = Guid.NewGuid(), TicketNumber = "ETHOS-WKS-CNC01-03", WorkshopBookingId = booking.Id, WorkshopId = w.Id, WorkshopSessionId = sCanc.Id, UserId = sp.UserId, AttendeeName = "Student User", Status = TicketStatus.Cancelled, QrTokenHash = "H3", IssuedAt = DateTime.UtcNow };

        db.WorkshopTickets.AddRange(tActive, tReplaced, tCancelled);
        await db.SaveChangesAsync();

        var ticketService = new WorkshopTicketService(db, config);
        var tickets = await ticketService.GetTicketsForBookingAsync(booking.Id, sp.UserId, CancellationToken.None);

        Assert.Single(tickets);
        Assert.Equal(tActive.Id, tickets[0].Id);
        Assert.Equal("ETHOS-WKS-ACT01-01", tickets[0].TicketNumber);
    }
}
