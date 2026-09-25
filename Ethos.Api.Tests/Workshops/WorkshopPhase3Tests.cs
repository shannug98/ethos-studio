using System.Text.Json;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Exceptions;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class WorkshopPhase3Tests
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

    [Fact]
    public async Task Draft_FirstSave_CreatesVersion1_And_RetrievesSuccessfully()
    {
        using var db = CreateInMemoryDbContext();
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var adminId = Guid.NewGuid();
        var request = new AdminSaveWorkshopDraftRequest
        {
            WorkshopId = null, // New workshop draft
            DraftJson = "{\"title\":\"My New Workshop\",\"danceStyle\":\"Hip Hop\"}",
            ExpectedVersion = 0
        };

        var createdDraft = await adminService.SaveDraftAsync(adminId, request, CancellationToken.None);

        Assert.NotNull(createdDraft);
        Assert.Equal(1, createdDraft.Version);
        Assert.Equal(adminId, createdDraft.AdminUserId);
        Assert.Null(createdDraft.WorkshopId);

        // Retrieve draft
        var retrieved = await adminService.GetDraftAsync(adminId, null, CancellationToken.None);
        Assert.NotNull(retrieved);
        Assert.Equal(createdDraft.Id, retrieved.Id);
        Assert.Equal(1, retrieved.Version);
    }

    [Fact]
    public async Task Draft_SaveWithMatchingVersion_IncrementsVersion()
    {
        using var db = CreateInMemoryDbContext();
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var adminId = Guid.NewGuid();
        var initial = await adminService.SaveDraftAsync(adminId, new AdminSaveWorkshopDraftRequest
        {
            WorkshopId = null,
            DraftJson = "{\"title\":\"Version 1\"}",
            ExpectedVersion = 0
        }, CancellationToken.None);

        Assert.Equal(1, initial.Version);

        // Second save with matching version 1
        var updated = await adminService.SaveDraftAsync(adminId, new AdminSaveWorkshopDraftRequest
        {
            WorkshopId = null,
            DraftJson = "{\"title\":\"Version 2\"}",
            ExpectedVersion = 1
        }, CancellationToken.None);

        Assert.Equal(2, updated.Version);

        var retrieved = await adminService.GetDraftAsync(adminId, null, CancellationToken.None);
        Assert.Equal(2, retrieved!.Version);
        Assert.Contains("Version 2", retrieved.DraftJson);
    }

    [Fact]
    public async Task Draft_SaveWithStaleVersion_ThrowsConflictException409()
    {
        using var db = CreateInMemoryDbContext();
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var adminId = Guid.NewGuid();
        await adminService.SaveDraftAsync(adminId, new AdminSaveWorkshopDraftRequest
        {
            WorkshopId = null,
            DraftJson = "{\"title\":\"Initial\"}",
            ExpectedVersion = 0
        }, CancellationToken.None);

        // Update to version 2
        await adminService.SaveDraftAsync(adminId, new AdminSaveWorkshopDraftRequest
        {
            WorkshopId = null,
            DraftJson = "{\"title\":\"Updated\"}",
            ExpectedVersion = 1
        }, CancellationToken.None);

        // Stale client still sends expectedVersion = 1
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            adminService.SaveDraftAsync(adminId, new AdminSaveWorkshopDraftRequest
            {
                WorkshopId = null,
                DraftJson = "{\"title\":\"Stale Edit\"}",
                ExpectedVersion = 1
            }, CancellationToken.None));

        Assert.Equal("DRAFT_CONFLICT", ex.Code);
        Assert.Equal(409, ex.StatusCode);
        Assert.Equal(2, ex.ServerVersion);
    }

    [Fact]
    public async Task Draft_Discard_EnforcesAdminOwnership()
    {
        using var db = CreateInMemoryDbContext();
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var adminA = Guid.NewGuid();
        var adminB = Guid.NewGuid();

        var draftA = await adminService.SaveDraftAsync(adminA, new AdminSaveWorkshopDraftRequest
        {
            WorkshopId = null,
            DraftJson = "{\"title\":\"Admin A Draft\"}",
            ExpectedVersion = 0
        }, CancellationToken.None);

        // Admin B attempts to discard Admin A's draft
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            adminService.DiscardDraftAsync(adminB, draftA.Id, CancellationToken.None));

        // Admin A discards own draft successfully
        var discarded = await adminService.DiscardDraftAsync(adminA, draftA.Id, CancellationToken.None);
        Assert.True(discarded);

        var check = await adminService.GetDraftAsync(adminA, null, CancellationToken.None);
        Assert.Null(check);
    }

    [Fact]
    public async Task FacultyRemovalInvariant_WhenTrainerAssignedToSession_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext();
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var trainerAId = Guid.NewGuid();
        var trainerBId = Guid.NewGuid();

        var trainerRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == "TRAINER");
        if (trainerRole == null)
        {
            trainerRole = new Role { Id = Guid.NewGuid(), Code = "TRAINER", Name = "Trainer" };
            db.Roles.Add(trainerRole);
        }

        var trainerAUser = new User { Id = Guid.NewGuid(), FullName = "Trainer A", CustomerCode = "TRA", Email = "a@ethos.test", Phone = "+919000000001" };
        var trainerBUser = new User { Id = Guid.NewGuid(), FullName = "Trainer B", CustomerCode = "TRB", Email = "b@ethos.test", Phone = "+919000000002" };
        db.Users.AddRange(trainerAUser, trainerBUser);

        db.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = trainerAUser.Id, RoleId = trainerRole.Id });
        db.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = trainerBUser.Id, RoleId = trainerRole.Id });

        var trainerA = new TrainerProfile { Id = trainerAId, UserId = trainerAUser.Id, FullName = "Trainer A", TrainerCode = "TRA", User = trainerAUser };
        var trainerB = new TrainerProfile { Id = trainerBId, UserId = trainerBUser.Id, FullName = "Trainer B", TrainerCode = "TRB", User = trainerBUser };
        db.TrainerProfiles.AddRange(trainerA, trainerB);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Faculty Test Workshop",
            DanceStyle = "Urban",
            Level = "Open",
            WorkshopDate = DateTime.UtcNow.AddDays(5),
            Venue = "Main Studio",
            Price = 500,
            Capacity = 30,
            Status = WorkshopStatus.Draft,
            TrainerProfileId = trainerAId
        };
        db.Workshops.Add(workshop);

        workshop.WorkshopTrainers.Add(new WorkshopTrainer { Id = Guid.NewGuid(), WorkshopId = workshop.Id, TrainerProfileId = trainerAId, DisplayOrder = 0 });
        workshop.WorkshopTrainers.Add(new WorkshopTrainer { Id = Guid.NewGuid(), WorkshopId = workshop.Id, TrainerProfileId = trainerBId, DisplayOrder = 1 });

        var session1 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Session 1 with Trainer B",
            SessionDate = DateTime.UtcNow.AddDays(5).Date,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12),
            Capacity = 30,
            TrainerProfileId = trainerBId
        };
        workshop.Sessions.Add(session1);
        db.WorkshopSessions.Add(session1);

        // Also add to workshop_session_trainers
        db.WorkshopSessionTrainers.Add(new WorkshopSessionTrainer
        {
            Id = Guid.NewGuid(),
            WorkshopSessionId = session1.Id,
            TrainerProfileId = trainerBId,
            DisplayOrder = 0
        });

        await db.SaveChangesAsync();

        // Admin attempts to update faculty to only [trainerAId], removing trainerB who is assigned to session1
        var updateRequest = new AdminUpdateWorkshopRequest
        {
            Title = workshop.Title,
            DanceStyle = workshop.DanceStyle,
            Level = workshop.Level,
            WorkshopDate = workshop.WorkshopDate,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12),
            Venue = workshop.Venue,
            Capacity = 30,
            Price = 500,
            TrainerProfileIds = new List<Guid> { trainerAId } // Omits trainerBId
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adminService.UpdateWorkshopAsync(workshop.Id, Guid.NewGuid(), updateRequest, CancellationToken.None));

        Assert.Contains("Cannot remove trainer", ex.Message);
        Assert.Contains("Trainer B", ex.Message);
        Assert.Contains("Session 1 with Trainer B", ex.Message);
    }

    [Fact]
    public async Task FacultyRemovalInvariant_WhenTrainerReassigned_RemovalSucceeds()
    {
        using var db = CreateInMemoryDbContext();
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var trainerAId = Guid.NewGuid();
        var trainerBId = Guid.NewGuid();

        var trainerRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == "TRAINER");
        if (trainerRole == null)
        {
            trainerRole = new Role { Id = Guid.NewGuid(), Code = "TRAINER", Name = "Trainer" };
            db.Roles.Add(trainerRole);
        }

        var trainerAUser = new User { Id = Guid.NewGuid(), FullName = "Trainer A", CustomerCode = "TR1", Email = "a1@ethos.test", Phone = "+919000000011" };
        var trainerBUser = new User { Id = Guid.NewGuid(), FullName = "Trainer B", CustomerCode = "TR2", Email = "b1@ethos.test", Phone = "+919000000012" };
        db.Users.AddRange(trainerAUser, trainerBUser);

        db.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = trainerAUser.Id, RoleId = trainerRole.Id });
        db.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = trainerBUser.Id, RoleId = trainerRole.Id });

        var trainerA = new TrainerProfile { Id = trainerAId, UserId = trainerAUser.Id, FullName = "Trainer A", TrainerCode = "TR1", User = trainerAUser };
        var trainerB = new TrainerProfile { Id = trainerBId, UserId = trainerBUser.Id, FullName = "Trainer B", TrainerCode = "TR2", User = trainerBUser };
        db.TrainerProfiles.AddRange(trainerA, trainerB);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Faculty Reassign Workshop",
            DanceStyle = "Urban",
            Level = "Open",
            WorkshopDate = DateTime.UtcNow.AddDays(5),
            Venue = "Main Studio",
            Price = 500,
            Capacity = 30,
            Status = WorkshopStatus.Draft,
            TrainerProfileId = trainerAId
        };
        db.Workshops.Add(workshop);

        workshop.WorkshopTrainers.Add(new WorkshopTrainer { Id = Guid.NewGuid(), WorkshopId = workshop.Id, TrainerProfileId = trainerAId, DisplayOrder = 0 });
        workshop.WorkshopTrainers.Add(new WorkshopTrainer { Id = Guid.NewGuid(), WorkshopId = workshop.Id, TrainerProfileId = trainerBId, DisplayOrder = 1 });

        var session1 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            Title = "Reassigned Session",
            SessionDate = DateTime.UtcNow.AddDays(5).Date,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12),
            Capacity = 30,
            TrainerProfileId = trainerAId // Session is assigned to A, not B
        };
        workshop.Sessions.Add(session1);
        db.WorkshopSessions.Add(session1);

        db.WorkshopSessionTrainers.Add(new WorkshopSessionTrainer
        {
            Id = Guid.NewGuid(),
            WorkshopSessionId = session1.Id,
            TrainerProfileId = trainerAId,
            DisplayOrder = 0
        });

        await db.SaveChangesAsync();

        // Now removing Trainer B from workshop faculty should succeed because Trainer B is not in any session
        var updateRequest = new AdminUpdateWorkshopRequest
        {
            Title = workshop.Title,
            DanceStyle = workshop.DanceStyle,
            Level = workshop.Level,
            WorkshopDate = workshop.WorkshopDate,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12),
            Venue = workshop.Venue,
            Capacity = 30,
            Price = 500,
            TrainerProfileIds = new List<Guid> { trainerAId }
        };

        var response = await adminService.UpdateWorkshopAsync(workshop.Id, Guid.NewGuid(), updateRequest, CancellationToken.None);

        Assert.NotNull(response);
        var updatedTrainers = await db.WorkshopTrainers.Where(wt => wt.WorkshopId == workshop.Id).ToListAsync();
        Assert.Single(updatedTrainers);
        Assert.Equal(trainerAId, updatedTrainers[0].TrainerProfileId);
    }

    [Fact]
    public async Task ServiceLayer_DraftPayloadOver2MB_ThrowsArgumentException()
    {
        using var db = CreateInMemoryDbContext();
        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var adminId = Guid.NewGuid();
        // 2 MB + 100 bytes payload
        var largeString = new string('x', 2 * 1024 * 1024 + 100);
        var request = new AdminSaveWorkshopDraftRequest
        {
            WorkshopId = null,
            DraftJson = "{\"title\":\"" + largeString + "\"}",
            ExpectedVersion = 0
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            adminService.SaveDraftAsync(adminId, request, CancellationToken.None));
        Assert.Contains("2 MB", ex.Message);
    }

    [Fact]
    public async Task PostgreSql_LiveDatabase_VerifyInvariants_And_PartialUniqueIndexes()
    {
        var pgConnection = Environment.GetEnvironmentVariable("ETHOS_TEST_POSTGRES")
                           ?? Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(pgConnection))
        {
            return; // Skip if no live postgres connection
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(pgConnection)
            .Options;

        using var db = new AppDbContext(options);

        // 1. Verify workshop_session_trainers unique index exists
        var sessionTrainersIndexes = await db.Database.SqlQueryRaw<string>(
            "SELECT indexname AS \"Value\" FROM pg_indexes WHERE tablename = 'workshop_session_trainers' AND indexdef LIKE '%WorkshopSessionId%' AND indexdef LIKE '%TrainerProfileId%'")
            .ToListAsync();
        Assert.NotEmpty(sessionTrainersIndexes);

        // 2. Verify workshop_drafts partial unique index for WorkshopId IS NULL
        var draftNullIndexes = await db.Database.SqlQueryRaw<string>(
            "SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'workshop_drafts' AND indexdef LIKE '%AdminUserId%' AND indexdef LIKE '%WorkshopId%IS NULL%'")
            .ToListAsync();
        Assert.NotEmpty(draftNullIndexes);

        // 3. Verify workshop_drafts partial unique index for WorkshopId IS NOT NULL
        var draftNotNullIndexes = await db.Database.SqlQueryRaw<string>(
            "SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'workshop_drafts' AND indexdef LIKE '%AdminUserId%' AND indexdef LIKE '%WorkshopId%IS NOT NULL%'")
            .ToListAsync();
        Assert.NotEmpty(draftNotNullIndexes);

        // 4. Verify Faculty Invariant: Every session trainer exists in workshop faculty pool
        var orphanSessionTrainers = await db.Database.SqlQueryRaw<int>(
            @"SELECT COUNT(*)::int AS ""Value""
              FROM workshop_session_trainers wst
              JOIN workshop_sessions ws ON wst.""WorkshopSessionId"" = ws.""Id""
              LEFT JOIN workshop_trainers wt ON ws.""WorkshopId"" = wt.""WorkshopId"" AND wst.""TrainerProfileId"" = wt.""TrainerProfileId""
              WHERE wt.""Id"" IS NULL")
            .ToListAsync();
        Assert.Equal(0, orphanSessionTrainers[0]);

        // 5. Test Live PostgreSQL Concurrent First-Save Race condition translation (expectedVersion = 0)
        var testUser = await db.Users.FirstOrDefaultAsync();
        Assert.NotNull(testUser);
        var testAdminId = testUser.Id;

        // Clean any existing draft for this user
        var existingDraft = await db.WorkshopDrafts.FirstOrDefaultAsync(d => d.AdminUserId == testAdminId && d.WorkshopId == null);
        if (existingDraft != null)
        {
            db.WorkshopDrafts.Remove(existingDraft);
            await db.SaveChangesAsync();
        }

        var adminService = new AdminWorkshopService(db, new DummyAuditService());

        var req1 = new AdminSaveWorkshopDraftRequest
        {
            WorkshopId = null,
            DraftJson = "{\"title\":\"Race 1\"}",
            ExpectedVersion = 0
        };
        var d1 = await adminService.SaveDraftAsync(testAdminId, req1, CancellationToken.None);
        Assert.Equal(1, d1.Version);

        // Second simultaneous first-save with expectedVersion = 0 hits PostgreSQL partial unique index and throws ConflictException
        var req2 = new AdminSaveWorkshopDraftRequest
        {
            WorkshopId = null,
            DraftJson = "{\"title\":\"Race 2\"}",
            ExpectedVersion = 0
        };
        await Assert.ThrowsAsync<ConflictException>(() =>
            adminService.SaveDraftAsync(testAdminId, req2, CancellationToken.None));

        // Cleanup test draft
        await adminService.DiscardDraftAsync(testAdminId, d1.Id, CancellationToken.None);
    }
}
