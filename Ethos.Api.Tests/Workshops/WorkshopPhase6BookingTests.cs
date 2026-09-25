using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Application.Payments;
using Ethos.Api.Application.Students;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Notifications;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Controllers;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class WorkshopPhase6BookingTests
{
    private class DummyAuditService : IAdminAuditService
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

    private class DummyStudentEligibilityService : IStudentEligibilityService
    {
        public Task<StudentEligibilityResult> CheckEligibilityByPhoneAsync(string phone, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StudentEligibilityResult(false, false, false));

        public Task<bool> IsStudentPortalEligibleAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private class MockCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? Phone => "+919876543210";
        public string? Name => "Test Student";
        public IReadOnlyList<string> Roles => new List<string> { "STUDENT" };
        public bool IsAuthenticated => true;
    }

    private class MockNotificationService : INotificationService
    {
        public Task<IReadOnlyList<NotificationResponse>> GetMyNotificationsAsync(NotificationType? type = null) =>
            Task.FromResult<IReadOnlyList<NotificationResponse>>(new List<NotificationResponse>());
        public Task<int> GetMyUnreadCountAsync() => Task.FromResult(0);
        public Task<bool> MarkAsReadAsync(Guid notificationId) => Task.FromResult(true);
        public Task MarkAllAsReadAsync() => Task.CompletedTask;
        public Task<bool> DeleteNotificationAsync(Guid notificationId) => Task.FromResult(true);
        public Task<NotificationResponse> SendNotificationAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new NotificationResponse());
    }

    private class MockTicketService : IWorkshopTicketService
    {
        public string DeriveQrToken(WorkshopTicket ticket) => "QR-TOKEN-PHASE6";
        public string ComputeTokenHash(string rawToken) => "HASH-PHASE6";
        public Task<List<WorkshopTicketResponse>> IssueTicketsForBookingAsync(WorkshopBooking booking, PaymentTransaction transaction, User? user, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<WorkshopTicketResponse>());
        public Task<IReadOnlyList<WorkshopTicketResponse>> GetTicketsForBookingAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkshopTicketResponse>>(new List<WorkshopTicketResponse>());
        public Task<WorkshopTicketResponse> GetTicketPassAsync(Guid ticketId, Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WorkshopTicketResponse> UpdateAttendeeDetailsAsync(Guid bookingId, Guid ticketId, Guid userId, UpdateAttendeeDetailsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ResendTicketPassAsync(Guid bookingId, Guid ticketId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public string GeneratePdfDownloadToken(Guid ticketId, TimeSpan? validity = null) => "TOKEN-PDF";
        public bool ValidatePdfDownloadToken(Guid ticketId, string? token) => token == "TOKEN-PDF";
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

    private StudentProfile CreateStudent(AppDbContext db, string name = "Student User", string? phone = null)
    {
        var role = db.Roles.First(r => r.Code == "STUDENT");
        var u = new User
        {
            Id = Guid.NewGuid(),
            FullName = name,
            Phone = phone ?? $"+9198{Random.Shared.Next(10000000, 99999999)}",
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
        return sp;
    }

    private (WorkshopService Service, AppDbContext Db, MockCurrentUserService CurrentUser) CreateService(AppDbContext? db = null)
    {
        var dbContext = db ?? CreateDbContext();
        var currentUser = new MockCurrentUserService();

        var studentRole = dbContext.Roles.First(r => r.Code == "STUDENT");
        var defaultUser = new User
        {
            Id = currentUser.UserId,
            FullName = "Logged In Student",
            Phone = "+919876543210",
            CustomerCode = "STU001",
            CreatedAt = DateTime.UtcNow
        };
        defaultUser.UserRoles.Add(new UserRole { UserId = defaultUser.Id, RoleId = studentRole.Id, Role = studentRole });
        dbContext.Users.Add(defaultUser);

        var defaultSp = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = defaultUser.Id,
            User = defaultUser,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.StudentProfiles.Add(defaultSp);
        dbContext.SaveChanges();

        var razorpaySettings = Options.Create(new RazorpaySettings
        {
            KeyId = "rzp_test_phase6key",
            KeySecret = "test_secret",
            WebhookSecret = "test_webhook_secret"
        });

        var ticketService = new MockTicketService();
        var notificationService = new MockNotificationService();
        var pricingService = new WorkshopPricingService(dbContext, new DummyStudentEligibilityService());
        var fulfillmentService = new PaymentFulfillmentService(
            dbContext,
            ticketService,
            razorpaySettings,
            notificationService,
            NullLogger<PaymentFulfillmentService>.Instance);

        var service = new WorkshopService(
            dbContext,
            currentUser,
            pricingService,
            notificationService,
            ticketService,
            fulfillmentService,
            razorpaySettings,
            new TestWebHostEnvironment());

        return (service, dbContext, currentUser);
    }

    private async Task<(Workshop Workshop, List<WorkshopSession> Sessions, TrainerProfile Trainer)> CreateBaseWorkshopAsync(
        AppDbContext db,
        int sessionCount = 3)
    {
        var trainerRole = await db.Roles.FirstAsync(r => r.Code == "TRAINER");
        var trainerUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Master Choreographer",
            Email = "choreographer@ethos.test",
            Phone = "+919876543210",
            CustomerCode = "TRN01",
            CreatedAt = DateTime.UtcNow
        };
        trainerUser.UserRoles.Add(new UserRole { UserId = trainerUser.Id, RoleId = trainerRole.Id, Role = trainerRole });
        db.Users.Add(trainerUser);

        var trainer = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            UserId = trainerUser.Id,
            User = trainerUser,
            TrainerCode = "TRN01",
            FullName = "Master Choreographer",
            Bio = "Lead Instructor",
            Status = TrainerStatus.Active
        };
        db.TrainerProfiles.Add(trainer);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Urban Fusion Masterclass",
            Description = "Full weekend intensive",
            Venue = "Ethos Main Arena",
            Capacity = 30,
            Price = 1200m,
            Status = WorkshopStatus.Published,
            WorkshopDate = DateTime.UtcNow.AddDays(7).Date,
            Timezone = "Asia/Kolkata",
            TrainerProfileId = trainer.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.Workshops.Add(workshop);

        var sessions = new List<WorkshopSession>();
        for (int i = 0; i < sessionCount; i++)
        {
            var session = new WorkshopSession
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                Title = $"Session {i + 1}",
                SessionDate = workshop.WorkshopDate,
                StartTime = new TimeSpan(10 + (i * 2), 0, 0),
                EndTime = new TimeSpan(11 + (i * 2), 30, 0),
                Capacity = 10,
                IsActive = true,
                DisplayOrder = i + 1,
                TrainerProfileId = trainer.Id
            };
            sessions.Add(session);
            db.WorkshopSessions.Add(session);
        }

        await db.SaveChangesAsync();
        return (workshop, sessions, trainer);
    }

    [Fact]
    public async Task Scenario1_AdvisoryQuoteVsAuthoritativeOrder_DynamicPricingRecalculationUnderLock()
    {
        // Pass has Tier 1 (1-2 @ 500) and Tier 2 (3-5 @ 700)
        var (service, db, currentUser) = CreateService();
        var (workshop, sessions, _) = await CreateBaseWorkshopAsync(db);

        var pass = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "Dynamic Single Pass",
            Price = 500m,
            TotalQuantity = 10,
            WorkshopSessionId = sessions[0].Id,
            DisplayOrder = 1,
            IsActive = true,
            PricingTiers = new List<WorkshopPricingTier>
            {
                new() { TierNumber = 1, TierName = "Tier 1", Price = 500m, MinTickets = 1, MaxTickets = 2 },
                new() { TierNumber = 2, TierName = "Tier 2", Price = 700m, MinTickets = 3, MaxTickets = 5 },
                new() { TierNumber = 3, TierName = "Tier 3", Price = 900m, MinTickets = 6, MaxTickets = null }
            }
        };
        db.WorkshopPassTypes.Add(pass);
        await db.SaveChangesAsync();

        // 1. Initial quote for Q=1 when sold=0 -> Unit price 500
        var quoteInitial = await service.GetWorkshopQuoteAsync(workshop.Id, 1, pass.Id, new List<Guid> { sessions[0].Id });
        Assert.Equal(500m, quoteInitial.TotalAmount);

        // 2. Intervening booking occurs: 2 tickets sold at Tier 1
        var intermediateStudent = CreateStudent(db, "Intervening Student");
        var interveningBooking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = intermediateStudent.Id,
            WorkshopPassTypeId = pass.Id,
            PassName = pass.Name,
            Quantity = 2,
            TotalPrice = 1000m,
            Status = WorkshopBookingStatus.Confirmed,
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(interveningBooking);
        await db.SaveChangesAsync();

        // 3. Customer places order for Q=1. The server must ignore stale client quote of 500 and calculate 700 (Tier 2)!
        var orderResponse = await service.CreateWorkshopOrderAsync(workshop.Id, new CreateWorkshopOrderRequest
        {
            Quantity = 1,
            PassTypeId = pass.Id,
            SelectedSessionIds = new List<Guid> { sessions[0].Id },
            FullName = "Aarav Sharma",
            Phone = "+919876543211",
            Email = "aarav@test.com"
        });

        Assert.Equal(700m, orderResponse.Amount);

        var savedBooking = await db.WorkshopBookings.FirstOrDefaultAsync(b => b.Id == orderResponse.BookingId);
        Assert.NotNull(savedBooking);
        Assert.Equal(700m, savedBooking.TotalPrice);
    }

    [Fact]
    public async Task Scenario2_SelectedMultiSessionBundle_CapacityCheck_AllowedVsRejected()
    {
        // Bundle N=2. Session A (Cap=10, 5 booked), Session B (Cap=10, 10 booked -> FULL), Session C (Cap=10, 2 booked)
        var (service, db, _) = CreateService();
        var (workshop, sessions, _) = await CreateBaseWorkshopAsync(db, sessionCount: 3);

        var sessionA = sessions[0];
        var sessionB = sessions[1];
        var sessionC = sessions[2];

        // Fill Session B to capacity
        var student = CreateStudent(db, "Booked Student");
        for (int i = 0; i < 10; i++)
        {
            var b = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                StudentProfileId = student.Id,
                Quantity = 1,
                TotalPrice = 500m,
                Status = WorkshopBookingStatus.Confirmed
            };
            db.WorkshopBookings.Add(b);
            db.WorkshopBookingSessions.Add(new WorkshopBookingSession
            {
                Id = Guid.NewGuid(),
                WorkshopBookingId = b.Id,
                WorkshopSessionId = sessionB.Id,
                Status = WorkshopBookingSessionStatus.Booked
            });
        }
        await db.SaveChangesAsync();

        var bundlePass = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "2-Session Bundle",
            Price = 1100m,
            TotalQuantity = 20,
            SessionsIncluded = 2,
            DisplayOrder = 2,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(bundlePass);
        await db.SaveChangesAsync();

        // A + C selection is valid and has seats
        var quoteAc = await service.GetWorkshopQuoteAsync(workshop.Id, 1, bundlePass.Id, new List<Guid> { sessionA.Id, sessionC.Id });
        Assert.Equal(1100m, quoteAc.TotalAmount);

        var orderAc = await service.CreateWorkshopOrderAsync(workshop.Id, new CreateWorkshopOrderRequest
        {
            Quantity = 1,
            PassTypeId = bundlePass.Id,
            SelectedSessionIds = new List<Guid> { sessionA.Id, sessionC.Id },
            FullName = "Rohan Verma",
            Phone = "+919876543222",
            Email = "rohan@test.com"
        });
        Assert.NotNull(orderAc);
        Assert.Equal(1100m, orderAc.Amount);

        // A + B selection must be rejected because Session B is full!
        var exQuoteAb = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetWorkshopQuoteAsync(workshop.Id, 1, bundlePass.Id, new List<Guid> { sessionA.Id, sessionB.Id }));
        Assert.Contains("Session 'Session 2' has only 0 seats remaining", exQuoteAb.Message);

        var exOrderAb = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateWorkshopOrderAsync(workshop.Id, new CreateWorkshopOrderRequest
            {
                Quantity = 1,
                PassTypeId = bundlePass.Id,
                SelectedSessionIds = new List<Guid> { sessionA.Id, sessionB.Id },
                FullName = "Meera Nair",
                Phone = "+919876543233",
                Email = "meera@test.com"
            }));
        Assert.Contains("Session 'Session 2' has only 0 seats remaining", exOrderAb.Message);
    }

    [Fact]
    public async Task Scenario3_MultiSessionBundle_QuantityMultiplier_EnforcesSeatsPerSession()
    {
        // Session A has 2 remaining seats. Customer attempts to book Q=3 bundles for A+C.
        var (service, db, _) = CreateService();
        var (workshop, sessions, _) = await CreateBaseWorkshopAsync(db, sessionCount: 2);
        var sessionA = sessions[0];
        var sessionB = sessions[1];

        sessionA.Capacity = 2; // only 2 seats total!
        sessionB.Capacity = 10;
        await db.SaveChangesAsync();

        var bundlePass = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "2-Session Intensive",
            Price = 1000m,
            TotalQuantity = 10,
            SessionsIncluded = 2,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(bundlePass);
        await db.SaveChangesAsync();

        // Requesting Q=3 bundles requires 3 seats in Session A!
        var exQuote = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetWorkshopQuoteAsync(workshop.Id, 3, bundlePass.Id, new List<Guid> { sessionA.Id, sessionB.Id }));
        Assert.Contains("Session 'Session 1' has only 2 seats remaining", exQuote.Message);

        var exOrder = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateWorkshopOrderAsync(workshop.Id, new CreateWorkshopOrderRequest
            {
                Quantity = 3,
                PassTypeId = bundlePass.Id,
                SelectedSessionIds = new List<Guid> { sessionA.Id, sessionB.Id },
                FullName = "Pooja Hegde",
                Phone = "+919876543244",
                Email = "pooja@test.com"
            }));
        Assert.Contains("Session 'Session 1' has only 2 seats remaining", exOrder.Message);
    }

    [Fact]
    public async Task Scenario4_BundleSessionTimeOverlap_HalfOpenBoundaryRules()
    {
        // Touching sessions [10:00, 11:00) and [11:00, 12:00) allowed!
        // Intersecting sessions [10:00, 11:30) and [11:00, 12:30) rejected!
        var (service, db, _) = CreateService();
        var (workshop, _, trainer) = await CreateBaseWorkshopAsync(db, sessionCount: 0);

        var touchingS1 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Morning Stretch",
            SessionDate = workshop.WorkshopDate,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            Capacity = 10,
            IsActive = true,
            TrainerProfileId = trainer.Id
        };
        var touchingS2 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Groove Foundations",
            SessionDate = workshop.WorkshopDate,
            StartTime = new TimeSpan(11, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Capacity = 10,
            IsActive = true,
            TrainerProfileId = trainer.Id
        };

        var overlappingS3 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Floorwork Essentials",
            SessionDate = workshop.WorkshopDate,
            StartTime = new TimeSpan(10, 30, 0),
            EndTime = new TimeSpan(11, 45, 0),
            Capacity = 10,
            IsActive = true,
            TrainerProfileId = trainer.Id
        };

        db.WorkshopSessions.AddRange(touchingS1, touchingS2, overlappingS3);

        var bundlePass = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "Duo Pass",
            Price = 900m,
            TotalQuantity = 10,
            SessionsIncluded = 2,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(bundlePass);
        await db.SaveChangesAsync();

        // 1. Touching bounds: Allowed
        var quoteTouching = await service.GetWorkshopQuoteAsync(
            workshop.Id, 1, bundlePass.Id, new List<Guid> { touchingS1.Id, touchingS2.Id });
        Assert.Equal(900m, quoteTouching.TotalAmount);

        var orderTouching = await service.CreateWorkshopOrderAsync(workshop.Id, new CreateWorkshopOrderRequest
        {
            Quantity = 1,
            PassTypeId = bundlePass.Id,
            SelectedSessionIds = new List<Guid> { touchingS1.Id, touchingS2.Id },
            FullName = "Ananya Roy",
            Phone = "+919876543255",
            Email = "ananya@test.com"
        });
        Assert.NotNull(orderTouching);

        // 2. Intersecting bounds: Rejected with clear overlap message
        var exQuoteOverlap = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetWorkshopQuoteAsync(
                workshop.Id, 1, bundlePass.Id, new List<Guid> { touchingS1.Id, overlappingS3.Id }));
        Assert.Contains("overlap in time", exQuoteOverlap.Message);

        var exOrderOverlap = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateWorkshopOrderAsync(workshop.Id, new CreateWorkshopOrderRequest
            {
                Quantity = 1,
                PassTypeId = bundlePass.Id,
                SelectedSessionIds = new List<Guid> { touchingS1.Id, overlappingS3.Id },
                FullName = "Sameer Sen",
                Phone = "+919876543266",
                Email = "sameer@test.com"
            }));
        Assert.Contains("overlap in time", exOrderOverlap.Message);
    }

    [Fact]
    public async Task Scenario5_AllAccessPass_CutoffValidation_ClosedIfAnySessionPastCutoff()
    {
        var (service, db, _) = CreateService();
        var (workshop, sessions, _) = await CreateBaseWorkshopAsync(db, sessionCount: 2);

        // Make Session 1 past cutoff
        sessions[0].SessionDate = DateTime.UtcNow.AddDays(-1).Date; // past date!
        await db.SaveChangesAsync();

        var allAccessPass = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "VIP All Access",
            Price = 2500m,
            TotalQuantity = 10,
            SessionsIncluded = null,
            WorkshopSessionId = null,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(allAccessPass);
        await db.SaveChangesAsync();

        var exQuote = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetWorkshopQuoteAsync(workshop.Id, 1, allAccessPass.Id, null));
        Assert.Contains("All-Access is unavailable because session 'Session 1' has passed its booking cutoff", exQuote.Message);

        var exOrder = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateWorkshopOrderAsync(workshop.Id, new CreateWorkshopOrderRequest
            {
                Quantity = 1,
                PassTypeId = allAccessPass.Id,
                FullName = "Kavya Menon",
                Phone = "+919876543277",
                Email = "kavya@test.com"
            }));
        Assert.Contains("All-Access is unavailable because session 'Session 1' has passed its booking cutoff", exOrder.Message);
    }

    [Fact]
    public async Task Scenario6_PriceTamperingImmunity_ServerAuthoritativePriceOnly()
    {
        var (service, db, _) = CreateService();
        var (workshop, sessions, _) = await CreateBaseWorkshopAsync(db, sessionCount: 1);

        var pass = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "Standard Single",
            Price = 850m,
            TotalQuantity = 10,
            WorkshopSessionId = sessions[0].Id,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(pass);
        await db.SaveChangesAsync();

        // Create order request — note that CreateWorkshopOrderRequest doesn't even accept client price,
        // but even if an attacker modified the client HTTP payload, the server queries the pass and sets 850.
        var orderResponse = await service.CreateWorkshopOrderAsync(workshop.Id, new CreateWorkshopOrderRequest
        {
            Quantity = 2,
            PassTypeId = pass.Id,
            SelectedSessionIds = new List<Guid> { sessions[0].Id },
            FullName = "Vikram Reddy",
            Phone = "+919876543288",
            Email = "vikram@test.com"
        });

        Assert.Equal(1700m, orderResponse.Amount);
        var booking = await db.WorkshopBookings.FirstOrDefaultAsync(b => b.Id == orderResponse.BookingId);
        Assert.NotNull(booking);
        Assert.Equal(1700m, booking.TotalPrice);
    }

    [Fact]
    public async Task Scenario7_IdempotencyFingerprint_MatchSucceeds_MismatchReturns409Conflict()
    {
        var (service, db, _) = CreateService();
        var (workshop, sessions, _) = await CreateBaseWorkshopAsync(db, sessionCount: 2);

        var pass = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "Standard Single",
            Price = 600m,
            TotalQuantity = 10,
            WorkshopSessionId = sessions[0].Id,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(pass);
        await db.SaveChangesAsync();

        var key = "idempotency-key-" + Guid.NewGuid().ToString("N");

        // 1. First order creation
        var order1 = await service.CreateWorkshopOrderAsync(workshop.Id, new CreateWorkshopOrderRequest
        {
            Quantity = 1,
            PassTypeId = pass.Id,
            SelectedSessionIds = new List<Guid> { sessions[0].Id },
            IdempotencyKey = key,
            FullName = "Deepak Patel",
            Phone = "+919876543299",
            Email = "deepak@test.com"
        });
        Assert.NotNull(order1);

        // 2. Exactly matching repeat request returns the existing order
        var order2 = await service.CreateWorkshopOrderAsync(workshop.Id, new CreateWorkshopOrderRequest
        {
            Quantity = 1,
            PassTypeId = pass.Id,
            SelectedSessionIds = new List<Guid> { sessions[0].Id },
            IdempotencyKey = key,
            FullName = "Deepak Patel",
            Phone = "+919876543299",
            Email = "deepak@test.com"
        });
        Assert.Equal(order1.BookingId, order2.BookingId);

        // 3. Altered request (Quantity changed from 1 to 2) with the same key throws InvalidOperationException
        var exChanged = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateWorkshopOrderAsync(workshop.Id, new CreateWorkshopOrderRequest
            {
                Quantity = 2, // Changed!
                PassTypeId = pass.Id,
                SelectedSessionIds = new List<Guid> { sessions[0].Id },
                IdempotencyKey = key,
                FullName = "Deepak Patel",
                Phone = "+919876543299",
                Email = "deepak@test.com"
            }));
        Assert.Contains("Idempotency key was previously used with different order parameters", exChanged.Message);

        // 4. Test WorkshopsController maps this exception to 409 Conflict
        var controller = new WorkshopsController(service);
        var actionResult = await controller.CreateWorkshopOrder(workshop.Id, new CreateWorkshopOrderRequest
        {
            Quantity = 2,
            PassTypeId = pass.Id,
            SelectedSessionIds = new List<Guid> { sessions[0].Id },
            IdempotencyKey = key,
            FullName = "Deepak Patel",
            Phone = "+919876543299",
            Email = "deepak@test.com"
        }, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(actionResult.Result);
        Assert.Equal(409, conflictResult.StatusCode);
    }

    [Fact]
    public async Task Scenario8_FifteenMinuteReservationHold_ExpiredHoldReleasesSeat()
    {
        var (service, db, _) = CreateService();
        var (workshop, sessions, _) = await CreateBaseWorkshopAsync(db, sessionCount: 1);
        var session = sessions[0];
        session.Capacity = 1; // Only 1 seat available!
        await db.SaveChangesAsync();

        var pass = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "Single Seat Pass",
            Price = 500m,
            TotalQuantity = 1,
            WorkshopSessionId = session.Id,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(pass);
        await db.SaveChangesAsync();

        // Expired hold (reservation expired 2 minutes ago)
        var student = CreateStudent(db, "Expired Student");
        var expiredBooking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = student.Id,
            WorkshopPassTypeId = pass.Id,
            PassName = pass.Name,
            Quantity = 1,
            TotalPrice = 500m,
            Status = WorkshopBookingStatus.PendingPayment,
            ReservationExpiresAt = DateTime.UtcNow.AddMinutes(-2), // Expired!
            BookedAt = DateTime.UtcNow.AddMinutes(-17)
        };
        db.WorkshopBookings.Add(expiredBooking);
        db.WorkshopBookingSessions.Add(new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = expiredBooking.Id,
            WorkshopSessionId = session.Id,
            Status = WorkshopBookingSessionStatus.Booked
        });
        await db.SaveChangesAsync();

        // Because previous hold is expired, new customer should be able to quote and book the seat!
        var quote = await service.GetWorkshopQuoteAsync(workshop.Id, 1, pass.Id, new List<Guid> { session.Id });
        Assert.Equal(500m, quote.TotalAmount);

        var newOrder = await service.CreateWorkshopOrderAsync(workshop.Id, new CreateWorkshopOrderRequest
        {
            Quantity = 1,
            PassTypeId = pass.Id,
            SelectedSessionIds = new List<Guid> { session.Id },
            FullName = "Pravin Das",
            Phone = "+919876543200",
            Email = "pravin@test.com"
        });

        Assert.NotNull(newOrder);
        Assert.Equal(500m, newOrder.Amount);
    }
}
