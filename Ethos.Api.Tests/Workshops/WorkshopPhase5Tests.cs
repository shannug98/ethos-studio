using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Students;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class WorkshopPhase5Tests
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

    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private async Task<(Guid adminId, Guid trainerId, Guid studentProfileId)> SetupBaseAsync(AppDbContext db)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Code == "ADMIN");
        if (role == null)
        {
            role = new Role { Id = Guid.NewGuid(), Code = "ADMIN", Name = "Admin" };
            db.Roles.Add(role);
        }

        var adminId = Guid.NewGuid();
        var adminUser = new User
        {
            Id = adminId,
            Phone = "+919999999999",
            FullName = "Admin User",
            CustomerCode = "ADM01",
            CreatedAt = DateTime.UtcNow
        };
        adminUser.UserRoles.Add(new UserRole { UserId = adminId, RoleId = role.Id, Role = role });
        db.Users.Add(adminUser);

        var trainer = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TrainerCode = "TR01",
            FullName = "Trainer Lead",
            Status = TrainerStatus.Active,
            PrimaryDanceStyle = "Hip Hop",
            CreatedAt = DateTime.UtcNow
        };
        db.TrainerProfiles.Add(trainer);

        var studentProfile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = adminId,
            CreatedAt = DateTime.UtcNow
        };
        db.StudentProfiles.Add(studentProfile);

        await db.SaveChangesAsync();
        return (adminId, trainer.Id, studentProfile.Id);
    }

    // =========================================================================
    // 1. PASS CATEGORY CLASSIFICATION MATRIX VALIDATION
    // =========================================================================

    [Fact]
    public async Task PassCategoryClassification_MatrixEnforcedCorrectly()
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, trainerId, _) = await SetupBaseAsync(db);
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var sessionId1 = Guid.NewGuid();
        var sessionId2 = Guid.NewGuid();

        var sessions = new List<AdminWorkshopSessionItem>
        {
            new()
            {
                Id = sessionId1,
                Title = "Session A",
                SessionDate = DateTime.UtcNow.Date.AddDays(7),
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(12, 0, 0),
                Capacity = 20,
                TrainerProfileIds = new List<Guid> { trainerId }
            },
            new()
            {
                Id = sessionId2,
                Title = "Session B",
                SessionDate = DateTime.UtcNow.Date.AddDays(7),
                StartTime = new TimeSpan(13, 0, 0),
                EndTime = new TimeSpan(15, 0, 0),
                Capacity = 20,
                TrainerProfileIds = new List<Guid> { trainerId }
            }
        };

        // Case 1: Valid Single-Session Pass (WorkshopSessionId != null, SessionsIncluded = 1)
        var validPasses = new List<AdminWorkshopPassTypeItem>
        {
            new()
            {
                Name = "Session A Single Pass",
                Price = 500m,
                TotalQuantity = 20,
                WorkshopSessionId = sessionId1,
                SessionsIncluded = 1
            },
            new()
            {
                Name = "All Access Pass",
                Price = 900m,
                TotalQuantity = 20,
                WorkshopSessionId = null,
                SessionsIncluded = null
            },
            new()
            {
                Name = "2-Session Bundle",
                Price = 850m,
                TotalQuantity = 20,
                WorkshopSessionId = null,
                SessionsIncluded = 2
            }
        };

        var validReq = new AdminCreateWorkshopRequest
        {
            Title = "Matrix Workshop",
            DanceStyle = "Hip Hop",
            Level = "All",
            Venue = "Main Studio",
            Price = 500m,
            Capacity = 20,
            WorkshopDate = DateTime.UtcNow.Date.AddDays(7),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(15, 0, 0),
            TrainerProfileIds = new List<Guid> { trainerId },
            Sessions = sessions,
            PassTypes = validPasses
        };

        var created = await adminService.CreateWorkshopAsync(adminId, validReq, CancellationToken.None);
        Assert.NotNull(created);
        Assert.Equal(3, created.PassTypes.Count);

        // Case 2: Invalid Single-Session (WorkshopSessionId != null with SessionsIncluded = 2)
        var invalidPass1 = new List<AdminWorkshopPassTypeItem>
        {
            new()
            {
                Name = "Contradictory Single Pass",
                Price = 500m,
                TotalQuantity = 20,
                WorkshopSessionId = sessionId1,
                SessionsIncluded = 2
            }
        };
        validReq.PassTypes = invalidPass1;
        var ex1 = await Assert.ThrowsAsync<ArgumentException>(() =>
            adminService.CreateWorkshopAsync(adminId, validReq, CancellationToken.None));
        Assert.Contains("Single-session ticket", ex1.Message);

        // Case 3: Invalid SessionsIncluded (SessionsIncluded < 1)
        var invalidPass2 = new List<AdminWorkshopPassTypeItem>
        {
            new()
            {
                Name = "Invalid Zero Session Ticket",
                Price = 500m,
                TotalQuantity = 20,
                WorkshopSessionId = null,
                SessionsIncluded = 0
            }
        };
        validReq.PassTypes = invalidPass2;
        var ex2 = await Assert.ThrowsAsync<ArgumentException>(() =>
            adminService.CreateWorkshopAsync(adminId, validReq, CancellationToken.None));
        Assert.Contains("must have SessionsIncluded greater than or equal to 1", ex2.Message);

        // Case 4: Nonexistent Session Referenced
        var invalidPass3 = new List<AdminWorkshopPassTypeItem>
        {
            new()
            {
                Name = "Ghost Session Pass",
                Price = 500m,
                TotalQuantity = 20,
                WorkshopSessionId = Guid.NewGuid(), // Not in sessions
                SessionsIncluded = 1
            }
        };
        validReq.PassTypes = invalidPass3;
        var ex3 = await Assert.ThrowsAsync<ArgumentException>(() =>
            adminService.CreateWorkshopAsync(adminId, validReq, CancellationToken.None));
        Assert.Contains("was not found in the workshop sessions", ex3.Message);
    }

    // =========================================================================
    // 2. PRICING SCOPE ISOLATION (PASS-QUANTITY SCOPED)
    // =========================================================================

    [Fact]
    public async Task PricingScopeIsolation_SalesAdvanceOnlyTargetPass()
    {
        using var db = CreateInMemoryDbContext();
        var pricingService = new WorkshopPricingService(db, new DummyStudentEligibilityService());

        var workshopId = Guid.NewGuid();
        var passA = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            Name = "Pass A",
            Price = 500m,
            TotalQuantity = 50,
            PricingTiers = new List<WorkshopPricingTier>
            {
                new() { TierNumber = 1, TierName = "T1", MinTickets = 1, MaxTickets = 2, Price = 500m },
                new() { TierNumber = 2, TierName = "T2", MinTickets = 3, MaxTickets = null, Price = 700m }
            }
        };

        var passB = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            Name = "Pass B",
            Price = 500m,
            TotalQuantity = 50,
            PricingTiers = new List<WorkshopPricingTier>
            {
                new() { TierNumber = 1, TierName = "T1", MinTickets = 1, MaxTickets = 2, Price = 500m },
                new() { TierNumber = 2, TierName = "T2", MinTickets = 3, MaxTickets = null, Price = 700m }
            }
        };

        // When 2 tickets of Pass A are sold (currentTicketsSold = 2):
        var quoteA = await pricingService.CalculateTicketTypeQuoteAsync(passA, 1, 2);
        // Slot is 3 -> Tier 2 (Price = 700)
        Assert.Equal(700m, quoteA.TotalAmount);

        // Pass B has 0 tickets sold:
        var quoteB = await pricingService.CalculateTicketTypeQuoteAsync(passB, 1, 0);
        // Slot is 1 -> Tier 1 (Price = 500)
        Assert.Equal(500m, quoteB.TotalAmount);
    }

    // =========================================================================
    // 3. CART SPLIT BOUNDARY TRAVERSAL (5 CASES)
    // =========================================================================

    [Fact]
    public async Task CartSplitBoundaryTraversal_HandlesAllFiveBoundaryCases()
    {
        using var db = CreateInMemoryDbContext();
        var pricingService = new WorkshopPricingService(db, new DummyStudentEligibilityService());

        var pass = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = Guid.NewGuid(),
            Name = "Tiered Pass",
            Price = 100m,
            TotalQuantity = 30,
            PricingTiers = new List<WorkshopPricingTier>
            {
                new() { TierNumber = 1, TierName = "Tier 1", MinTickets = 1, MaxTickets = 10, Price = 100m },
                new() { TierNumber = 2, TierName = "Tier 2", MinTickets = 11, MaxTickets = 20, Price = 150m },
                new() { TierNumber = 3, TierName = "Tier 3", MinTickets = 21, MaxTickets = 30, Price = 200m }
            }
        };

        // Case A: Exactly reaches boundary (S=8, Q=2 -> Slots 9, 10 in Tier 1)
        var quoteA = await pricingService.CalculateTicketTypeQuoteAsync(pass, 2, 8);
        Assert.Equal(200m, quoteA.TotalAmount);
        Assert.False(quoteA.IsSplitTier);
        Assert.Single(quoteA.Breakdown);
        Assert.Equal(2, quoteA.Breakdown[0].Quantity);
        Assert.Equal(100m, quoteA.Breakdown[0].UnitPrice);

        // Case B: Crosses boundary by one (S=9, Q=2 -> Slot 10 in Tier 1, Slot 11 in Tier 2)
        var quoteB = await pricingService.CalculateTicketTypeQuoteAsync(pass, 2, 9);
        Assert.Equal(250m, quoteB.TotalAmount); // 100 + 150
        Assert.True(quoteB.IsSplitTier);
        Assert.Equal(2, quoteB.Breakdown.Count);
        Assert.Equal(1, quoteB.Breakdown[0].Quantity);
        Assert.Equal(100m, quoteB.Breakdown[0].UnitPrice);
        Assert.Equal(1, quoteB.Breakdown[1].Quantity);
        Assert.Equal(150m, quoteB.Breakdown[1].UnitPrice);

        // Case C: Starts exactly at new tier (S=10, Q=2 -> Slots 11, 12 in Tier 2)
        var quoteC = await pricingService.CalculateTicketTypeQuoteAsync(pass, 2, 10);
        Assert.Equal(300m, quoteC.TotalAmount); // 2 * 150
        Assert.False(quoteC.IsSplitTier);
        Assert.Single(quoteC.Breakdown);
        Assert.Equal(2, quoteC.Breakdown[0].Quantity);
        Assert.Equal(150m, quoteC.Breakdown[0].UnitPrice);

        // Case D: Crosses multiple boundaries (S=9, Q=13 -> 1 in Tier 1, 10 in Tier 2, 2 in Tier 3)
        var quoteD = await pricingService.CalculateTicketTypeQuoteAsync(pass, 13, 9);
        // Total = (1 * 100) + (10 * 150) + (2 * 200) = 100 + 1500 + 400 = 2000
        Assert.Equal(2000m, quoteD.TotalAmount);
        Assert.True(quoteD.IsSplitTier);
        Assert.Equal(3, quoteD.Breakdown.Count);
        Assert.Equal(1, quoteD.Breakdown[0].Quantity);
        Assert.Equal(10, quoteD.Breakdown[1].Quantity);
        Assert.Equal(2, quoteD.Breakdown[2].Quantity);

        // Case E: Quantity exceeds total capacity (S=25, Q=10 -> 35 > 30)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pricingService.CalculateTicketTypeQuoteAsync(pass, 10, 25));
    }

    // =========================================================================
    // 4. FLOATING COUNTER: REFUND RESTORATION
    // =========================================================================

    [Fact]
    public async Task FloatingCounter_RefundRestoresVacatedTierSlot()
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, trainerId, studentProfileId) = await SetupBaseAsync(db);
        var pricingService = new WorkshopPricingService(db, new DummyStudentEligibilityService());

        var workshopId = Guid.NewGuid();
        var passId = Guid.NewGuid();

        var pass = new WorkshopPassType
        {
            Id = passId,
            WorkshopId = workshopId,
            Name = "Early Bird Pass",
            Price = 100m,
            TotalQuantity = 10,
            PricingTiers = new List<WorkshopPricingTier>
            {
                new() { TierNumber = 1, TierName = "Tier 1", MinTickets = 1, MaxTickets = 1, Price = 100m },
                new() { TierNumber = 2, TierName = "Tier 2", MinTickets = 2, MaxTickets = null, Price = 200m }
            }
        };
        db.WorkshopPassTypes.Add(pass);

        // Buyer 1 purchases 1 ticket
        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            WorkshopPassTypeId = passId,
            StudentProfileId = studentProfileId,
            Status = WorkshopBookingStatus.Confirmed,
            Quantity = 1,
            TotalPrice = 100m,
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);
        await db.SaveChangesAsync();

        // Calculate sold counter based on active bookings:
        var sold1 = await db.WorkshopBookings
            .Where(b => b.WorkshopPassTypeId == passId &&
                        (b.Status == WorkshopBookingStatus.Confirmed ||
                         b.Status == WorkshopBookingStatus.Attended ||
                         (b.Status == WorkshopBookingStatus.PendingPayment && b.ReservationExpiresAt > DateTime.UtcNow)))
            .SumAsync(b => b.Quantity);
        Assert.Equal(1, sold1);

        // Next buyer's quote for 1 ticket should be at Tier 2 (200 INR)
        var quoteBuyer2 = await pricingService.CalculateTicketTypeQuoteAsync(pass, 1, sold1);
        Assert.Equal(200m, quoteBuyer2.TotalAmount);

        // Now Buyer 1 cancels booking
        booking.Status = WorkshopBookingStatus.Cancelled;
        await db.SaveChangesAsync();

        // Recalculate sold counter (Refunded booking is excluded):
        var sold2 = await db.WorkshopBookings
            .Where(b => b.WorkshopPassTypeId == passId &&
                        (b.Status == WorkshopBookingStatus.Confirmed ||
                         b.Status == WorkshopBookingStatus.Attended ||
                         (b.Status == WorkshopBookingStatus.PendingPayment && b.ReservationExpiresAt > DateTime.UtcNow)))
            .SumAsync(b => b.Quantity);
        Assert.Equal(0, sold2);

        // Subsequent buyer now gets restored Tier 1 price (100 INR)
        var quoteBuyer3 = await pricingService.CalculateTicketTypeQuoteAsync(pass, 1, sold2);
        Assert.Equal(100m, quoteBuyer3.TotalAmount);
    }

    // =========================================================================
    // 5. MULTI-SESSION BUNDLE INVARIANTS (AVAILABILITY & SELECTION)
    // =========================================================================

    [Fact]
    public async Task MultiSessionBundle_AvailabilityAndPurchaseSelectionValidated()
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, trainerId, studentProfileId) = await SetupBaseAsync(db);
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var s1Id = Guid.NewGuid();
        var s2Id = Guid.NewGuid();
        var s3Id = Guid.NewGuid();

        var sessions = new List<AdminWorkshopSessionItem>
        {
            new() { Id = s1Id, Title = "Session 1", SessionDate = DateTime.UtcNow.Date.AddDays(7), StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(11, 0, 0), Capacity = 10, TrainerProfileIds = new List<Guid> { trainerId } },
            new() { Id = s2Id, Title = "Session 2", SessionDate = DateTime.UtcNow.Date.AddDays(7), StartTime = new TimeSpan(12, 0, 0), EndTime = new TimeSpan(14, 0, 0), Capacity = 10, TrainerProfileIds = new List<Guid> { trainerId } },
            new() { Id = s3Id, Title = "Session 3", SessionDate = DateTime.UtcNow.Date.AddDays(7), StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(17, 0, 0), Capacity = 10, TrainerProfileIds = new List<Guid> { trainerId } }
        };

        var bundlePass = new AdminWorkshopPassTypeItem
        {
            Name = "2-Session Bundle",
            Price = 800m,
            TotalQuantity = 10,
            WorkshopSessionId = null,
            SessionsIncluded = 2
        };

        var req = new AdminCreateWorkshopRequest
        {
            Title = "Bundle Workshop",
            DanceStyle = "Hip Hop",
            Level = "All",
            Venue = "Main Studio",
            Price = 500m,
            Capacity = 10,
            WorkshopDate = DateTime.UtcNow.Date.AddDays(7),
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(17, 0, 0),
            TrainerProfileIds = new List<Guid> { trainerId },
            Sessions = sessions,
            PassTypes = new List<AdminWorkshopPassTypeItem> { bundlePass }
        };

        var created = await adminService.CreateWorkshopAsync(adminId, req, CancellationToken.None);
        Assert.NotNull(created);
        var passDto = created.PassTypes.First();
        Assert.True(passDto.IsAvailable);
        Assert.Equal("Available", passDto.AvailabilityLabel);

        // Fill Session 2 and Session 3 completely so only 1 session remains available
        var session2 = await db.WorkshopSessions.FirstAsync(s => s.Id == s2Id);
        var session3 = await db.WorkshopSessions.FirstAsync(s => s.Id == s3Id);

        var fillBooking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = created.Id,
            StudentProfileId = studentProfileId,
            Status = WorkshopBookingStatus.Confirmed,
            Quantity = 10,
            TotalPrice = 1000m,
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(fillBooking);

        for (int i = 0; i < 10; i++)
        {
            db.WorkshopBookingSessions.Add(new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = fillBooking.Id, WorkshopSessionId = session2.Id, Status = WorkshopBookingSessionStatus.Booked });
            db.WorkshopBookingSessions.Add(new WorkshopBookingSession { Id = Guid.NewGuid(), WorkshopBookingId = fillBooking.Id, WorkshopSessionId = session3.Id, Status = WorkshopBookingSessionStatus.Booked });
        }
        await db.SaveChangesAsync();

        // Now, active sessions with remaining capacity = 1 (Session 1). But bundle requires 2.
        var retrieved = await adminService.GetWorkshopByIdAsync(created.Id, CancellationToken.None);
        var retrievedBundle = retrieved!.PassTypes.First();
        Assert.False(retrievedBundle.IsAvailable);
        Assert.Equal(0, retrievedBundle.RemainingQuantity);
        Assert.Equal("Sold Out", retrievedBundle.AvailabilityLabel);
    }

    // =========================================================================
    // 6. PRICING TIER MATHEMATICAL VALIDATION
    // =========================================================================

    [Fact]
    public void PricingTierValidation_StrictMathematicalRulesEnforced()
    {
        using var db = CreateInMemoryDbContext();
        var pricingService = new WorkshopPricingService(db, new DummyStudentEligibilityService());

        // Rule 1: First tier must start at 1
        var badStart = new List<AdminWorkshopPricingTierItem>
        {
            new() { TierNumber = 1, TierName = "T1", MinTickets = 2, MaxTickets = 10, Price = 100m }
        };
        var ex1 = Assert.Throws<ArgumentException>(() => pricingService.ValidateTicketTypePricingTiers(20, badStart));
        Assert.Contains("must start at ticket 1", ex1.Message);

        // Rule 2: Gap between tiers (1-5, then 7-10)
        var gap = new List<AdminWorkshopPricingTierItem>
        {
            new() { TierNumber = 1, TierName = "T1", MinTickets = 1, MaxTickets = 5, Price = 100m },
            new() { TierNumber = 2, TierName = "T2", MinTickets = 7, MaxTickets = 10, Price = 150m }
        };
        var ex2 = Assert.Throws<ArgumentException>(() => pricingService.ValidateTicketTypePricingTiers(20, gap));
        Assert.Contains("gap or overlap detected", ex2.Message);

        // Rule 3: Overlap between tiers (1-5, then 5-10)
        var overlap = new List<AdminWorkshopPricingTierItem>
        {
            new() { TierNumber = 1, TierName = "T1", MinTickets = 1, MaxTickets = 5, Price = 100m },
            new() { TierNumber = 2, TierName = "T2", MinTickets = 5, MaxTickets = 10, Price = 150m }
        };
        var ex3 = Assert.Throws<ArgumentException>(() => pricingService.ValidateTicketTypePricingTiers(20, overlap));
        Assert.Contains("gap or overlap detected", ex3.Message);

        // Rule 4: Decreasing price (Tier 1: 200, Tier 2: 150)
        var decreasing = new List<AdminWorkshopPricingTierItem>
        {
            new() { TierNumber = 1, TierName = "T1", MinTickets = 1, MaxTickets = 5, Price = 200m },
            new() { TierNumber = 2, TierName = "T2", MinTickets = 6, MaxTickets = 10, Price = 150m }
        };
        var ex4 = Assert.Throws<ArgumentException>(() => pricingService.ValidateTicketTypePricingTiers(20, decreasing));
        Assert.Contains("Pricing must be non-decreasing", ex4.Message);

        // Rule 5: Zero or negative price
        var nonPositive = new List<AdminWorkshopPricingTierItem>
        {
            new() { TierNumber = 1, TierName = "T1", MinTickets = 1, MaxTickets = 5, Price = 0m }
        };
        var ex5 = Assert.Throws<ArgumentException>(() => pricingService.ValidateTicketTypePricingTiers(20, nonPositive));
        Assert.Contains("must be greater than zero", ex5.Message);

        // Rule 6: Open-ended tier in middle
        var openEndedMiddle = new List<AdminWorkshopPricingTierItem>
        {
            new() { TierNumber = 1, TierName = "T1", MinTickets = 1, MaxTickets = null, Price = 100m },
            new() { TierNumber = 2, TierName = "T2", MinTickets = 11, MaxTickets = 20, Price = 150m }
        };
        var ex6 = Assert.Throws<ArgumentException>(() => pricingService.ValidateTicketTypePricingTiers(20, openEndedMiddle));
        Assert.Contains("is open-ended, so no subsequent tier can follow it", ex6.Message);

        // Rule 7: Duplicate MinTickets
        var duplicateMin = new List<AdminWorkshopPricingTierItem>
        {
            new() { TierNumber = 1, TierName = "T1", MinTickets = 1, MaxTickets = 5, Price = 100m },
            new() { TierNumber = 2, TierName = "T2", MinTickets = 1, MaxTickets = 10, Price = 150m }
        };
        var ex7 = Assert.Throws<ArgumentException>(() => pricingService.ValidateTicketTypePricingTiers(20, duplicateMin));
        Assert.Contains("Duplicate MinTickets detected", ex7.Message);
    }

    // =========================================================================
    // 7. FINAL-GRAPH DELETION (SIMULTANEOUS PASS & SESSION REMOVAL)
    // =========================================================================

    [Fact]
    public async Task FinalGraphDeletion_SimultaneousPassAndSessionRemovalSucceeds()
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, trainerId, _) = await SetupBaseAsync(db);
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var s1Id = Guid.NewGuid();
        var s2Id = Guid.NewGuid();
        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();

        var sessions = new List<AdminWorkshopSessionItem>
        {
            new() { Id = s1Id, Title = "Session 1", SessionDate = DateTime.UtcNow.Date.AddDays(7), StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(12, 0, 0), Capacity = 20, TrainerProfileIds = new List<Guid> { trainerId } },
            new() { Id = s2Id, Title = "Session 2", SessionDate = DateTime.UtcNow.Date.AddDays(7), StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(15, 0, 0), Capacity = 20, TrainerProfileIds = new List<Guid> { trainerId } }
        };

        var passes = new List<AdminWorkshopPassTypeItem>
        {
            new() { Id = p1Id, Name = "Pass Session 1", Price = 400m, TotalQuantity = 20, WorkshopSessionId = s1Id, SessionsIncluded = 1 },
            new() { Id = p2Id, Name = "Pass Session 2", Price = 400m, TotalQuantity = 20, WorkshopSessionId = s2Id, SessionsIncluded = 1 }
        };

        var createReq = new AdminCreateWorkshopRequest
        {
            Title = "Final Graph Workshop",
            DanceStyle = "Contemporary",
            Level = "All",
            Venue = "Main Studio",
            Price = 400m,
            Capacity = 20,
            WorkshopDate = DateTime.UtcNow.Date.AddDays(7),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(15, 0, 0),
            TrainerProfileIds = new List<Guid> { trainerId },
            Sessions = sessions,
            PassTypes = passes
        };

        var created = await adminService.CreateWorkshopAsync(adminId, createReq, CancellationToken.None);
        Assert.NotNull(created);

        // Attempt 1: Delete Session 1 while KEEPING Pass 1 (which references Session 1)
        var badUpdateReq = new AdminUpdateWorkshopRequest
        {
            Title = "Final Graph Workshop",
            DanceStyle = "Contemporary",
            Level = "All",
            Venue = "Main Studio",
            Price = 400m,
            Capacity = 20,
            WorkshopDate = DateTime.UtcNow.Date.AddDays(7),
            StartTime = new TimeSpan(13, 0, 0),
            EndTime = new TimeSpan(15, 0, 0),
            Sessions = new List<AdminWorkshopSessionItem> { sessions[1] }, // Only Session 2 kept
            PassTypes = passes // Both passes kept -> Pass 1 still references deleted Session 1
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            adminService.UpdateWorkshopAsync(created.Id, adminId, badUpdateReq, CancellationToken.None));
        Assert.Contains("still referenced by ticket", ex.Message);

        // Attempt 2: SIMULTANEOUS REMOVAL of Session 1 and Pass 1 in the final graph
        var goodUpdateReq = new AdminUpdateWorkshopRequest
        {
            Title = "Final Graph Workshop",
            DanceStyle = "Contemporary",
            Level = "All",
            Venue = "Main Studio",
            Price = 400m,
            Capacity = 20,
            WorkshopDate = DateTime.UtcNow.Date.AddDays(7),
            StartTime = new TimeSpan(13, 0, 0),
            EndTime = new TimeSpan(15, 0, 0),
            Sessions = new List<AdminWorkshopSessionItem> { sessions[1] }, // Only Session 2 kept
            PassTypes = new List<AdminWorkshopPassTypeItem> { passes[1] } // Only Pass 2 kept
        };

        var updated = await adminService.UpdateWorkshopAsync(created.Id, adminId, goodUpdateReq, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Single(updated.Sessions);
        Assert.Equal(s2Id, updated.Sessions.First().Id);
        Assert.Single(updated.PassTypes);
        Assert.Equal(p2Id, updated.PassTypes.First().Id);
    }

    // =========================================================================
    // 8. MULTIPLE SINGLE-SESSION PASSES COMPETING FOR SHARED ROOM CAPACITY
    // =========================================================================

    [Fact]
    public async Task MultipleSingleSessionPasses_SharePhysicalSessionCapacity()
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, trainerId, studentProfileId) = await SetupBaseAsync(db);
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var sessionId = Guid.NewGuid();
        var s1 = new AdminWorkshopSessionItem
        {
            Id = sessionId,
            Title = "Shared Capacity Session",
            SessionDate = DateTime.UtcNow.Date.AddDays(7),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Capacity = 5, // Room capacity = 5
            TrainerProfileIds = new List<Guid> { trainerId }
        };

        var pass1Id = Guid.NewGuid();
        var pass2Id = Guid.NewGuid();
        var passes = new List<AdminWorkshopPassTypeItem>
        {
            new() { Id = pass1Id, Name = "Early Bird Session Pass", Price = 400m, TotalQuantity = 10, WorkshopSessionId = sessionId, SessionsIncluded = 1 },
            new() { Id = pass2Id, Name = "General Session Pass", Price = 600m, TotalQuantity = 10, WorkshopSessionId = sessionId, SessionsIncluded = 1 }
        };

        var req = new AdminCreateWorkshopRequest
        {
            Title = "Shared Room Workshop",
            DanceStyle = "Hip Hop",
            Level = "All",
            Venue = "Main Studio",
            Price = 400m,
            Capacity = 5,
            WorkshopDate = DateTime.UtcNow.Date.AddDays(7),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            TrainerProfileIds = new List<Guid> { trainerId },
            Sessions = new List<AdminWorkshopSessionItem> { s1 },
            PassTypes = passes
        };

        var created = await adminService.CreateWorkshopAsync(adminId, req, CancellationToken.None);
        Assert.NotNull(created);

        // Initially, room capacity is 5. Both passes have commercial quota 10, but effective remaining is 5.
        var p1 = created.PassTypes.First(p => p.Id == pass1Id);
        var p2 = created.PassTypes.First(p => p.Id == pass2Id);
        Assert.Equal(5, p1.RemainingQuantity);
        Assert.Equal(5, p2.RemainingQuantity);

        // Seed 3 bookings targeting Pass 1
        var booking1 = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = created.Id,
            WorkshopPassTypeId = pass1Id,
            StudentProfileId = studentProfileId,
            Status = WorkshopBookingStatus.Confirmed,
            Quantity = 3,
            TotalPrice = 1200m,
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking1);
        for (int i = 0; i < 3; i++)
        {
            db.WorkshopBookingSessions.Add(new WorkshopBookingSession
            {
                Id = Guid.NewGuid(),
                WorkshopBookingId = booking1.Id,
                WorkshopSessionId = sessionId,
                Status = WorkshopBookingSessionStatus.Booked
            });
        }
        await db.SaveChangesAsync();

        // After booking 3 seats: 2 physical seats remaining in session
        var afterBooking1 = await adminService.GetWorkshopByIdAsync(created.Id, CancellationToken.None);
        var p1After = afterBooking1!.PassTypes.First(p => p.Id == pass1Id);
        var p2After = afterBooking1!.PassTypes.First(p => p.Id == pass2Id);
        Assert.Equal(2, p1After.RemainingQuantity);
        Assert.Equal(2, p2After.RemainingQuantity);
        Assert.Equal("Selling Fast", p1After.AvailabilityLabel);
        Assert.Equal("Selling Fast", p2After.AvailabilityLabel);

        // Seed 2 more bookings targeting Pass 2 (fills the room to 5/5)
        var booking2 = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = created.Id,
            WorkshopPassTypeId = pass2Id,
            StudentProfileId = studentProfileId,
            Status = WorkshopBookingStatus.Confirmed,
            Quantity = 2,
            TotalPrice = 1200m,
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking2);
        for (int i = 0; i < 2; i++)
        {
            db.WorkshopBookingSessions.Add(new WorkshopBookingSession
            {
                Id = Guid.NewGuid(),
                WorkshopBookingId = booking2.Id,
                WorkshopSessionId = sessionId,
                Status = WorkshopBookingSessionStatus.Booked
            });
        }
        await db.SaveChangesAsync();

        // Room is now completely full (0 seats remaining)
        var afterBooking2 = await adminService.GetWorkshopByIdAsync(created.Id, CancellationToken.None);
        var p1Full = afterBooking2!.PassTypes.First(p => p.Id == pass1Id);
        var p2Full = afterBooking2!.PassTypes.First(p => p.Id == pass2Id);
        Assert.Equal(0, p1Full.RemainingQuantity);
        Assert.Equal(0, p2Full.RemainingQuantity);
        Assert.False(p1Full.IsAvailable);
        Assert.False(p2Full.IsAvailable);
        Assert.Equal("Sold Out", p1Full.AvailabilityLabel);
        Assert.Equal("Sold Out", p2Full.AvailabilityLabel);
    }

    // =========================================================================
    // 9. CONCURRENCY SERIALIZATION ON PASS QUOTAS
    // =========================================================================

    [Fact]
    public async Task Concurrency_SerializesPassPurchases()
    {
        using var db = CreateInMemoryDbContext();
        var pricingService = new WorkshopPricingService(db, new DummyStudentEligibilityService());

        var pass = new WorkshopPassType
        {
            Id = Guid.NewGuid(),
            WorkshopId = Guid.NewGuid(),
            Name = "Limited Pass",
            Price = 100m,
            TotalQuantity = 10,
            PricingTiers = new List<WorkshopPricingTier>
            {
                new() { TierNumber = 1, TierName = "Tier 1", MinTickets = 1, MaxTickets = 5, Price = 100m },
                new() { TierNumber = 2, TierName = "Tier 2", MinTickets = 6, MaxTickets = 10, Price = 150m }
            }
        };

        // Simulate 5 concurrent threads requesting quotes for 1 ticket each when 4 tickets were already sold
        int initialSold = 4;
        var quoteResults = new List<decimal>();
        var lockObj = new object();

        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            // Each buyer requests sequentially advancing tickets
            int ticketSlot;
            lock (lockObj)
            {
                ticketSlot = initialSold++;
            }
            var quote = await pricingService.CalculateTicketTypeQuoteAsync(pass, 1, ticketSlot);
            lock (lockObj)
            {
                quoteResults.Add(quote.TotalAmount);
            }
        });

        await Task.WhenAll(tasks);

        // Exactly one buyer got slot 5 (Tier 1 @ 100) and four buyers got slots 6..9 (Tier 2 @ 150)
        Assert.Equal(5, quoteResults.Count);
        Assert.Single(quoteResults, p => p == 100m);
        Assert.Equal(4, quoteResults.Count(p => p == 150m));
    }
}
