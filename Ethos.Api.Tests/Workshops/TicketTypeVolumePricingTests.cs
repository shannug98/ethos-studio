using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Common;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class TicketTypeVolumePricingTests
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

    private class MockCurrentUserService : ICurrentUserService
    {
        public Guid UserId => Guid.NewGuid();
        public bool IsAuthenticated => false;
        public string? Phone => null;
        public string? Name => null;
        public IReadOnlyList<string> Roles => Array.Empty<string>();
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

    private class MockTicketService : IWorkshopTicketService
    {
        public string DeriveQrToken(WorkshopTicket ticket) => "QR-TOKEN-123";
        public string ComputeTokenHash(string rawToken) => "HASH-123";
        public Task<List<WorkshopTicketResponse>> IssueTicketsForBookingAsync(WorkshopBooking booking, PaymentTransaction transaction, User? user, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<WorkshopTicketResponse>());
        }
        public Task<IReadOnlyList<WorkshopTicketResponse>> GetTicketsForBookingAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorkshopTicketResponse>>(new List<WorkshopTicketResponse>());
        public Task<WorkshopTicketResponse> GetTicketPassAsync(Guid ticketId, Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WorkshopTicketResponse> UpdateAttendeeDetailsAsync(Guid bookingId, Guid ticketId, Guid userId, UpdateAttendeeDetailsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ResendTicketPassAsync(Guid bookingId, Guid ticketId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public string GeneratePdfDownloadToken(Guid ticketId, TimeSpan? validity = null) => "TOKEN-123";
        public bool ValidatePdfDownloadToken(Guid ticketId, string? token) => token == "TOKEN-123";
    }

    private class DummyStudentEligibilityService : IStudentEligibilityService
    {
        public Task<StudentEligibilityResult> CheckEligibilityByPhoneAsync(string phone, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StudentEligibilityResult(false, false, false));
        public Task<bool> IsStudentPortalEligibleAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
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
        db.Roles.Add(new Role { Id = Guid.NewGuid(), Code = "ADMIN", Name = "Admin" });
        db.SaveChanges();
        return db;
    }

    private WorkshopService CreateWorkshopService(AppDbContext dbContext, IWorkshopPricingService pricingService)
    {
        var razorpaySettings = Options.Create(new RazorpaySettings
        {
            KeyId = "rzp_test_placeholder",
            KeySecret = "test_secret",
            WebhookSecret = "test_webhook_secret"
        });

        var ticketService = new MockTicketService();
        var notificationService = new MockNotificationService();
        var fulfillmentService = new Ethos.Api.Application.Payments.PaymentFulfillmentService(
            dbContext,
            ticketService,
            razorpaySettings,
            notificationService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Ethos.Api.Application.Payments.PaymentFulfillmentService>.Instance);

        return new WorkshopService(
            dbContext,
            new MockCurrentUserService(),
            pricingService,
            notificationService,
            ticketService,
            fulfillmentService,
            razorpaySettings,
            new TestWebHostEnvironment());
    }

    [Fact]
    public void ValidateTicketTypePricingTiers_ValidContiguousTiers_Succeeds()
    {
        var pricingService = new WorkshopPricingService(CreateDbContext(), new DummyStudentEligibilityService());

        var validTiers = new List<AdminWorkshopPricingTierItem>
        {
            new() { TierNumber = 1, TierName = "Early Bird", MinTickets = 1, MaxTickets = 10, Price = 499m },
            new() { TierNumber = 2, TierName = "Standard", MinTickets = 11, MaxTickets = 25, Price = 599m },
            new() { TierNumber = 3, TierName = "Peak", MinTickets = 26, MaxTickets = null, Price = 699m }
        };

        pricingService.ValidateTicketTypePricingTiers(50, validTiers);
    }

    [Fact]
    public void ValidateTicketTypePricingTiers_FirstTierNotStartingAtOne_Throws()
    {
        var pricingService = new WorkshopPricingService(CreateDbContext(), new DummyStudentEligibilityService());

        var invalidTiers = new List<AdminWorkshopPricingTierItem>
        {
            new() { TierNumber = 1, TierName = "Early", MinTickets = 5, MaxTickets = 15, Price = 499m }
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            pricingService.ValidateTicketTypePricingTiers(30, invalidTiers));
        Assert.Contains("must start at ticket 1", ex.Message);
    }

    [Fact]
    public void ValidateTicketTypePricingTiers_GapBetweenTiers_Throws()
    {
        var pricingService = new WorkshopPricingService(CreateDbContext(), new DummyStudentEligibilityService());

        var gapTiers = new List<AdminWorkshopPricingTierItem>
        {
            new() { TierNumber = 1, TierName = "Tier 1", MinTickets = 1, MaxTickets = 10, Price = 499m },
            new() { TierNumber = 2, TierName = "Tier 2", MinTickets = 15, MaxTickets = 25, Price = 599m }
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            pricingService.ValidateTicketTypePricingTiers(30, gapTiers));
        Assert.Contains("gap or overlap detected", ex.Message);
    }

    [Fact]
    public void ValidateTicketTypePricingTiers_IncompleteCapacityCoverage_Throws()
    {
        var pricingService = new WorkshopPricingService(CreateDbContext(), new DummyStudentEligibilityService());

        var incompleteTiers = new List<AdminWorkshopPricingTierItem>
        {
            new() { TierNumber = 1, TierName = "Tier 1", MinTickets = 1, MaxTickets = 10, Price = 499m },
            new() { TierNumber = 2, TierName = "Tier 2", MinTickets = 11, MaxTickets = 20, Price = 599m }
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            pricingService.ValidateTicketTypePricingTiers(50, incompleteTiers));
        Assert.Contains("do not cover total ticket capacity", ex.Message);
    }

    [Fact]
    public async Task CalculateTicketTypeQuoteAsync_OptionA_PerTicketProgressiveSplitTier_CalculatesCorrectly()
    {
        using var db = CreateDbContext();
        var pricingService = new WorkshopPricingService(db, new DummyStudentEligibilityService());

        var passType = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = Guid.NewGuid(),
            Name = "Early Bird Pass",
            TotalQuantity = 50,
            Price = 500m,
            PricingTiers = new List<WorkshopPricingTier>
            {
                new() { TierNumber = 1, TierName = "Tier 1 (1-10)", MinTickets = 1, MaxTickets = 10, Price = 500m },
                new() { TierNumber = 2, TierName = "Tier 2 (11-20)", MinTickets = 11, MaxTickets = 20, Price = 600m },
                new() { TierNumber = 3, TierName = "Tier 3 (21+)", MinTickets = 21, MaxTickets = null, Price = 700m }
            }
        };

        // Currently 9 tickets sold so far. Customer buys 3 tickets:
        // Ticket 10 -> Tier 1 (500)
        // Ticket 11 -> Tier 2 (600)
        // Ticket 12 -> Tier 2 (600)
        // Expected total: 500 + 600 + 600 = 1700
        var quote = await pricingService.CalculateTicketTypeQuoteAsync(passType, 3, 9);

        Assert.Equal(1700m, quote.TotalAmount);
        Assert.True(quote.IsSplitTier);
        Assert.Equal(2, quote.Breakdown.Count);

        var tier1Item = quote.Breakdown.First(b => b.TierNumber == 1);
        Assert.Equal(1, tier1Item.Quantity);
        Assert.Equal(500m, tier1Item.UnitPrice);
        Assert.Equal(500m, tier1Item.Subtotal);

        var tier2Item = quote.Breakdown.First(b => b.TierNumber == 2);
        Assert.Equal(2, tier2Item.Quantity);
        Assert.Equal(600m, tier2Item.UnitPrice);
        Assert.Equal(1200m, tier2Item.Subtotal);
    }

    [Fact]
    public async Task CalculateTicketTypeQuoteAsync_FlatPrice_WhenNoTiersConfigured()
    {
        using var db = CreateDbContext();
        var pricingService = new WorkshopPricingService(db, new DummyStudentEligibilityService());

        var passType = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = Guid.NewGuid(),
            Name = "General Admission",
            Price = 650m,
            TotalQuantity = 100
        };

        var quote = await pricingService.CalculateTicketTypeQuoteAsync(passType, 4, 15);

        Assert.Equal(2600m, quote.TotalAmount); // 4 * 650
        Assert.False(quote.IsSplitTier);
        Assert.Single(quote.Breakdown);
        Assert.Equal(4, quote.Breakdown[0].Quantity);
        Assert.Equal(650m, quote.Breakdown[0].UnitPrice);
    }

    [Fact]
    public async Task CreateWorkshopOrderAsync_AuthoritativeSalesCount_UsesSumOfQuantity()
    {
        using var db = CreateDbContext();
        var pricingService = new WorkshopPricingService(db, new DummyStudentEligibilityService());
        var workshopService = CreateWorkshopService(db, pricingService);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Master Workshop",
            Price = 500m,
            Capacity = 50,
            Status = WorkshopStatus.Published,
            PublicVisibility = true,
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12)
        };
        db.Workshops.Add(workshop);

        var ticketType = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "VIP Pass",
            SessionsIncluded = 1,
            TotalQuantity = 10,
            Price = 800m,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(ticketType);

        var trainerRole = db.Roles.First(r => r.Code == "TRAINER");
        var trainerUser = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "TRN-001",
            FullName = "Trainer One",
            Phone = "+919876543210",
            Email = "trainer@ethos.test",
            IsActive = true
        };
        db.Users.Add(trainerUser);
        db.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = trainerUser.Id,
            RoleId = trainerRole.Id,
            AssignedAt = DateTime.UtcNow
        });

        var trainerProfile = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            UserId = trainerUser.Id,
            TrainerCode = "TR01",
            FullName = "Trainer One",
            Status = TrainerStatus.Active
        };
        db.TrainerProfiles.Add(trainerProfile);

        workshop.TrainerProfileId = trainerProfile.Id;

        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            TrainerProfileId = trainerProfile.Id,
            Title = "Session 1",
            Capacity = 30,
            SessionDate = workshop.WorkshopDate,
            StartTime = workshop.StartTime,
            EndTime = workshop.EndTime,
            IsActive = true
        };
        db.WorkshopSessions.Add(session);

        // Seed 2 existing bookings with Quantity 3 and Quantity 4 (Total = 7 tickets sold)
        var studentRole = db.Roles.First(r => r.Code == "STUDENT");
        var studentUser = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "STU-001",
            FullName = "Student One",
            Phone = "+919999999999",
            Email = "student1@ethos.test",
            IsActive = true
        };
        db.Users.Add(studentUser);
        db.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = studentUser.Id,
            RoleId = studentRole.Id,
            AssignedAt = DateTime.UtcNow
        });

        var studentProfile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = studentUser.Id
        };
        db.StudentProfiles.Add(studentProfile);

        db.WorkshopBookings.Add(new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = studentProfile.Id,
            WorkshopPassTypeId = ticketType.Id,
            Quantity = 3,
            TotalPrice = 2400m,
            Status = WorkshopBookingStatus.Confirmed
        });

        db.WorkshopBookings.Add(new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = studentProfile.Id,
            WorkshopPassTypeId = ticketType.Id,
            Quantity = 4,
            TotalPrice = 3200m,
            Status = WorkshopBookingStatus.Confirmed
        });

        await db.SaveChangesAsync();

        // Attempt to book 4 more tickets (7 + 4 = 11 > 10 capacity)
        // With SUM(Quantity), 7 tickets are detected sold. Booking 4 tickets must be rejected.
        var request = new CreateWorkshopOrderRequest
        {
            PassTypeId = ticketType.Id,
            Quantity = 4,
            FullName = "Guest User",
            Phone = "+918888888888",
            Email = "guest@ethos.test",
            SelectedSessionIds = new List<Guid> { session.Id }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workshopService.CreateWorkshopOrderAsync(workshop.Id, request));

        Assert.Contains("only 3 tickets remaining", ex.Message);
    }

    [Fact]
    public async Task PendingPayment_TemporarilyClaimsPricingSlots_AndReleasesOnExpiry()
    {
        using var db = CreateDbContext();
        var pricingService = new WorkshopPricingService(db, new DummyStudentEligibilityService());

        var ticketType = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = Guid.NewGuid(),
            Name = "Dynamic Tier Pass",
            TotalQuantity = 50,
            Price = 500m,
            PricingTiers = new List<WorkshopPricingTier>
            {
                new() { TierNumber = 1, TierName = "Tier 1", MinTickets = 1, MaxTickets = 10, Price = 500m },
                new() { TierNumber = 2, TierName = "Tier 2", MinTickets = 11, MaxTickets = 20, Price = 600m }
            }
        };
        db.WorkshopPassTypes.Add(ticketType);

        var now = DateTime.UtcNow;

        // 9 tickets confirmed sold
        db.WorkshopBookings.Add(new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = ticketType.WorkshopId,
            WorkshopPassTypeId = ticketType.Id,
            Quantity = 9,
            Status = WorkshopBookingStatus.Confirmed
        });

        // 1 active pending payment reservation for 2 tickets (expires in 15 mins)
        var pendingBooking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = ticketType.WorkshopId,
            WorkshopPassTypeId = ticketType.Id,
            Quantity = 2,
            Status = WorkshopBookingStatus.PendingPayment,
            ReservationExpiresAt = now.AddMinutes(15)
        };
        db.WorkshopBookings.Add(pendingBooking);

        await db.SaveChangesAsync();

        // While pending reservation is active, active tickets sold = 9 + 2 = 11
        var activeSold = await db.WorkshopBookings
            .Where(b => b.WorkshopPassTypeId == ticketType.Id &&
                        (b.Status == WorkshopBookingStatus.Confirmed ||
                         b.Status == WorkshopBookingStatus.Attended ||
                         (b.Status == WorkshopBookingStatus.PendingPayment &&
                          b.ReservationExpiresAt > now)))
            .SumAsync(b => (int?)b.Quantity) ?? 0;

        Assert.Equal(11, activeSold);

        // Next shopper gets Tier 2 (since slots 1-11 are occupied)
        var quoteDuringPending = await pricingService.CalculateTicketTypeQuoteAsync(ticketType, 1, activeSold);
        Assert.Equal(600m, quoteDuringPending.TotalAmount);
        Assert.Equal(2, quoteDuringPending.Breakdown[0].TierNumber);

        // Now simulate cart abandonment: 20 minutes pass, pending reservation expires
        var futureTime = now.AddMinutes(20);

        var activeSoldAfterExpiry = await db.WorkshopBookings
            .Where(b => b.WorkshopPassTypeId == ticketType.Id &&
                        (b.Status == WorkshopBookingStatus.Confirmed ||
                         b.Status == WorkshopBookingStatus.Attended ||
                         (b.Status == WorkshopBookingStatus.PendingPayment &&
                          b.ReservationExpiresAt > futureTime)))
            .SumAsync(b => (int?)b.Quantity) ?? 0;

        // Expired reservation is released, active count returns to 9
        Assert.Equal(9, activeSoldAfterExpiry);

        // Subsequent shopper re-claims Tier 1!
        var quoteAfterExpiry = await pricingService.CalculateTicketTypeQuoteAsync(ticketType, 1, activeSoldAfterExpiry);
        Assert.Equal(500m, quoteAfterExpiry.TotalAmount);
        Assert.Equal(1, quoteAfterExpiry.Breakdown[0].TierNumber);
    }

    [Fact]
    public void IsSalesOpen_EnforcesSalesStartAndEndWindows()
    {
        var now = DateTime.UtcNow;

        var futureTicket = new WorkshopPassType
        {
            SalesStartUtc = now.AddDays(1),
            SalesEndUtc = now.AddDays(5)
        };
        Assert.False(futureTicket.IsSalesOpen(now));
        Assert.True(futureTicket.IsSalesClosed(now));

        var activeTicket = new WorkshopPassType
        {
            SalesStartUtc = now.AddDays(-1),
            SalesEndUtc = now.AddDays(3)
        };
        Assert.True(activeTicket.IsSalesOpen(now));
        Assert.False(activeTicket.IsSalesClosed(now));

        var expiredTicket = new WorkshopPassType
        {
            SalesStartUtc = now.AddDays(-5),
            SalesEndUtc = now.AddDays(-1)
        };
        Assert.False(expiredTicket.IsSalesOpen(now));
        Assert.True(expiredTicket.IsSalesClosed(now));
    }

    private PaymentFulfillmentService CreateFulfillmentService(AppDbContext dbContext)
    {
        var razorpaySettings = Options.Create(new RazorpaySettings
        {
            KeyId = "rzp_test_placeholder",
            KeySecret = "test_secret",
            WebhookSecret = "test_webhook_secret"
        });

        return new PaymentFulfillmentService(
            dbContext,
            new MockTicketService(),
            razorpaySettings,
            new MockNotificationService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentFulfillmentService>.Instance);
    }

    [Fact]
    public async Task PublicWorkshop_MultipleLoads_PreservesTicketTypePricingTiers_NeverWipesWithDefaultTiers()
    {
        using var db = CreateDbContext();
        var pricingService = new WorkshopPricingService(db, new DummyStudentEligibilityService());
        var workshopService = CreateWorkshopService(db, pricingService);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Ticket Type Preservation Workshop",
            Status = WorkshopStatus.Published,
            PublicVisibility = true,
            Capacity = 100,
            Price = 500m,
            WorkshopDate = DateTime.UtcNow.AddDays(10),
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12)
        };
        db.Workshops.Add(workshop);

        var pass1 = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "Early Bird",
            Price = 400m,
            SessionsIncluded = 1,
            TotalQuantity = 50,
            DisplayOrder = 0,
            IsActive = true
        };
        var pass2 = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "Regular",
            Price = 600m,
            SessionsIncluded = 1,
            TotalQuantity = 50,
            DisplayOrder = 1,
            IsActive = true
        };
        db.WorkshopPassTypes.AddRange(pass1, pass2);

        var tier1 = new WorkshopPricingTier
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            WorkshopPassTypeId = pass1.Id,
            TierNumber = 1,
            TierName = "EB Tier 1",
            MinTickets = 1,
            MaxTickets = 25,
            Price = 400m
        };
        var tier2 = new WorkshopPricingTier
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            WorkshopPassTypeId = pass1.Id,
            TierNumber = 2,
            TierName = "EB Tier 2",
            MinTickets = 26,
            MaxTickets = 50,
            Price = 450m
        };
        var tier3 = new WorkshopPricingTier
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            WorkshopPassTypeId = pass2.Id,
            TierNumber = 1,
            TierName = "Reg Tier 1",
            MinTickets = 1,
            MaxTickets = 25,
            Price = 600m
        };
        var tier4 = new WorkshopPricingTier
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            WorkshopPassTypeId = pass2.Id,
            TierNumber = 2,
            TierName = "Reg Tier 2",
            MinTickets = 26,
            MaxTickets = 50,
            Price = 650m
        };
        var tier5 = new WorkshopPricingTier
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            WorkshopPassTypeId = pass2.Id,
            TierNumber = 3,
            TierName = "Reg Tier 3",
            MinTickets = 51,
            MaxTickets = 75,
            Price = 700m
        };
        db.WorkshopPricingTiers.AddRange(tier1, tier2, tier3, tier4, tier5);
        await db.SaveChangesAsync();

        var initialTiersCount = await db.WorkshopPricingTiers.CountAsync(t => t.WorkshopId == workshop.Id);
        Assert.Equal(5, initialTiersCount);

        // Load public workshop multiple times
        for (int i = 0; i < 5; i++)
        {
            var publicDetails = await workshopService.GetWorkshopByIdAsync(workshop.Id);
            Assert.NotNull(publicDetails);
            Assert.Equal(2, publicDetails.PassTypes.Count);
        }

        // Calculate pricing directly
        var pricing = await pricingService.CalculatePricingAsync(workshop);
        Assert.NotNull(pricing);

        // Crucial verification: Tiers count must STILL be 5, not wiped or replaced by 4 default tiers
        var finalTiers = await db.WorkshopPricingTiers.Where(t => t.WorkshopId == workshop.Id).ToListAsync();
        Assert.Equal(5, finalTiers.Count);
        Assert.All(finalTiers, t => Assert.NotNull(t.WorkshopPassTypeId));
        Assert.DoesNotContain(finalTiers, t => t.WorkshopPassTypeId == null);
    }

    [Fact]
    public async Task MultiSeatBooking_PaymentFulfillment_UsesCountAsync_DoesNotOversellOrFalseRefund()
    {
        using var db = CreateDbContext();
        var fulfillmentService = CreateFulfillmentService(db);

        var trainerRole = await db.Roles.FirstAsync(r => r.Code == "TRAINER");
        var studentRole = await db.Roles.FirstAsync(r => r.Code == "STUDENT");

        var trainerUser = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "TR-" + Guid.NewGuid().ToString()[..6],
            FullName = "Test Trainer",
            Phone = "9999900000",
            Email = "trainer@ethos.test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        trainerUser.UserRoles.Add(new UserRole { UserId = trainerUser.Id, RoleId = trainerRole.Id, Role = trainerRole });
        db.Users.Add(trainerUser);

        var trainer = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            UserId = trainerUser.Id,
            User = trainerUser,
            TrainerCode = "TR09",
            FullName = "Test Trainer",
            Status = TrainerStatus.Active
        };
        db.TrainerProfiles.Add(trainer);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            TrainerProfileId = trainer.Id,
            TrainerProfile = trainer,
            Title = "Capacity Session Test Workshop",
            Status = WorkshopStatus.Published,
            PublicVisibility = true,
            Capacity = 10,
            Price = 500m,
            WorkshopDate = DateTime.UtcNow.AddDays(5),
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12)
        };
        db.Workshops.Add(workshop);

        var passType = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Name = "Standard Pass",
            Price = 500m,
            SessionsIncluded = 1,
            TotalQuantity = 10,
            IsActive = true
        };
        db.WorkshopPassTypes.Add(passType);

        // Single session with exact capacity 4
        var session = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Workshop = workshop,
            TrainerProfileId = trainer.Id,
            TrainerProfile = trainer,
            Title = "Session A",
            Capacity = 4,
            SessionDate = DateTime.UtcNow.AddDays(5),
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12)
        };
        db.WorkshopSessions.Add(session);

        // Existing confirmed booking: Quantity = 2
        // System creates 2 WorkshopBookingSession rows for this booking (1 per ticket seat)
        var existingBooking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Workshop = workshop,
            WorkshopPassTypeId = passType.Id,
            WorkshopPassType = passType,
            Quantity = 2,
            Status = WorkshopBookingStatus.Confirmed,
            GuestName = "Shopper 1",
            GuestEmail = "shopper1@ethos.test",
            GuestPhone = "9999900001"
        };
        db.WorkshopBookings.Add(existingBooking);

        var existingBs1 = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = existingBooking.Id,
            WorkshopBooking = existingBooking,
            WorkshopSessionId = session.Id,
            WorkshopSession = session,
            Status = WorkshopBookingSessionStatus.Booked
        };
        var existingBs2 = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = existingBooking.Id,
            WorkshopBooking = existingBooking,
            WorkshopSessionId = session.Id,
            WorkshopSession = session,
            Status = WorkshopBookingSessionStatus.Booked
        };
        db.WorkshopBookingSessions.AddRange(existingBs1, existingBs2);

        var shopperUser = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "CUST-" + Guid.NewGuid().ToString()[..6],
            FullName = "Shopper 2",
            Phone = "9999900002",
            Email = "shopper2@ethos.test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        shopperUser.UserRoles.Add(new UserRole { UserId = shopperUser.Id, RoleId = studentRole.Id, Role = studentRole });
        db.Users.Add(shopperUser);

        var shopperProfile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = shopperUser.Id,
            User = shopperUser,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.StudentProfiles.Add(shopperProfile);

        // New booking: Quantity = 2 (2 seats requested)
        // Total seats consumed will be 2 (existing) + 2 (new) = 4 <= 4 (session capacity).
        // Under the bug (SumAsync of Quantity), existing seats were summed as 2 + 2 = 4,
        // so 4 + 2 = 6 > 4 -> falsely triggering OversoldRefundRequired!
        // Under the fix (CountAsync), existing seats count = 2, so 2 + 2 = 4 <= 4 -> succeeds!
        var newBooking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Workshop = workshop,
            WorkshopPassTypeId = passType.Id,
            WorkshopPassType = passType,
            StudentProfileId = shopperProfile.Id,
            StudentProfile = shopperProfile,
            Quantity = 2,
            Status = WorkshopBookingStatus.PendingPayment,
            GuestName = "Shopper 2",
            GuestEmail = "shopper2@ethos.test",
            GuestPhone = "9999900002"
        };
        db.WorkshopBookings.Add(newBooking);

        var newBs1 = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = newBooking.Id,
            WorkshopBooking = newBooking,
            WorkshopSessionId = session.Id,
            WorkshopSession = session,
            Status = WorkshopBookingSessionStatus.Booked
        };
        var newBs2 = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = newBooking.Id,
            WorkshopBooking = newBooking,
            WorkshopSessionId = session.Id,
            WorkshopSession = session,
            Status = WorkshopBookingSessionStatus.Booked
        };
        db.WorkshopBookingSessions.AddRange(newBs1, newBs2);

        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = shopperUser.Id,
            User = shopperUser,
            Purpose = PaymentPurpose.WorkshopBooking,
            ReferenceId = newBooking.Id,
            Amount = 1000m,
            Currency = "INR",
            Status = PaymentStatus.OrderCreated,
            RazorpayOrderId = "order_test_123"
        };
        db.PaymentTransactions.Add(transaction);

        await db.SaveChangesAsync();

        // Act: fulfill payment
        var result = await fulfillmentService.FulfillWorkshopPaymentAsync(
            transaction,
            "pay_test_123",
            null,
            "test",
            null,
            CancellationToken.None);

        // Assert:
        Assert.NotNull(result);
        Assert.Equal(WorkshopBookingStatus.Confirmed, newBooking.Status);
        Assert.Equal(PaymentStatus.Paid, transaction.Status);
        Assert.False(transaction.Status == PaymentStatus.Failed);
    }

    [Fact]
    public async Task AdminWorkshopService_SalesStartUtcGreaterThanOrEqualSalesEndUtc_ThrowsArgumentException()
    {
        using var db = CreateDbContext();
        var pricingService = new WorkshopPricingService(db, new DummyStudentEligibilityService());
        var adminService = new AdminWorkshopService(db, new DummyAuditService(), pricingService);

        var adminUserId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var invalidRequest = new AdminCreateWorkshopRequest
        {
            Title = "Invalid Sales Window Workshop",
            DanceStyle = "Hip Hop",
            Level = "All Levels",
            Venue = "Studio A",
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12),
            Capacity = 50,
            Price = 500,
            PassTypes = new List<AdminWorkshopPassTypeItem>
            {
                new AdminWorkshopPassTypeItem
                {
                    Name = "Bad Dates Pass",
                    Price = 500,
                    SessionsIncluded = 1,
                    TotalQuantity = 50,
                    SalesStartUtc = now.AddDays(5),
                    SalesEndUtc = now.AddDays(2), // Start >= End!
                    IsActive = true
                }
            }
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            adminService.CreateWorkshopAsync(adminUserId, invalidRequest, CancellationToken.None));
        Assert.Contains("must be before sales end", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CalculateTicketTypeQuoteAsync_SlotGap_FailsFastWithoutSilentFallback()
    {
        using var db = CreateDbContext();
        var pricingService = new WorkshopPricingService(db, new DummyStudentEligibilityService());

        var passType = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = Guid.NewGuid(),
            Name = "Tier Gap Pass",
            Price = 500m,
            TotalQuantity = 50,
            PricingTiers = new List<WorkshopPricingTier>
            {
                new WorkshopPricingTier
                {
                    TierNumber = 1,
                    MinTickets = 1,
                    MaxTickets = 10,
                    Price = 500m
                }
            }
        };

        // Quote for slot 11, which has NO configured tier (only 1-10 is configured)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pricingService.CalculateTicketTypeQuoteAsync(passType, 1, 10)); // soldSoFar=10, so slot is 11

        Assert.Contains("No matching pricing tier found for ticket slot #11", ex.Message);
    }

    [Fact]
    public async Task CalculateQuoteAsync_Legacy_IncludesActivePendingPaymentReservations()
    {
        using var db = CreateDbContext();
        var pricingService = new WorkshopPricingService(db, new DummyStudentEligibilityService());

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Legacy Workshop",
            Capacity = 100,
            Price = 500m,
            WorkshopDate = DateTime.UtcNow.AddDays(5),
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12)
        };
        db.Workshops.Add(workshop);

        // Add 2 legacy tiers (1-10 @ 500, 11-20 @ 600)
        db.WorkshopPricingTiers.AddRange(
            new WorkshopPricingTier { Id = Guid.NewGuid(), WorkshopId = workshop.Id, WorkshopPassTypeId = null, TierNumber = 1, MinTickets = 1, MaxTickets = 10, Price = 500m },
            new WorkshopPricingTier { Id = Guid.NewGuid(), WorkshopId = workshop.Id, WorkshopPassTypeId = null, TierNumber = 2, MinTickets = 11, MaxTickets = 20, Price = 600m }
        );

        var now = DateTime.UtcNow;
        // 9 confirmed bookings
        db.WorkshopBookings.Add(new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Quantity = 9,
            Status = WorkshopBookingStatus.Confirmed
        });
        // 1 pending booking of 2 tickets expiring in 10 mins
        db.WorkshopBookings.Add(new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Quantity = 2,
            Status = WorkshopBookingStatus.PendingPayment,
            ReservationExpiresAt = now.AddMinutes(10)
        });
        await db.SaveChangesAsync();

        // Next quote should see 9 + 2 = 11 active tickets sold -> next ticket is slot 12 -> Tier 2 (600m)
        var quote = await pricingService.CalculateQuoteAsync(workshop.Id, 1);
        Assert.Equal(600m, quote.TotalAmount);
        Assert.Equal(2, quote.Breakdown[0].TierNumber);
    }
}
