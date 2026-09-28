using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Application.Payments;
using Ethos.Api.Application.Students;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Notifications;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class WorkshopCustomerSelectableSoloTests
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
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PagedResult<AdminAuditLogResponse>> GetAuditLogsAsync(
            int page, int pageSize, string? category = null, string? actionType = null,
            string? entityType = null, Guid? entityId = null, Guid? adminUserId = null,
            string? traceId = null, DateTime? startDate = null, DateTime? endDate = null,
            CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<AdminAuditLogResponse> { Items = new List<AdminAuditLogResponse>() });

        public Task<AdminAuditLogResponse?> GetAuditLogByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<AdminAuditLogResponse?>(null);

        public Task<PagedResult<AdminSecurityEventResponse>> GetSecurityEventsAsync(
            int page, int pageSize, string? eventType = null, string? severity = null,
            Guid? userId = null, string? traceId = null, DateTime? startDate = null,
            DateTime? endDate = null, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<AdminSecurityEventResponse> { Items = new List<AdminSecurityEventResponse>() });

        public Task<AdminSecurityEventResponse?> GetSecurityEventByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<AdminSecurityEventResponse?>(null);
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
        public string DeriveQrToken(WorkshopTicket ticket) => "QR-TOKEN";
        public string ComputeTokenHash(string rawToken) => "HASH";
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

    private class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Ethos.Api";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options);
        db.Roles.Add(new Role { Id = Guid.NewGuid(), Code = "STUDENT", Name = "Student" });
        db.Roles.Add(new Role { Id = Guid.NewGuid(), Code = "ADMIN", Name = "Admin" });
        db.SaveChanges();
        return db;
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
            KeySecret = "rzp_test_secret",
            WebhookSecret = "webhook_secret"
        });

        var pricingService = new WorkshopPricingService(dbContext, new DummyStudentEligibilityService());
        var notificationService = new MockNotificationService();
        var ticketService = new MockTicketService();
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

    private async Task<(Guid adminId, Guid trainerId, Guid workshopId, Guid sess1Id, Guid sess2Id, Guid sess3Id, Guid soloPassId)> SeedWorkshopWithSessionsAsync(AppDbContext db, WorkshopStatus status = WorkshopStatus.Published)
    {
        var adminId = Guid.NewGuid();
        var adminRole = await db.Roles.FirstAsync(r => r.Code == "ADMIN");
        var adminUser = new User
        {
            Id = adminId,
            Phone = "+919999999999",
            FullName = "Admin User",
            CustomerCode = "ADM01",
            CreatedAt = DateTime.UtcNow
        };
        adminUser.UserRoles.Add(new UserRole { UserId = adminId, RoleId = adminRole.Id, Role = adminRole });
        db.Users.Add(adminUser);

        var trainer = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            UserId = adminId,
            FullName = "Test Trainer",
            TrainerCode = "TRN-001"
        };
        db.TrainerProfiles.Add(trainer);

        var workshopId = Guid.NewGuid();
        var tomorrow = DateTime.UtcNow.Date.AddDays(2);
        var workshop = new Workshop
        {
            Id = workshopId,
            Title = "Master Dance Experience",
            Description = "Comprehensive workshop with multi-session capability",
            Venue = "Main Hall",
            City = "Hyderabad",
            DanceStyle = "Hip Hop",
            Level = "All Levels",
            Price = 600m,
            Capacity = 30,
            ImageUrl = "https://example.com/poster.jpg",
            Status = status,
            WorkshopDate = tomorrow,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            StartUtc = tomorrow.ToUniversalTime().AddHours(10),
            EndUtc = tomorrow.ToUniversalTime().AddHours(18),
            TrainerProfileId = trainer.Id,
            IsEthosOriginal = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Workshops.Add(workshop);

        var s1Id = Guid.NewGuid();
        var s2Id = Guid.NewGuid();
        var s3Id = Guid.NewGuid();

        var sessions = new List<WorkshopSession>
        {
            new() { Id = s1Id, WorkshopId = workshopId, TrainerProfileId = trainer.Id, Title = "Session 1 - Pranay", SessionDate = tomorrow, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(12, 0, 0), Capacity = 30, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = s2Id, WorkshopId = workshopId, TrainerProfileId = trainer.Id, Title = "Session 2 - Uttej", SessionDate = tomorrow, StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(15, 0, 0), Capacity = 30, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = s3Id, WorkshopId = workshopId, TrainerProfileId = trainer.Id, Title = "Session 3 - Pavan", SessionDate = tomorrow, StartTime = new TimeSpan(16, 0, 0), EndTime = new TimeSpan(18, 0, 0), Capacity = 30, IsActive = true, CreatedAt = DateTime.UtcNow }
        };
        db.WorkshopSessions.AddRange(sessions);
        workshop.Sessions = sessions;

        var soloPassId = Guid.NewGuid();
        var soloPass = new WorkshopPassType
        {
            Id = soloPassId,
            WorkshopId = workshopId,
            Name = "Solo Pass",
            Description = "Attend any 1 session of your choice",
            Price = 600m,
            SessionsIncluded = 1,
            WorkshopSessionId = null, // Customer selects session during booking!
            TotalQuantity = 50,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(soloPass);
        workshop.PassTypes = new List<WorkshopPassType> { soloPass };

        var pricingTier = new WorkshopPricingTier
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            WorkshopPassTypeId = null,
            TierNumber = 1,
            TierName = "Standard",
            MinTickets = 1,
            MaxTickets = null,
            Price = 600m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.WorkshopPricingTiers.Add(pricingTier);
        workshop.PricingTiers = new List<WorkshopPricingTier> { pricingTier };

        await db.SaveChangesAsync();
        return (adminId, trainer.Id, workshopId, s1Id, s2Id, s3Id, soloPassId);
    }

    [Fact]
    public async Task Admin_Can_Create_Solo_Pass_With_Null_WorkshopSessionId()
    {
        var db = CreateDbContext();
        var audit = new DummyAuditService();
        var adminService = new AdminWorkshopService(db, audit);

        var (adminId, trainerId, _, _, _, _, _) = await SeedWorkshopWithSessionsAsync(db);

        var tomorrow = DateTime.UtcNow.Date.AddDays(5);
        var createReq = new AdminCreateWorkshopRequest
        {
            Title = "Brand New Workshop",
            Description = "Description for workshop",
            Venue = "Ethos Studio",
            City = "Hyderabad",
            DanceStyle = "Hip Hop",
            Level = "All Levels",
            Price = 500m,
            Capacity = 25,
            WorkshopDate = tomorrow,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(16, 0, 0),
            TrainerProfileId = trainerId,
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new() { Title = "Session A", SessionDate = tomorrow, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(12, 0, 0), Capacity = 25 },
                new() { Title = "Session B", SessionDate = tomorrow, StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(16, 0, 0), Capacity = 25 }
            },
            PassTypes = new List<AdminWorkshopPassTypeItem>
            {
                new()
                {
                    Name = "Solo Pass",
                    Price = 500m,
                    TotalQuantity = 50,
                    WorkshopSessionId = null, // Customer-selectable Solo pass!
                    SessionsIncluded = 1,
                    PricingTiers = new List<AdminWorkshopPricingTierItem>
                    {
                        new() { TierNumber = 1, TierName = "Standard", MinTickets = 1, MaxTickets = null, Price = 500m }
                    }
                }
            }
        };

        var created = await adminService.CreateWorkshopAsync(adminId, createReq, CancellationToken.None);
        Assert.NotNull(created);
        Assert.Single(created.PassTypes);
        var pass = created.PassTypes.First();
        Assert.Equal("Solo Pass", pass.Name);
        Assert.Equal(1, pass.SessionsIncluded);
        Assert.Null(pass.WorkshopSessionId);
    }

    [Fact]
    public async Task Admin_Can_Update_Solo_Pass_With_Null_WorkshopSessionId()
    {
        var db = CreateDbContext();
        var audit = new DummyAuditService();
        var adminService = new AdminWorkshopService(db, audit);

        var (adminId, _, workshopId, s1, s2, _, soloPassId) = await SeedWorkshopWithSessionsAsync(db);

        var updateReq = new AdminUpdateWorkshopRequest
        {
            Title = "Updated Workshop Title",
            Description = "Updated Description",
            Venue = "Main Studio",
            City = "Hyderabad",
            DanceStyle = "Hip Hop",
            Level = "All Levels",
            Price = 550m,
            Capacity = 30,
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new() { Id = s1, Title = "Session 1", SessionDate = DateTime.UtcNow.Date.AddDays(2), StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(12, 0, 0), Capacity = 30 },
                new() { Id = s2, Title = "Session 2", SessionDate = DateTime.UtcNow.Date.AddDays(2), StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(15, 0, 0), Capacity = 30 }
            },
            PassTypes = new List<AdminWorkshopPassTypeItem>
            {
                new()
                {
                    Id = soloPassId,
                    Name = "Solo Pass - Any 1 Session",
                    Price = 550m,
                    TotalQuantity = 40,
                    WorkshopSessionId = null,
                    SessionsIncluded = 1,
                    PricingTiers = new List<AdminWorkshopPricingTierItem>
                    {
                        new() { TierNumber = 1, TierName = "Standard", MinTickets = 1, MaxTickets = null, Price = 550m }
                    }
                }
            }
        };

        var updated = await adminService.UpdateWorkshopAsync(workshopId, adminId, updateReq, CancellationToken.None);
        Assert.NotNull(updated);
        var pass = updated.PassTypes.First(p => p.Id == soloPassId);
        Assert.Equal("Solo Pass - Any 1 Session", pass.Name);
        Assert.Equal(1, pass.SessionsIncluded);
        Assert.Null(pass.WorkshopSessionId);
    }

    [Fact]
    public async Task Admin_Can_Publish_Workshop_Containing_Customer_Selectable_Solo_Pass()
    {
        var db = CreateDbContext();
        var audit = new DummyAuditService();
        var adminService = new AdminWorkshopService(db, audit);

        var (adminId, _, workshopId, _, _, _, _) = await SeedWorkshopWithSessionsAsync(db, status: WorkshopStatus.Draft);

        // Workshop status starts as Draft; publishing should succeed without requiring WorkshopSessionId
        await adminService.PublishWorkshopAsync(workshopId, adminId, CancellationToken.None);
        var workshop = await db.Workshops.FindAsync(workshopId);
        Assert.NotNull(workshop);
        Assert.Equal(WorkshopStatus.Published, workshop.Status);
    }

    [Fact]
    public async Task Customer_Can_Select_Exactly_One_Session_For_Solo_Pass()
    {
        var (workshopService, db, _) = CreateService();
        var (_, _, workshopId, s1Id, s2Id, _, soloPassId) = await SeedWorkshopWithSessionsAsync(db);

        // 1. Get quote for Session 2
        var quote = await workshopService.GetWorkshopQuoteAsync(
            workshopId,
            quantity: 1,
            passTypeId: soloPassId,
            selectedSessionIds: new List<Guid> { s2Id },
            CancellationToken.None);

        Assert.Equal(600m, quote.TotalAmount);

        // 2. Create order for Session 2
        var order = await workshopService.CreateWorkshopOrderAsync(
            workshopId,
            new CreateWorkshopOrderRequest
            {
                Quantity = 1,
                FullName = "Dance Enthusiast",
                Phone = "+919876543210",
                Email = "dancer@example.com",
                PassTypeId = soloPassId,
                SelectedSessionIds = new List<Guid> { s2Id },
                IdempotencyKey = Guid.NewGuid().ToString()
            },
            CancellationToken.None);

        Assert.NotNull(order);
        Assert.Equal(600m, order.Amount);

        // Verify booking session record was created specifically for Session 2
        var booking = await db.WorkshopBookings
            .Include(b => b.BookingSessions)
            .FirstOrDefaultAsync(b => b.Id == order.BookingId);

        Assert.NotNull(booking);
        Assert.Equal(1, booking.SessionsIncludedCount);
        Assert.Single(booking.BookingSessions);
        Assert.Equal(s2Id, booking.BookingSessions.First().WorkshopSessionId);
    }

    [Fact]
    public async Task Customer_Cannot_Select_Two_Sessions_For_Solo_Pass()
    {
        var (workshopService, db, _) = CreateService();
        var (_, _, workshopId, s1Id, s2Id, _, soloPassId) = await SeedWorkshopWithSessionsAsync(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            workshopService.CreateWorkshopOrderAsync(
                workshopId,
                new CreateWorkshopOrderRequest
                {
                    Quantity = 1,
                    FullName = "Rule Breaker",
                    Phone = "+919876543210",
                    PassTypeId = soloPassId,
                    SelectedSessionIds = new List<Guid> { s1Id, s2Id }, // 2 sessions for Solo!
                    IdempotencyKey = Guid.NewGuid().ToString()
                },
                CancellationToken.None));

        Assert.Contains("Please select exactly 1 sessions", ex.Message);
    }

    [Fact]
    public async Task Customer_Cannot_Select_Zero_Sessions_For_Solo_Pass()
    {
        var (workshopService, db, _) = CreateService();
        var (_, _, workshopId, _, _, _, soloPassId) = await SeedWorkshopWithSessionsAsync(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            workshopService.CreateWorkshopOrderAsync(
                workshopId,
                new CreateWorkshopOrderRequest
                {
                    Quantity = 1,
                    FullName = "Empty Picker",
                    Phone = "+919876543210",
                    PassTypeId = soloPassId,
                    SelectedSessionIds = new List<Guid>(), // 0 sessions for Solo!
                    IdempotencyKey = Guid.NewGuid().ToString()
                },
                CancellationToken.None));

        Assert.Contains("Please select exactly 1 sessions", ex.Message);
    }

    [Fact]
    public async Task Dual_Requires_Exactly_Two_Sessions_And_Trio_Requires_Three()
    {
        var (workshopService, db, _) = CreateService();
        var (_, _, workshopId, s1Id, s2Id, s3Id, _) = await SeedWorkshopWithSessionsAsync(db);

        var dualPassId = Guid.NewGuid();
        var trioPassId = Guid.NewGuid();

        var dualPass = new WorkshopPassType
        {
            Id = dualPassId,
            WorkshopId = workshopId,
            Name = "Dual Pass",
            Price = 1000m,
            SessionsIncluded = 2,
            WorkshopSessionId = null,
            TotalQuantity = 30,
            IsActive = true
        };
        var trioPass = new WorkshopPassType
        {
            Id = trioPassId,
            WorkshopId = workshopId,
            Name = "Trio Pass",
            Price = 1400m,
            SessionsIncluded = 3,
            WorkshopSessionId = null,
            TotalQuantity = 20,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(dualPass);
        db.WorkshopPassTypes.Add(trioPass);
        await db.SaveChangesAsync();

        // Dual with 1 session -> Fails
        var exDual = await Assert.ThrowsAsync<ArgumentException>(() =>
            workshopService.CreateWorkshopOrderAsync(
                workshopId,
                new CreateWorkshopOrderRequest
                {
                    Quantity = 1,
                    FullName = "Dual Buyer",
                    Phone = "+919876543210",
                    PassTypeId = dualPassId,
                    SelectedSessionIds = new List<Guid> { s1Id },
                    IdempotencyKey = Guid.NewGuid().ToString()
                },
                CancellationToken.None));
        Assert.Contains("Please select exactly 2 sessions", exDual.Message);

        // Dual with 2 sessions -> Succeeds
        var dualOrder = await workshopService.CreateWorkshopOrderAsync(
            workshopId,
            new CreateWorkshopOrderRequest
            {
                Quantity = 1,
                FullName = "Dual Buyer",
                Phone = "+919876543210",
                PassTypeId = dualPassId,
                SelectedSessionIds = new List<Guid> { s1Id, s2Id },
                IdempotencyKey = Guid.NewGuid().ToString()
            },
            CancellationToken.None);
        Assert.NotNull(dualOrder);

        // Trio with 2 sessions -> Fails
        var exTrio = await Assert.ThrowsAsync<ArgumentException>(() =>
            workshopService.CreateWorkshopOrderAsync(
                workshopId,
                new CreateWorkshopOrderRequest
                {
                    Quantity = 1,
                    FullName = "Trio Buyer",
                    Phone = "+919876543210",
                    PassTypeId = trioPassId,
                    SelectedSessionIds = new List<Guid> { s1Id, s2Id },
                    IdempotencyKey = Guid.NewGuid().ToString()
                },
                CancellationToken.None));
        Assert.Contains("Please select exactly 3 sessions", exTrio.Message);

        // Trio with 3 sessions -> Succeeds
        var trioOrder = await workshopService.CreateWorkshopOrderAsync(
            workshopId,
            new CreateWorkshopOrderRequest
            {
                Quantity = 1,
                FullName = "Trio Buyer",
                Phone = "+919876543210",
                PassTypeId = trioPassId,
                SelectedSessionIds = new List<Guid> { s1Id, s2Id, s3Id },
                IdempotencyKey = Guid.NewGuid().ToString()
            },
            CancellationToken.None);
        Assert.NotNull(trioOrder);
    }

    [Fact]
    public async Task All_Access_Pass_Includes_All_Sessions_Without_Selection()
    {
        var (workshopService, db, _) = CreateService();
        var (_, _, workshopId, s1Id, s2Id, s3Id, _) = await SeedWorkshopWithSessionsAsync(db);

        var allAccessPassId = Guid.NewGuid();
        var allAccessPass = new WorkshopPassType
        {
            Id = allAccessPassId,
            WorkshopId = workshopId,
            Name = "All Workshops Pass",
            Price = 1800m,
            SessionsIncluded = null, // All sessions!
            WorkshopSessionId = null,
            TotalQuantity = 20,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(allAccessPass);
        await db.SaveChangesAsync();

        // Customer passes empty selectedSessionIds; all 3 sessions are automatically booked
        var order = await workshopService.CreateWorkshopOrderAsync(
            workshopId,
            new CreateWorkshopOrderRequest
            {
                Quantity = 1,
                FullName = "All Access VIP",
                Phone = "+919876543210",
                PassTypeId = allAccessPassId,
                SelectedSessionIds = new List<Guid>(),
                IdempotencyKey = Guid.NewGuid().ToString()
            },
            CancellationToken.None);

        Assert.NotNull(order);
        var booking = await db.WorkshopBookings
            .Include(b => b.BookingSessions)
            .FirstOrDefaultAsync(b => b.Id == order.BookingId);

        Assert.NotNull(booking);
        Assert.Equal(3, booking.SessionsIncludedCount);
        Assert.Equal(3, booking.BookingSessions.Count);
        Assert.Contains(booking.BookingSessions, bs => bs.WorkshopSessionId == s1Id);
        Assert.Contains(booking.BookingSessions, bs => bs.WorkshopSessionId == s2Id);
        Assert.Contains(booking.BookingSessions, bs => bs.WorkshopSessionId == s3Id);
    }

    [Fact]
    public async Task Solo_Pass_Quantity_Multiplier_Allocates_Seats_Correctly()
    {
        // 3 x Solo passes for Session 2 should create 3 booking session entries for Session 2
        var (workshopService, db, _) = CreateService();
        var (_, _, workshopId, s1Id, s2Id, _, _) = await SeedWorkshopWithSessionsAsync(db);

        var soloPassId = Guid.NewGuid();
        var soloPass = new WorkshopPassType
        {
            Id = soloPassId,
            WorkshopId = workshopId,
            Name = "Solo Pass",
            Price = 500m,
            SessionsIncluded = 1,
            WorkshopSessionId = null,
            TotalQuantity = 50,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(soloPass);
        await db.SaveChangesAsync();

        var order = await workshopService.CreateWorkshopOrderAsync(
            workshopId,
            new CreateWorkshopOrderRequest
            {
                Quantity = 3,
                FullName = "Rohan Group Leader",
                Phone = "+919876543210",
                PassTypeId = soloPassId,
                SelectedSessionIds = new List<Guid> { s2Id },
                IdempotencyKey = Guid.NewGuid().ToString()
            },
            CancellationToken.None);

        Assert.NotNull(order);
        Assert.Equal(3, order.Quantity);
        Assert.Equal(1500m, order.Amount); // 3 * 500

        var booking = await db.WorkshopBookings
            .Include(b => b.BookingSessions)
            .FirstOrDefaultAsync(b => b.Id == order.BookingId);

        Assert.NotNull(booking);
        Assert.Equal(3, booking.Quantity);
        Assert.Equal(3, booking.BookingSessions.Count);
        Assert.All(booking.BookingSessions, bs => Assert.Equal(s2Id, bs.WorkshopSessionId));
    }

    [Fact]
    public async Task Multi_Session_Trio_Quantity_Multiplier_Allocates_Seats_Across_Sessions()
    {
        // 2 x Trio passes covering 3 sessions each = 6 total seats booked (2 for each session)
        var (workshopService, db, _) = CreateService();
        var (_, _, workshopId, s1Id, s2Id, s3Id, _) = await SeedWorkshopWithSessionsAsync(db);

        var trioPassId = Guid.NewGuid();
        var trioPass = new WorkshopPassType
        {
            Id = trioPassId,
            WorkshopId = workshopId,
            Name = "Trio Pass",
            Price = 1200m,
            SessionsIncluded = 3,
            WorkshopSessionId = null,
            TotalQuantity = 20,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(trioPass);
        await db.SaveChangesAsync();

        var order = await workshopService.CreateWorkshopOrderAsync(
            workshopId,
            new CreateWorkshopOrderRequest
            {
                Quantity = 2,
                FullName = "Dance Duo",
                Phone = "+919876543210",
                PassTypeId = trioPassId,
                SelectedSessionIds = new List<Guid> { s1Id, s2Id, s3Id },
                IdempotencyKey = Guid.NewGuid().ToString()
            },
            CancellationToken.None);

        Assert.NotNull(order);
        Assert.Equal(2, order.Quantity);
        Assert.Equal(2400m, order.Amount); // 2 * 1200

        var booking = await db.WorkshopBookings
            .Include(b => b.BookingSessions)
            .FirstOrDefaultAsync(b => b.Id == order.BookingId);

        Assert.NotNull(booking);
        Assert.Equal(2, booking.Quantity);
        Assert.Equal(6, booking.BookingSessions.Count); // 2 * 3 = 6
        Assert.Equal(2, booking.BookingSessions.Count(bs => bs.WorkshopSessionId == s1Id));
        Assert.Equal(2, booking.BookingSessions.Count(bs => bs.WorkshopSessionId == s2Id));
        Assert.Equal(2, booking.BookingSessions.Count(bs => bs.WorkshopSessionId == s3Id));
    }

    [Fact]
    public async Task Solo_Pass_Quantity_Exceeding_Session_Capacity_Throws_InvalidOperationException()
    {
        var (workshopService, db, _) = CreateService();
        var (_, _, workshopId, s1Id, _, _, _) = await SeedWorkshopWithSessionsAsync(db);

        // Session 1 only has 2 seats left
        var session1 = await db.WorkshopSessions.FindAsync(s1Id);
        session1!.Capacity = 2;
        await db.SaveChangesAsync();

        var soloPassId = Guid.NewGuid();
        var soloPass = new WorkshopPassType
        {
            Id = soloPassId,
            WorkshopId = workshopId,
            Name = "Solo Pass",
            Price = 500m,
            SessionsIncluded = 1,
            WorkshopSessionId = null,
            TotalQuantity = 50,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(soloPass);
        await db.SaveChangesAsync();

        // Customer attempts to purchase 3 tickets for Session 1 (which only has 2 seats)
        var quoteEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workshopService.GetWorkshopQuoteAsync(
                workshopId,
                quantity: 3,
                passTypeId: soloPassId,
                selectedSessionIds: new List<Guid> { s1Id }));
        Assert.Contains("has only 2 seats remaining", quoteEx.Message);

        var orderEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workshopService.CreateWorkshopOrderAsync(
                workshopId,
                new CreateWorkshopOrderRequest
                {
                    Quantity = 3,
                    FullName = "Exceeds Capacity Buyer",
                    Phone = "+919876543210",
                    PassTypeId = soloPassId,
                    SelectedSessionIds = new List<Guid> { s1Id },
                    IdempotencyKey = Guid.NewGuid().ToString()
                }));
        Assert.Contains("has only 2 seats remaining", orderEx.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(50)]
    public async Task Order_And_Quote_Reject_Quantity_Outside_1_To_10_Range(int invalidQuantity)
    {
        var (workshopService, db, _) = CreateService();
        var (_, _, workshopId, s1Id, _, _, _) = await SeedWorkshopWithSessionsAsync(db);

        var soloPassId = Guid.NewGuid();
        var soloPass = new WorkshopPassType
        {
            Id = soloPassId,
            WorkshopId = workshopId,
            Name = "Solo Pass",
            Price = 500m,
            SessionsIncluded = 1,
            WorkshopSessionId = null,
            TotalQuantity = 50,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(soloPass);
        await db.SaveChangesAsync();

        var quoteEx = await Assert.ThrowsAsync<ArgumentException>(() =>
            workshopService.GetWorkshopQuoteAsync(
                workshopId,
                quantity: invalidQuantity,
                passTypeId: soloPassId,
                selectedSessionIds: new List<Guid> { s1Id }));
        Assert.Contains("Ticket quantity must be between 1 and 10", quoteEx.Message);

        var orderEx = await Assert.ThrowsAsync<ArgumentException>(() =>
            workshopService.CreateWorkshopOrderAsync(
                workshopId,
                new CreateWorkshopOrderRequest
                {
                    Quantity = invalidQuantity,
                    FullName = "Invalid Q Buyer",
                    Phone = "+919876543210",
                    PassTypeId = soloPassId,
                    SelectedSessionIds = new List<Guid> { s1Id },
                    IdempotencyKey = Guid.NewGuid().ToString()
                }));
        Assert.Contains("Ticket quantity must be between 1 and 10", orderEx.Message);
    }

    [Fact]
    public async Task Solo_Pass_With_Session_1_Succeeds()
    {
        var (workshopService, db, _) = CreateService();
        var (_, _, workshopId, s1Id, _, _, soloPassId) = await SeedWorkshopWithSessionsAsync(db);

        var order = await workshopService.CreateWorkshopOrderAsync(
            workshopId,
            new CreateWorkshopOrderRequest
            {
                Quantity = 1,
                FullName = "Session 1 Dancer",
                Phone = "+919876543210",
                PassTypeId = soloPassId,
                SelectedSessionIds = new List<Guid> { s1Id },
                IdempotencyKey = Guid.NewGuid().ToString()
            },
            CancellationToken.None);

        Assert.NotNull(order);
        var booking = await db.WorkshopBookings
            .Include(b => b.BookingSessions)
            .FirstOrDefaultAsync(b => b.Id == order.BookingId);

        Assert.NotNull(booking);
        Assert.Single(booking.BookingSessions);
        Assert.Equal(s1Id, booking.BookingSessions.First().WorkshopSessionId);
    }

    [Fact]
    public async Task Solo_Pass_With_Session_2_Succeeds()
    {
        var (workshopService, db, _) = CreateService();
        var (_, _, workshopId, _, s2Id, _, soloPassId) = await SeedWorkshopWithSessionsAsync(db);

        var order = await workshopService.CreateWorkshopOrderAsync(
            workshopId,
            new CreateWorkshopOrderRequest
            {
                Quantity = 1,
                FullName = "Session 2 Dancer",
                Phone = "+919876543210",
                PassTypeId = soloPassId,
                SelectedSessionIds = new List<Guid> { s2Id },
                IdempotencyKey = Guid.NewGuid().ToString()
            },
            CancellationToken.None);

        Assert.NotNull(order);
        var booking = await db.WorkshopBookings
            .Include(b => b.BookingSessions)
            .FirstOrDefaultAsync(b => b.Id == order.BookingId);

        Assert.NotNull(booking);
        Assert.Single(booking.BookingSessions);
        Assert.Equal(s2Id, booking.BookingSessions.First().WorkshopSessionId);
    }

    [Fact]
    public async Task Solo_Pass_With_Legacy_Populated_WorkshopSessionId_Allows_Customer_To_Select_Any_Session()
    {
        // Even if pass has WorkshopSessionId = s2Id in DB, if SessionsIncluded = 1, customer can select Session 1
        var (workshopService, db, _) = CreateService();
        var (_, _, workshopId, s1Id, s2Id, _, _) = await SeedWorkshopWithSessionsAsync(db);

        var legacySoloPassId = Guid.NewGuid();
        var legacySoloPass = new WorkshopPassType
        {
            Id = legacySoloPassId,
            WorkshopId = workshopId,
            Name = "General Admission",
            Price = 500m,
            SessionsIncluded = 1,
            WorkshopSessionId = s2Id, // Legacy DB value pointing to Session 2!
            TotalQuantity = 50,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(legacySoloPass);
        await db.SaveChangesAsync();

        // Customer selects Session 1 -> Must succeed! Not rejected with "Invalid session selection"
        var order = await workshopService.CreateWorkshopOrderAsync(
            workshopId,
            new CreateWorkshopOrderRequest
            {
                Quantity = 1,
                FullName = "Customer Choosing Session 1",
                Phone = "+919876543210",
                PassTypeId = legacySoloPassId,
                SelectedSessionIds = new List<Guid> { s1Id }, // Selecting Session 1!
                IdempotencyKey = Guid.NewGuid().ToString()
            },
            CancellationToken.None);

        Assert.NotNull(order);
        var booking = await db.WorkshopBookings
            .Include(b => b.BookingSessions)
            .FirstOrDefaultAsync(b => b.Id == order.BookingId);

        Assert.NotNull(booking);
        Assert.Single(booking.BookingSessions);
        Assert.Equal(s1Id, booking.BookingSessions.First().WorkshopSessionId);
    }
}
