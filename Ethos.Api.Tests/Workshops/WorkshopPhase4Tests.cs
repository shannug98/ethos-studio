using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class WorkshopPhase4Tests
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

    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private async Task<(Guid adminId, Guid trainer1Id, Guid trainer2Id, Guid trainer3Id, Guid studentProfileId)> SetupAdminAndTrainersAsync(AppDbContext db)
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

        var trainer1 = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TrainerCode = "TR01",
            FullName = "Trainer One",
            Status = TrainerStatus.Active,
            PrimaryDanceStyle = "Hip Hop",
            CreatedAt = DateTime.UtcNow
        };
        var trainer2 = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TrainerCode = "TR02",
            FullName = "Trainer Two",
            Status = TrainerStatus.Active,
            PrimaryDanceStyle = "Contemporary",
            CreatedAt = DateTime.UtcNow
        };
        var trainer3 = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TrainerCode = "TR03",
            FullName = "Trainer Three",
            Status = TrainerStatus.Active,
            PrimaryDanceStyle = "Commercial",
            CreatedAt = DateTime.UtcNow
        };

        var studentProfile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = adminId,
            CreatedAt = DateTime.UtcNow
        };

        db.TrainerProfiles.AddRange(trainer1, trainer2, trainer3);
        db.StudentProfiles.Add(studentProfile);
        await db.SaveChangesAsync();

        return (adminId, trainer1.Id, trainer2.Id, trainer3.Id, studentProfile.Id);
    }

    private AdminCreateWorkshopRequest BuildBaseCreateRequest(List<Guid> facultyPool, List<AdminWorkshopSessionItem> sessions)
    {
        return new AdminCreateWorkshopRequest
        {
            Title = "Urban Dance Intensive",
            Description = "Full weekend intensive",
            DanceStyle = "Hip Hop",
            Level = "Open Level",
            WorkshopDate = new DateTime(2026, 11, 20),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(13, 0, 0),
            Venue = "Ethos Studio A",
            Price = 1500m,
            Capacity = 30,
            TrainerProfileId = facultyPool.First(),
            TrainerProfileIds = facultyPool,
            Sessions = sessions
        };
    }

    // =========================================================================
    // SECTION 1: SCHEDULE OVERLAP 6 BOUNDARY CASES
    // =========================================================================

    [Fact]
    public async Task Overlap_Case1_SameTrainer_SameDate_TimeOverlap_ThrowsArgumentException()
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, t1, t2, _, _) = await SetupAdminAndTrainersAsync(db);
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var sessions = new List<AdminWorkshopSessionItem>
        {
            new() { Title = "Session 1", SessionDate = new DateTime(2026, 11, 20), StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), Capacity = 25, TrainerProfileIds = new List<Guid> { t1 } },
            new() { Title = "Session 2", SessionDate = new DateTime(2026, 11, 20), StartTime = new TimeSpan(11, 0, 0), EndTime = new TimeSpan(12, 30, 0), Capacity = 25, TrainerProfileIds = new List<Guid> { t1 } }
        };

        var request = BuildBaseCreateRequest(new List<Guid> { t1, t2 }, sessions);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => adminService.CreateWorkshopAsync(adminId, request, CancellationToken.None));
        Assert.Contains("overlapping sessions", ex.Message);
        Assert.Contains("Session 1", ex.Message);
        Assert.Contains("Session 2", ex.Message);
    }

    [Fact]
    public async Task Overlap_Case2_SameTrainer_SameDate_TouchingBoundaries_Allowed()
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, t1, _, _, _) = await SetupAdminAndTrainersAsync(db);
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        // 10:00 - 11:00 and 11:00 - 12:00: Touching boundary, should succeed without overlap error!
        var sessions = new List<AdminWorkshopSessionItem>
        {
            new() { Title = "Session 1", SessionDate = new DateTime(2026, 11, 20), StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 0, 0), Capacity = 25, TrainerProfileIds = new List<Guid> { t1 } },
            new() { Title = "Session 2", SessionDate = new DateTime(2026, 11, 20), StartTime = new TimeSpan(11, 0, 0), EndTime = new TimeSpan(12, 0, 0), Capacity = 25, TrainerProfileIds = new List<Guid> { t1 } }
        };

        var request = BuildBaseCreateRequest(new List<Guid> { t1 }, sessions);

        var result = await adminService.CreateWorkshopAsync(adminId, request, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(2, result.Sessions.Count);
    }

    [Fact]
    public async Task Overlap_Case3_DifferentTrainers_SameDate_TimeOverlap_Allowed()
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, t1, t2, _, _) = await SetupAdminAndTrainersAsync(db);
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        // Concurrent sessions on the same date with different instructors in different studios/rooms
        var sessions = new List<AdminWorkshopSessionItem>
        {
            new() { Title = "Session A", SessionDate = new DateTime(2026, 11, 20), StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(12, 0, 0), Capacity = 20, TrainerProfileIds = new List<Guid> { t1 } },
            new() { Title = "Session B", SessionDate = new DateTime(2026, 11, 20), StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(12, 0, 0), Capacity = 20, TrainerProfileIds = new List<Guid> { t2 } }
        };

        var request = BuildBaseCreateRequest(new List<Guid> { t1, t2 }, sessions);

        var result = await adminService.CreateWorkshopAsync(adminId, request, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(2, result.Sessions.Count);
    }

    [Fact]
    public async Task Overlap_Case4_SharedSecondaryTrainer_SameDate_TimeOverlap_ThrowsArgumentException()
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, t1, t2, t3, _) = await SetupAdminAndTrainersAsync(db);
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        // Session 1: lead t1, secondary t3
        // Session 2: lead t2, secondary t3 -> t3 is double-booked during overlapping times!
        var sessions = new List<AdminWorkshopSessionItem>
        {
            new() { Title = "Session 1", SessionDate = new DateTime(2026, 11, 20), StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), Capacity = 25, TrainerProfileIds = new List<Guid> { t1, t3 } },
            new() { Title = "Session 2", SessionDate = new DateTime(2026, 11, 20), StartTime = new TimeSpan(11, 0, 0), EndTime = new TimeSpan(12, 30, 0), Capacity = 25, TrainerProfileIds = new List<Guid> { t2, t3 } }
        };

        var request = BuildBaseCreateRequest(new List<Guid> { t1, t2, t3 }, sessions);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => adminService.CreateWorkshopAsync(adminId, request, CancellationToken.None));
        Assert.Contains("overlapping sessions", ex.Message);
    }

    [Fact]
    public async Task Overlap_Case5_SameTrainer_DifferentDates_SameTime_Allowed()
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, t1, _, _, _) = await SetupAdminAndTrainersAsync(db);
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        // Multi-day workshop: Same trainer on Friday and Saturday at 10:00 - 11:30
        var sessions = new List<AdminWorkshopSessionItem>
        {
            new() { Title = "Day 1 Workshop", SessionDate = new DateTime(2026, 11, 20), StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), Capacity = 30, TrainerProfileIds = new List<Guid> { t1 } },
            new() { Title = "Day 2 Workshop", SessionDate = new DateTime(2026, 11, 21), StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), Capacity = 30, TrainerProfileIds = new List<Guid> { t1 } }
        };

        var request = BuildBaseCreateRequest(new List<Guid> { t1 }, sessions);

        var result = await adminService.CreateWorkshopAsync(adminId, request, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(2, result.Sessions.Count);
    }

    [Fact]
    public async Task Overlap_Case6_EndTimeBeforeOrEqualToStartTime_ThrowsArgumentException()
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, t1, _, _, _) = await SetupAdminAndTrainersAsync(db);
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var sessions = new List<AdminWorkshopSessionItem>
        {
            new() { Title = "Invalid Time Session", SessionDate = new DateTime(2026, 11, 20), StartTime = new TimeSpan(12, 0, 0), EndTime = new TimeSpan(10, 0, 0), Capacity = 30, TrainerProfileIds = new List<Guid> { t1 } }
        };

        var request = BuildBaseCreateRequest(new List<Guid> { t1 }, sessions);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => adminService.CreateWorkshopAsync(adminId, request, CancellationToken.None));
        Assert.Contains("after start time", ex.Message);
    }

    // =========================================================================
    // SECTION 2: RELATIONAL FACULTY INVARIANT
    // =========================================================================

    [Fact]
    public async Task FacultyInvariant_SessionTrainerNotInWorkshopFaculty_ThrowsArgumentException()
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, t1, t2, rogueTrainer, _) = await SetupAdminAndTrainersAsync(db);
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        // Workshop faculty pool only contains t1 and t2
        var facultyPool = new List<Guid> { t1, t2 };

        // Session attempts to assign rogueTrainer who is not in faculty pool
        var sessions = new List<AdminWorkshopSessionItem>
        {
            new() { Title = "Session 1", SessionDate = new DateTime(2026, 11, 20), StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), Capacity = 25, TrainerProfileIds = new List<Guid> { rogueTrainer } }
        };

        var request = BuildBaseCreateRequest(facultyPool, sessions);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => adminService.CreateWorkshopAsync(adminId, request, CancellationToken.None));
        Assert.Contains("not part of the workshop faculty pool", ex.Message);
    }

    // =========================================================================
    // SECTION 3: LEAD INSTRUCTOR SYNCHRONIZATION
    // =========================================================================

    [Fact]
    public async Task LeadTrainer_DisplayOrder0_SynchronizedToLegacyTrainerProfileId()
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, t1, t2, t3, _) = await SetupAdminAndTrainersAsync(db);
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var sessions = new List<AdminWorkshopSessionItem>
        {
            new()
            {
                Title = "Trio Session",
                SessionDate = new DateTime(2026, 11, 20),
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(12, 0, 0),
                Capacity = 35,
                // Order: t2 (Lead), t1 (Co-instructor), t3 (Co-instructor)
                TrainerProfileIds = new List<Guid> { t2, t1, t3 }
            }
        };

        var request = BuildBaseCreateRequest(new List<Guid> { t1, t2, t3 }, sessions);

        var created = await adminService.CreateWorkshopAsync(adminId, request, CancellationToken.None);
        Assert.NotNull(created);

        // Verify in database
        var sessionEntity = await db.WorkshopSessions
            .Include(s => s.SessionTrainers)
            .FirstOrDefaultAsync(s => s.WorkshopId == created.Id);

        Assert.NotNull(sessionEntity);
        // Legacy TrainerProfileId matches DisplayOrder = 0 (t2)
        Assert.Equal(t2, sessionEntity.TrainerProfileId);
        Assert.Equal(3, sessionEntity.SessionTrainers.Count);

        var leadTrainer = sessionEntity.SessionTrainers.First(st => st.DisplayOrder == 0);
        Assert.Equal(t2, leadTrainer.TrainerProfileId);

        var secondTrainer = sessionEntity.SessionTrainers.First(st => st.DisplayOrder == 1);
        Assert.Equal(t1, secondTrainer.TrainerProfileId);

        var thirdTrainer = sessionEntity.SessionTrainers.First(st => st.DisplayOrder == 2);
        Assert.Equal(t3, thirdTrainer.TrainerProfileId);
    }

    // =========================================================================
    // SECTION 4: SESSION POSTER ROUND-TRIP & WORKSHOP COVER FALLBACK
    // =========================================================================

    [Fact]
    public async Task SessionPoster_PersistsCustomPoster_And_FallsBackToWorkshopCover()
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, t1, _, _, _) = await SetupAdminAndTrainersAsync(db);
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var customPosterUrl = "https://r2.ethos.dance/workshops/posters/session-urban.jpg";
        var workshopCoverUrl = "https://r2.ethos.dance/workshops/covers/main-cover.jpg";

        var sessions = new List<AdminWorkshopSessionItem>
        {
            new()
            {
                Title = "Session With Custom Poster",
                SessionDate = new DateTime(2026, 11, 20),
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(11, 30, 0),
                Capacity = 30,
                PosterImageUrl = customPosterUrl,
                TrainerProfileIds = new List<Guid> { t1 }
            },
            new()
            {
                Title = "Session Without Poster",
                SessionDate = new DateTime(2026, 11, 20),
                StartTime = new TimeSpan(12, 0, 0),
                EndTime = new TimeSpan(13, 30, 0),
                Capacity = 30,
                PosterImageUrl = null, // Will fall back to workshop cover
                TrainerProfileIds = new List<Guid> { t1 }
            }
        };

        var request = BuildBaseCreateRequest(new List<Guid> { t1 }, sessions);
        request.ImageUrl = workshopCoverUrl;

        var created = await adminService.CreateWorkshopAsync(adminId, request, CancellationToken.None);
        Assert.NotNull(created);

        // Verify mapped response
        var retrieved = await adminService.GetWorkshopByIdAsync(created.Id, CancellationToken.None);
        Assert.NotNull(retrieved);
        Assert.Equal(2, retrieved.Sessions.Count);

        var s1 = retrieved.Sessions.First(s => s.Title == "Session With Custom Poster");
        Assert.Equal(customPosterUrl, s1.PosterImageUrl);

        var s2 = retrieved.Sessions.First(s => s.Title == "Session Without Poster");
        Assert.Equal(workshopCoverUrl, s2.PosterImageUrl); // Verified fallback!
    }

    // =========================================================================
    // SECTION 5: AVAILABILITY LABEL CALCULATION
    // =========================================================================

    [Theory]
    [InlineData(25, 30, "Selling Fast")] // 5 remaining <= 5
    [InlineData(26, 30, "Selling Fast")] // 4 remaining <= 5
    [InlineData(30, 30, "Sold Out")]     // 0 remaining
    [InlineData(10, 30, "Available")]    // 20 remaining > 5
    [InlineData(0, 30, "Available")]     // 30 remaining
    public async Task AvailabilityLabel_ComputesCorrectlyFromRemainingSeats(
        int bookedCount, int capacity, string expectedLabel)
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, t1, _, _, studentProfileId) = await SetupAdminAndTrainersAsync(db);
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var sessions = new List<AdminWorkshopSessionItem>
        {
            new()
            {
                Title = "Availability Session",
                SessionDate = DateTime.UtcNow.Date.AddDays(7),
                StartTime = new TimeSpan(14, 0, 0),
                EndTime = new TimeSpan(16, 0, 0),
                Capacity = capacity,
                TrainerProfileIds = new List<Guid> { t1 }
            }
        };

        var request = BuildBaseCreateRequest(new List<Guid> { t1 }, sessions);
        var created = await adminService.CreateWorkshopAsync(adminId, request, CancellationToken.None);
        var sessionEntity = await db.WorkshopSessions.FirstAsync(s => s.WorkshopId == created.Id);

        // Seed bookings to reach bookedCount
        if (bookedCount > 0)
        {
            var booking = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = created.Id,
                StudentProfileId = studentProfileId,
                Status = WorkshopBookingStatus.Confirmed,
                TotalPrice = 1000m,
                BookedAt = DateTime.UtcNow
            };
            db.WorkshopBookings.Add(booking);

            for (int i = 0; i < bookedCount; i++)
            {
                db.WorkshopBookingSessions.Add(new WorkshopBookingSession
                {
                    Id = Guid.NewGuid(),
                    WorkshopBookingId = booking.Id,
                    WorkshopSessionId = sessionEntity.Id,
                    Status = WorkshopBookingSessionStatus.Booked
                });
            }
            await db.SaveChangesAsync();
        }

        var retrieved = await adminService.GetWorkshopByIdAsync(created.Id, CancellationToken.None);
        Assert.NotNull(retrieved);
        var sessionDto = retrieved.Sessions.First();
        Assert.Equal(expectedLabel, sessionDto.AvailabilityLabel);
    }

    // =========================================================================
    // SECTION 6: UPDATE WORKSHOP & CAPACITY CONCURRENCY ROW LOCKING
    // =========================================================================

    [Fact]
    public async Task UpdateWorkshop_SyncsMultiTrainerAndSessionCapacity()
    {
        using var db = CreateInMemoryDbContext();
        var (adminId, t1, t2, _, _) = await SetupAdminAndTrainersAsync(db);
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        // 1. Create workshop with 1 session
        var createSessions = new List<AdminWorkshopSessionItem>
        {
            new()
            {
                Title = "Original Session",
                SessionDate = new DateTime(2026, 11, 20),
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(11, 30, 0),
                Capacity = 25,
                TrainerProfileIds = new List<Guid> { t1 }
            }
        };
        var created = await adminService.CreateWorkshopAsync(adminId, BuildBaseCreateRequest(new List<Guid> { t1, t2 }, createSessions), CancellationToken.None);
        var originalSessionId = created.Sessions.First().Id;

        // 2. Update workshop: expand session capacity to 45, add t2 as co-instructor
        var updateRequest = new AdminUpdateWorkshopRequest
        {
            Title = "Updated Urban Dance Intensive",
            Description = "Updated description",
            DanceStyle = "Hip Hop",
            Level = "Open Level",
            WorkshopDate = new DateTime(2026, 11, 20),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Venue = "Ethos Studio Main",
            Price = 1600m,
            Capacity = 45,
            TrainerProfileId = t1,
            TrainerProfileIds = new List<Guid> { t1, t2 },
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new()
                {
                    Id = originalSessionId,
                    Title = "Expanded Session",
                    SessionDate = new DateTime(2026, 11, 20),
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(11, 30, 0),
                    Capacity = 45,
                    TrainerProfileIds = new List<Guid> { t1, t2 }
                }
            }
        };

        var updated = await adminService.UpdateWorkshopAsync(created.Id, adminId, updateRequest, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(45, updated.Capacity);

        var updatedSession = updated.Sessions.First();
        Assert.Equal(45, updatedSession.Capacity);
        Assert.Equal(2, updatedSession.Trainers.Count);
        Assert.Equal(t1, updatedSession.Trainers[0].TrainerProfileId);
        Assert.Equal(t2, updatedSession.Trainers[1].TrainerProfileId);
    }

    [Fact]
    public async Task PostgreSql_LiveDatabase_VerifyPhase4_InvariantsAndSchema()
    {
        var pgConnection = Environment.GetEnvironmentVariable("ETHOS_TEST_POSTGRES")
                           ?? Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(pgConnection))
        {
            return; // Skip if no live postgres connection configured
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(pgConnection)
            .Options;

        using var db = new AppDbContext(options);

        // 1. Verify workshop_sessions.PosterImageUrl column exists in database
        var posterColumnExists = await db.Database.SqlQueryRaw<int>(
            @"SELECT COUNT(*)::int AS ""Value""
              FROM information_schema.columns
              WHERE table_name = 'workshop_sessions' AND column_name = 'PosterImageUrl'")
            .ToListAsync();
        Assert.Equal(1, posterColumnExists[0]);

        // 2. Verify workshop_session_trainers table exists with composite FKs
        var sessionTrainersTableExists = await db.Database.SqlQueryRaw<int>(
            @"SELECT COUNT(*)::int AS ""Value""
              FROM information_schema.tables
              WHERE table_name = 'workshop_session_trainers'")
            .ToListAsync();
        Assert.Equal(1, sessionTrainersTableExists[0]);

        // 3. Verify zero orphan session trainers in PostgreSQL
        var orphanTrainers = await db.Database.SqlQueryRaw<int>(
            @"SELECT COUNT(*)::int AS ""Value""
              FROM workshop_session_trainers wst
              JOIN workshop_sessions ws ON wst.""WorkshopSessionId"" = ws.""Id""
              LEFT JOIN workshop_trainers wt ON ws.""WorkshopId"" = wt.""WorkshopId"" AND wst.""TrainerProfileId"" = wt.""TrainerProfileId""
              WHERE wt.""Id"" IS NULL")
            .ToListAsync();
        Assert.Equal(0, orphanTrainers[0]);
    }
}
