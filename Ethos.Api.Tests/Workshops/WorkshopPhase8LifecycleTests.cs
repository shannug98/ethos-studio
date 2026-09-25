using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Storage;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Controllers.Admin;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class WorkshopPhase8LifecycleTests
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

    private class TrackingR2StorageService : ICloudflareR2StorageService
    {
        public List<string> DeletedKeys { get; } = new();
        public bool ShouldThrowOnDelete { get; set; } = false;

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            if (ShouldThrowOnDelete)
            {
                throw new HttpRequestException("Simulated R2 network failure");
            }
            DeletedKeys.Add(objectKey);
            return Task.CompletedTask;
        }

        public Task<R2UploadResult> UploadAsync(
            Stream stream, string originalFileName, string contentType, string section, CancellationToken cancellationToken = default)
            => Task.FromResult(new R2UploadResult { ObjectKey = originalFileName, PublicUrl = $"https://r2.ethosdance.com/{originalFileName}" });

        public string GetPublicUrl(string objectKey) => $"https://r2.ethosdance.com/{objectKey}";
        public string GeneratePreSignedGetUrl(string objectKey, TimeSpan duration) => $"https://r2.ethosdance.com/{objectKey}";
        public string GeneratePreSignedPutUrl(string objectKey, string contentType, TimeSpan duration) => $"https://r2.ethosdance.com/{objectKey}";
        public Task<R2ObjectMetadata?> GetObjectMetadataAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult<R2ObjectMetadata?>(null);
        public Task<(Stream Stream, string ContentType)?> GetObjectStreamAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult<(Stream Stream, string ContentType)?>(null);
        public Task<R2RangeResult?> GetObjectRangeStreamAsync(string objectKey, long? fromByte, long? toByte, CancellationToken cancellationToken = default) => Task.FromResult<R2RangeResult?>(null);
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
            return Task.FromResult(new AdminAuthorizationResult { Success = false, StatusCode = 403, ErrorMessage = "Forbidden permission" });
        }
    }

    private class DummyGuestFeedbackService : Ethos.Api.Application.Feedback.IGuestWorkshopFeedbackService
    {
        public Task<Ethos.Api.Contracts.Feedback.GuestWorkshopFeedbackDetailsResponse> GetFeedbackDetailsByTokenAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new Ethos.Api.Contracts.Feedback.GuestWorkshopFeedbackDetailsResponse());

        public Task<bool> SubmitGuestFeedbackAsync(Ethos.Api.Contracts.Feedback.SubmitGuestWorkshopFeedbackRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<Ethos.Api.Contracts.Feedback.GenerateFeedbackTokenResponse> GenerateTokenForBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
            => Task.FromResult(new Ethos.Api.Contracts.Feedback.GenerateFeedbackTokenResponse());

        public Task<Ethos.Api.Contracts.Feedback.TrainerAggregateFeedbackDto> GetTrainerAggregateFeedbackAsync(Guid trainerProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult(new Ethos.Api.Contracts.Feedback.TrainerAggregateFeedbackDto());

        public Task<IReadOnlyList<Ethos.Api.Contracts.Feedback.AdminTrainerFeedbackDto>> GetAdminTrainerFeedbackListAsync(Guid trainerProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Ethos.Api.Contracts.Feedback.AdminTrainerFeedbackDto>>(Array.Empty<Ethos.Api.Contracts.Feedback.AdminTrainerFeedbackDto>());
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private Workshop CreateFullyConfiguredWorkshop(Guid workshopId, Guid trainerProfileId)
    {
        var sessionId = Guid.NewGuid();
        var passId = Guid.NewGuid();

        var workshop = new Workshop
        {
            Id = workshopId,
            Title = "Urban Choreo Intensive",
            Description = "Master urban choreography and performance techniques.",
            DanceStyle = "Urban",
            Level = "Advanced",
            Venue = "Ethos Studio A",
            Price = 1200m,
            ImageUrl = "https://r2.ethosdance.com/workshops/portrait.jpg",
            LandscapeImageUrl = "https://r2.ethosdance.com/workshops/landscape.jpg",
            TrainerProfileId = trainerProfileId,
            Status = WorkshopStatus.Draft,
            WorkshopDate = DateTime.UtcNow.AddDays(7).Date,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(13),
            Capacity = 30
        };

        var faculty = new WorkshopTrainer
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            TrainerProfileId = trainerProfileId,
            DisplayOrder = 1
        };

        var session = new WorkshopSession
        {
            Id = sessionId,
            WorkshopId = workshopId,
            Title = "Urban Fundamentals",
            SessionDate = DateTime.UtcNow.AddDays(7).Date,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12),
            Capacity = 30,
            PosterImageUrl = "https://r2.ethosdance.com/sessions/poster-s1.jpg",
            IsActive = true
        };

        var sessionTrainer = new WorkshopSessionTrainer
        {
            Id = Guid.NewGuid(),
            WorkshopSessionId = sessionId,
            TrainerProfileId = trainerProfileId
        };
        session.SessionTrainers.Add(sessionTrainer);

        var pass = new WorkshopPassType
        {
            Id = passId,
            WorkshopId = workshopId,
            Name = "Single Session Pass",
            Price = 1200m,
            SessionsIncluded = 1,
            WorkshopSessionId = sessionId,
            IsActive = true
        };

        var tier1 = new WorkshopPricingTier
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            TierNumber = 1,
            TierName = "Early Bird",
            MinTickets = 1,
            MaxTickets = 10,
            Price = 1000m
        };

        var tier2 = new WorkshopPricingTier
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            TierNumber = 2,
            TierName = "Standard",
            MinTickets = 11,
            MaxTickets = null,
            Price = 1200m
        };

        workshop.WorkshopTrainers.Add(faculty);
        workshop.Sessions.Add(session);
        workshop.PassTypes.Add(pass);
        workshop.PricingTiers.Add(tier1);
        workshop.PricingTiers.Add(tier2);

        return workshop;
    }

    [Fact]
    public async Task Test01_PublishWorkshop_ValidatesConfiguration_TransitionsToPublished()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var r2 = new TrackingR2StorageService();
        var service = new AdminWorkshopService(db, audit, null, r2);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);

        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();

        await service.PublishWorkshopAsync(workshopId, adminId, CancellationToken.None);

        var updated = await db.Workshops.FindAsync(workshopId);
        Assert.NotNull(updated);
        Assert.Equal(WorkshopStatus.Published, updated.Status);
        Assert.Equal(1200m, updated.AdminApprovedPrice);
        Assert.NotNull(updated.PriceApprovedAt);
        Assert.Equal(adminId, updated.PriceApprovedByUserId);
        Assert.Contains(audit.LoggedActions, a => a.ActionType == "WORKSHOP_PUBLISHED" && a.EntityId == workshopId);
    }

    [Fact]
    public async Task Test02_PublishWorkshop_FailsWhen_NoActiveSessions()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var service = new AdminWorkshopService(db, audit);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);
        // Deactivate all sessions
        foreach (var s in workshop.Sessions) s.IsActive = false;

        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PublishWorkshopAsync(workshopId, adminId, CancellationToken.None));
        Assert.Contains("active session", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test03_PublishWorkshop_FailsWhen_MissingPricingTiersOrPasses()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var service = new AdminWorkshopService(db, audit);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);
        workshop.PricingTiers.Clear();

        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PublishWorkshopAsync(workshopId, adminId, CancellationToken.None));
        Assert.Contains("pricing tier", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test04_CompleteWorkshop_FailsWhen_SessionsInFuture_WithoutForceOverride()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var service = new AdminWorkshopService(db, audit);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);
        workshop.Status = WorkshopStatus.Published;
        // Session in the future
        workshop.Sessions.First().SessionDate = DateTime.UtcNow.AddDays(3).Date;
        workshop.Sessions.First().EndTime = TimeSpan.FromHours(18);

        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteWorkshopAsync(workshopId, adminId, forceComplete: false, overrideReason: null));
        Assert.Contains("future active sessions", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test05_CompleteWorkshop_SucceedsWhen_SessionsInFuture_WithValidForceOverride()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var r2 = new TrackingR2StorageService();
        var service = new AdminWorkshopService(db, audit, null, r2);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);
        workshop.Status = WorkshopStatus.Published;
        workshop.Sessions.First().SessionDate = DateTime.UtcNow.AddDays(3).Date;

        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();

        // Short override reason fails
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteWorkshopAsync(workshopId, adminId, forceComplete: true, overrideReason: "bad"));

        // Valid override reason succeeds
        await service.CompleteWorkshopAsync(workshopId, adminId, forceComplete: true, overrideReason: "Early emergency weather closure");

        var updated = await db.Workshops.FindAsync(workshopId);
        Assert.NotNull(updated);
        Assert.Equal(WorkshopStatus.Completed, updated.Status);
    }

    [Fact]
    public async Task Test06_CompleteWorkshop_FromPublished_SucceedsWhenConcluded()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var r2 = new TrackingR2StorageService();
        var service = new AdminWorkshopService(db, audit, null, r2);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);
        workshop.Status = WorkshopStatus.Published;
        workshop.Sessions.First().SessionDate = DateTime.UtcNow.AddDays(-2).Date;
        workshop.Sessions.First().EndTime = TimeSpan.FromHours(12);

        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();

        await service.CompleteWorkshopAsync(workshopId, adminId);

        var updated = await db.Workshops.FindAsync(workshopId);
        Assert.NotNull(updated);
        Assert.Equal(WorkshopStatus.Completed, updated.Status);
    }

    [Fact]
    public async Task Test07_CompleteWorkshop_PreservesPermanentPortrait_ImageUrl_InDbAndR2()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var r2 = new TrackingR2StorageService();
        var service = new AdminWorkshopService(db, audit, null, r2);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);
        workshop.Status = WorkshopStatus.Published;
        workshop.ImageUrl = "https://r2.ethosdance.com/workshops/permanent-portrait-key.jpg";
        workshop.Sessions.First().SessionDate = DateTime.UtcNow.AddDays(-1).Date;

        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();

        await service.CompleteWorkshopAsync(workshopId, adminId);

        var updated = await db.Workshops.FindAsync(workshopId);
        Assert.NotNull(updated);
        // Portrait in DB is strictly preserved
        Assert.Equal("https://r2.ethosdance.com/workshops/permanent-portrait-key.jpg", updated.ImageUrl);

        // Strict negative assertion: portrait key was NEVER passed to R2 DeleteAsync
        Assert.DoesNotContain("workshops/permanent-portrait-key.jpg", r2.DeletedKeys);
    }

    [Fact]
    public async Task Test08_CompleteWorkshop_ExactKeyMediaCleanup_LandscapeAndPostersOnly()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var r2 = new TrackingR2StorageService();
        var service = new AdminWorkshopService(db, audit, null, r2);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);
        workshop.Status = WorkshopStatus.Published;
        workshop.LandscapeImageUrl = "https://r2.ethosdance.com/workshops/ephemeral-landscape.jpg";
        workshop.Sessions.First().PosterImageUrl = "https://r2.ethosdance.com/sessions/ephemeral-poster.jpg";
        workshop.Sessions.First().SessionDate = DateTime.UtcNow.AddDays(-1).Date;

        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();

        await service.CompleteWorkshopAsync(workshopId, adminId);

        var updated = await db.Workshops.Include(w => w.Sessions).FirstAsync(w => w.Id == workshopId);
        // Ephemeral properties nullified in DB
        Assert.Null(updated.LandscapeImageUrl);
        Assert.Null(updated.Sessions.First().PosterImageUrl);

        // Exact keys submitted to R2 (no wildcards, no folder prefixes)
        Assert.Contains("workshops/ephemeral-landscape.jpg", r2.DeletedKeys);
        Assert.Contains("sessions/ephemeral-poster.jpg", r2.DeletedKeys);
        Assert.DoesNotContain("*", string.Join(" ", r2.DeletedKeys));
    }

    [Fact]
    public async Task Test09_CompleteWorkshop_PostCommitR2Failure_DoesNotRollbackDbCommit()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var r2 = new TrackingR2StorageService { ShouldThrowOnDelete = true };
        var service = new AdminWorkshopService(db, audit, null, r2);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);
        workshop.Status = WorkshopStatus.Published;
        workshop.Sessions.First().SessionDate = DateTime.UtcNow.AddDays(-1).Date;

        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();

        // Must NOT throw even if R2 network fails
        await service.CompleteWorkshopAsync(workshopId, adminId);

        var updated = await db.Workshops.FindAsync(workshopId);
        Assert.NotNull(updated);
        Assert.Equal(WorkshopStatus.Completed, updated.Status);
        Assert.Null(updated.LandscapeImageUrl);
        Assert.Contains(audit.LoggedActions, a => a.ActionType == "WORKSHOP_MEDIA_CLEANUP_FAILED");
    }

    [Fact]
    public async Task Test10_CompleteWorkshop_Idempotency_RerunIsSafe()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var r2 = new TrackingR2StorageService();
        var service = new AdminWorkshopService(db, audit, null, r2);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);
        workshop.Status = WorkshopStatus.Completed;

        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();

        // Idempotent rerun should complete with no-op
        await service.CompleteWorkshopAsync(workshopId, adminId);
        Assert.Empty(r2.DeletedKeys);
    }

    [Fact]
    public async Task Test11_LifecycleMatrix_CompletedIsTerminal_RejectsAnyTransition()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var service = new AdminWorkshopService(db, audit);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);
        workshop.Status = WorkshopStatus.Completed;

        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PublishWorkshopAsync(workshopId, adminId, CancellationToken.None));
        Assert.Contains("terminal state", ex1.Message, StringComparison.OrdinalIgnoreCase);

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UnpublishWorkshopAsync(workshopId, adminId, CancellationToken.None));
        Assert.Contains("terminal state", ex2.Message, StringComparison.OrdinalIgnoreCase);

        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ArchiveWorkshopAsync(workshopId, adminId, CancellationToken.None));
        Assert.Contains("terminal state", ex3.Message, StringComparison.OrdinalIgnoreCase);

        var ex4 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelWorkshopAsync(workshopId, adminId, "Cancellation attempt", CancellationToken.None));
        Assert.Contains("terminal state", ex4.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test12_LifecycleMatrix_InvalidTransition_ThrowsInvalidOperationException()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var service = new AdminWorkshopService(db, audit);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);
        workshop.Status = WorkshopStatus.Draft;

        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();

        // Draft cannot transition directly to Archived
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ArchiveWorkshopAsync(workshopId, adminId, CancellationToken.None));
        Assert.Contains("Invalid status transition", ex.Message);
    }

    [Fact]
    public async Task Test13_DeleteWorkshop_RejectsIfHasConfirmedBookings()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var service = new AdminWorkshopService(db, audit);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            StudentProfileId = Guid.NewGuid(),
            Status = WorkshopBookingStatus.Confirmed,
            TotalPrice = 1200m,
            Quantity = 1
        };

        db.Workshops.Add(workshop);
        db.WorkshopBookings.Add(booking);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteWorkshopAsync(workshopId, adminId));
        Assert.Contains("Cannot delete workshop with existing bookings", ex.Message);
        Assert.Contains(audit.LoggedActions, a => a.ActionType == "WORKSHOP_DELETE_REJECTED");
    }

    [Fact]
    public async Task Test14_DeleteWorkshop_RejectsIfHasCancelledOrRefundedBookings()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var service = new AdminWorkshopService(db, audit);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            StudentProfileId = Guid.NewGuid(),
            Status = WorkshopBookingStatus.Cancelled,
            TotalPrice = 1200m,
            Quantity = 1
        };

        db.Workshops.Add(workshop);
        db.WorkshopBookings.Add(booking);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteWorkshopAsync(workshopId, adminId));
        Assert.Contains("Cannot delete workshop with existing bookings", ex.Message);
    }

    [Fact]
    public async Task Test15_DeleteWorkshop_RejectsIfHasPaymentTransactions()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var service = new AdminWorkshopService(db, audit);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);

        var payment = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 1200m,
            Purpose = PaymentPurpose.WorkshopBooking,
            ReferenceId = workshopId,
            Status = PaymentStatus.Paid
        };

        db.Workshops.Add(workshop);
        db.PaymentTransactions.Add(payment);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteWorkshopAsync(workshopId, adminId));
        Assert.Contains("financial records", ex.Message);
    }

    [Fact]
    public async Task Test16_DeleteWorkshop_RejectsIfStatusIsCompleted()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var service = new AdminWorkshopService(db, audit);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);
        workshop.Status = WorkshopStatus.Completed;

        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteWorkshopAsync(workshopId, adminId));
        Assert.Contains("Cannot delete workshop with existing bookings", ex.Message);
        Assert.Contains(audit.LoggedActions, a => a.ActionType == "WORKSHOP_DELETE_REJECTED");
    }

    [Fact]
    public async Task Test17_DeleteWorkshop_UnbookedDraft_CascadesAllChildEntitiesAndAssociatedDraft()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var r2 = new TrackingR2StorageService();
        var service = new AdminWorkshopService(db, audit, null, r2);

        var adminId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var workshop = CreateFullyConfiguredWorkshop(workshopId, trainerId);
        workshop.Status = WorkshopStatus.Draft;

        var draft = new WorkshopDraft
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshopId,
            AdminUserId = adminId,
            DraftJson = "{}"
        };

        db.Workshops.Add(workshop);
        db.WorkshopDrafts.Add(draft);
        await db.SaveChangesAsync();

        await service.DeleteWorkshopAsync(workshopId, adminId);

        // Verify clean cascade
        Assert.Null(await db.Workshops.FindAsync(workshopId));
        Assert.Empty(await db.WorkshopSessions.Where(s => s.WorkshopId == workshopId).ToListAsync());
        Assert.Empty(await db.WorkshopTrainers.Where(t => t.WorkshopId == workshopId).ToListAsync());
        Assert.Empty(await db.WorkshopPassTypes.Where(p => p.WorkshopId == workshopId).ToListAsync());
        Assert.Empty(await db.WorkshopPricingTiers.Where(t => t.WorkshopId == workshopId).ToListAsync());
        Assert.Empty(await db.WorkshopDrafts.Where(d => d.WorkshopId == workshopId).ToListAsync());

        // Verify media cleanup
        Assert.Contains("workshops/portrait.jpg", r2.DeletedKeys);
        Assert.Contains("workshops/landscape.jpg", r2.DeletedKeys);
        Assert.Contains("sessions/poster-s1.jpg", r2.DeletedKeys);
        Assert.Contains(audit.LoggedActions, a => a.ActionType == "WORKSHOP_DELETED");
    }

    [Fact]
    public async Task Test18_AdminAuthorization_EnforcesWorkshopDeleteAndWorkshopUpdate()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var r2 = new TrackingR2StorageService();
        var service = new AdminWorkshopService(db, audit, null, r2);
        var authService = new MockAuthService();

        var workshopId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var controller = new AdminWorkshopsController(service, authService, new DummyGuestFeedbackService());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, adminId.ToString())
                }))
            }
        };

        var workshop = CreateFullyConfiguredWorkshop(workshopId, Guid.NewGuid());
        workshop.Status = WorkshopStatus.Draft;
        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();

        // 1. Unauthorized delete returns 403
        authService.AllowAll = false;
        var deleteForbidden = await controller.DeleteWorkshop(workshopId, CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(deleteForbidden);
        Assert.Equal(403, objResult.StatusCode);

        // 2. Unauthorized complete returns 403
        var completeForbidden = await controller.CompleteWorkshop(workshopId, null, CancellationToken.None);
        var objResult2 = Assert.IsType<ObjectResult>(completeForbidden);
        Assert.Equal(403, objResult2.StatusCode);

        // 3. Authorized delete returns 204
        authService.AllowAll = true;
        var deleteSuccess = await controller.DeleteWorkshop(workshopId, CancellationToken.None);
        Assert.IsType<NoContentResult>(deleteSuccess);
    }

    [Fact]
    public async Task Test19_GetWorkshopCountsAsync_CountsAllLifecycleStates_AndRespectsAdminDraftOwnership()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var r2 = new TrackingR2StorageService();
        var service = new AdminWorkshopService(db, audit, null, r2);

        var adminA = Guid.NewGuid();
        var adminB = Guid.NewGuid();

        // Standalone drafts (WorkshopId == null)
        db.WorkshopDrafts.Add(new WorkshopDraft { Id = Guid.NewGuid(), AdminUserId = adminA, WorkshopId = null, DraftJson = "{}", Version = 1 });
        db.WorkshopDrafts.Add(new WorkshopDraft { Id = Guid.NewGuid(), AdminUserId = adminB, WorkshopId = null, DraftJson = "{}", Version = 1 });
        db.WorkshopDrafts.Add(new WorkshopDraft { Id = Guid.NewGuid(), AdminUserId = adminB, WorkshopId = null, DraftJson = "{}", Version = 1 });

        // Add 1 workshop of each status:
        // 1. Draft
        var wDraft = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wDraft.Status = WorkshopStatus.Draft;
        db.Workshops.Add(wDraft);

        // 2. PendingApproval
        var wPending = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wPending.Status = WorkshopStatus.PendingApproval;
        db.Workshops.Add(wPending);

        // 3. Rejected
        var wRejected = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wRejected.Status = WorkshopStatus.Rejected;
        db.Workshops.Add(wRejected);

        // 4. Cancelled
        var wCancelled = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wCancelled.Status = WorkshopStatus.Cancelled;
        db.Workshops.Add(wCancelled);

        // 5. Completed
        var wCompleted = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wCompleted.Status = WorkshopStatus.Completed;
        db.Workshops.Add(wCompleted);

        // 6. Unpublished
        var wUnpublished = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wUnpublished.Status = WorkshopStatus.Unpublished;
        db.Workshops.Add(wUnpublished);

        // 7. Archived
        var wArchived = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wArchived.Status = WorkshopStatus.Archived;
        db.Workshops.Add(wArchived);

        // 8. Published (Upcoming)
        var wUpcoming = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wUpcoming.Status = WorkshopStatus.Published;
        wUpcoming.StartUtc = DateTime.UtcNow.AddDays(2);
        wUpcoming.EndUtc = DateTime.UtcNow.AddDays(2).AddHours(2);
        db.Workshops.Add(wUpcoming);

        // 9. Published (Ongoing)
        var wOngoing = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wOngoing.Status = WorkshopStatus.Published;
        wOngoing.StartUtc = DateTime.UtcNow.AddHours(-1);
        wOngoing.EndUtc = DateTime.UtcNow.AddHours(2);
        db.Workshops.Add(wOngoing);

        // 10. Published (Passed end time => derived completed in counts)
        var wEnded = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wEnded.Status = WorkshopStatus.Published;
        wEnded.StartUtc = DateTime.UtcNow.AddDays(-2);
        wEnded.EndUtc = DateTime.UtcNow.AddDays(-2).AddHours(2);
        db.Workshops.Add(wEnded);

        await db.SaveChangesAsync();

        // Scoped to Admin A: Draft should be 1 standalone draft + 1 materialized Draft = 2
        var countsA = await service.GetWorkshopCountsAsync(adminA, CancellationToken.None);
        Assert.Equal(2, countsA.Draft);
        Assert.Equal(1, countsA.PendingReview);
        Assert.Equal(1, countsA.Rejected);
        Assert.Equal(1, countsA.Cancelled);
        Assert.Equal(1, countsA.Unpublished);
        Assert.Equal(1, countsA.Archived);
        Assert.Equal(1, countsA.Upcoming);
        Assert.Equal(1, countsA.Ongoing);
        Assert.Equal(1, countsA.Ended); // 1 Ended Published
        Assert.Equal(1, countsA.Completed); // 1 strictly Completed
        Assert.Equal(10, countsA.All); // 10 Workshop table rows

        // Scoped to Admin B: Draft should be 2 standalone drafts + 1 materialized Draft = 3
        var countsB = await service.GetWorkshopCountsAsync(adminB, CancellationToken.None);
        Assert.Equal(3, countsB.Draft);
        Assert.Equal(1, countsB.Ended);
        Assert.Equal(1, countsB.Completed);
        Assert.Equal(10, countsB.All);
    }

    [Fact]
    public async Task Test20_GetWorkshopsAsync_PhaseFiltering_SupportsDraft_Rejected_AndPublishedOnly()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var r2 = new TrackingR2StorageService();
        var service = new AdminWorkshopService(db, audit, null, r2);

        var wDraft = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wDraft.Status = WorkshopStatus.Draft;
        db.Workshops.Add(wDraft);

        var wRejected = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wRejected.Status = WorkshopStatus.Rejected;
        db.Workshops.Add(wRejected);

        var wApprovedNotPublished = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wApprovedNotPublished.Status = WorkshopStatus.Approved;
        wApprovedNotPublished.StartUtc = DateTime.UtcNow.AddDays(5);
        wApprovedNotPublished.EndUtc = DateTime.UtcNow.AddDays(5).AddHours(2);
        db.Workshops.Add(wApprovedNotPublished);

        var wPublishedUpcoming = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wPublishedUpcoming.Status = WorkshopStatus.Published;
        wPublishedUpcoming.StartUtc = DateTime.UtcNow.AddDays(3);
        wPublishedUpcoming.EndUtc = DateTime.UtcNow.AddDays(3).AddHours(2);
        db.Workshops.Add(wPublishedUpcoming);

        var wPublishedEnded = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wPublishedEnded.Status = WorkshopStatus.Published;
        wPublishedEnded.StartUtc = DateTime.UtcNow.AddDays(-3);
        wPublishedEnded.EndUtc = DateTime.UtcNow.AddDays(-3).AddHours(2);
        db.Workshops.Add(wPublishedEnded);

        await db.SaveChangesAsync();

        // 1. phase = "draft"
        var draftRes = await service.GetWorkshopsAsync(1, 10, "draft", null, null, null, null, null, null, CancellationToken.None);
        Assert.Single(draftRes.Items);
        Assert.Equal(wDraft.Id, draftRes.Items[0].Id);

        // 2. phase = "rejected"
        var rejectedRes = await service.GetWorkshopsAsync(1, 10, "rejected", null, null, null, null, null, null, CancellationToken.None);
        Assert.Single(rejectedRes.Items);
        Assert.Equal(wRejected.Id, rejectedRes.Items[0].Id);

        // 3. phase = "upcoming" -> MUST only return wPublishedUpcoming, NOT wApprovedNotPublished
        var upcomingRes = await service.GetWorkshopsAsync(1, 10, "upcoming", null, null, null, null, null, null, CancellationToken.None);
        Assert.Single(upcomingRes.Items);
        Assert.Equal(wPublishedUpcoming.Id, upcomingRes.Items[0].Id);

        // 4. phase = "ended" -> returns wPublishedEnded
        var endedRes = await service.GetWorkshopsAsync(1, 10, "ended", null, null, null, null, null, null, CancellationToken.None);
        Assert.Single(endedRes.Items);
        Assert.Equal(wPublishedEnded.Id, endedRes.Items[0].Id);
    }

    [Fact]
    public void Test21_DeriveLifecyclePhase_DistinguishesCompleted_FromEndedPendingCompletion()
    {
        var nowUtc = DateTime.UtcNow;

        var wCompleted = new Workshop { Status = WorkshopStatus.Completed };
        Assert.Equal("Completed", AdminWorkshopService.DeriveLifecyclePhase(wCompleted, nowUtc));

        var wEndedPublished = new Workshop
        {
            Status = WorkshopStatus.Published,
            StartUtc = nowUtc.AddDays(-2),
            EndUtc = nowUtc.AddDays(-1)
        };
        Assert.Equal("Ended", AdminWorkshopService.DeriveLifecyclePhase(wEndedPublished, nowUtc));

        var wUpcoming = new Workshop
        {
            Status = WorkshopStatus.Published,
            StartUtc = nowUtc.AddDays(1),
            EndUtc = nowUtc.AddDays(2)
        };
        Assert.Equal("Upcoming", AdminWorkshopService.DeriveLifecyclePhase(wUpcoming, nowUtc));

        var wOngoing = new Workshop
        {
            Status = WorkshopStatus.Published,
            StartUtc = nowUtc.AddHours(-1),
            EndUtc = nowUtc.AddHours(2)
        };
        Assert.Equal("Ongoing", AdminWorkshopService.DeriveLifecyclePhase(wOngoing, nowUtc));

        var wRejected = new Workshop { Status = WorkshopStatus.Rejected };
        Assert.Equal("Rejected", AdminWorkshopService.DeriveLifecyclePhase(wRejected, nowUtc));
    }

    [Fact]
    public async Task Test22_PublishedWorkshop_NonUtcTimezone_NullableStartUtc_FallbackCorrectlyCalculatesPhase()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var r2 = new TrackingR2StorageService();
        var service = new AdminWorkshopService(db, audit, null, r2);

        // Workshop in Asia/Kolkata (+05:30)
        // Suppose workshop starts at tomorrow 10:00 AM IST.
        // In UTC, tomorrow 10:00 AM IST is tomorrow 04:30 AM UTC -> clearly in the future (Upcoming).
        var wFutureTz = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wFutureTz.Status = WorkshopStatus.Published;
        wFutureTz.Timezone = "Asia/Kolkata";
        wFutureTz.WorkshopDate = DateTime.UtcNow.AddDays(2).Date;
        wFutureTz.StartTime = new TimeSpan(10, 0, 0);
        wFutureTz.EndTime = new TimeSpan(12, 0, 0);
        wFutureTz.StartUtc = null; // Test fallback!
        wFutureTz.EndUtc = null;   // Test fallback!
        db.Workshops.Add(wFutureTz);

        // Workshop that ended yesterday in Asia/Kolkata
        var wPastTz = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        wPastTz.Status = WorkshopStatus.Published;
        wPastTz.Timezone = "Asia/Kolkata";
        wPastTz.WorkshopDate = DateTime.UtcNow.AddDays(-2).Date;
        wPastTz.StartTime = new TimeSpan(10, 0, 0);
        wPastTz.EndTime = new TimeSpan(12, 0, 0);
        wPastTz.StartUtc = null; // Test fallback!
        wPastTz.EndUtc = null;   // Test fallback!
        db.Workshops.Add(wPastTz);

        await db.SaveChangesAsync();

        // 1. Upcoming query must return only wFutureTz
        var upcoming = await service.GetWorkshopsAsync(1, 10, "upcoming", null, null, null, null, null, null, CancellationToken.None);
        Assert.Single(upcoming.Items);
        Assert.Equal(wFutureTz.Id, upcoming.Items[0].Id);

        // 2. Ended query must return only wPastTz
        var ended = await service.GetWorkshopsAsync(1, 10, "ended", null, null, null, null, null, null, CancellationToken.None);
        Assert.Single(ended.Items);
        Assert.Equal(wPastTz.Id, ended.Items[0].Id);
    }

    [Fact]
    public async Task Test23_ApprovedWorkshop_CanBePublishedFromCard()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var r2 = new TrackingR2StorageService();
        var service = new AdminWorkshopService(db, audit, null, r2);

        var adminId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var w = CreateFullyConfiguredWorkshop(workshopId, Guid.NewGuid());
        w.Status = WorkshopStatus.Approved;
        w.Price = 799m;
        w.ImageUrl = "https://r2.ethosdance.com/workshops/portrait.jpg";
        db.Workshops.Add(w);
        await db.SaveChangesAsync();

        var initialCount = await db.Workshops.CountAsync();

        // Call authoritative publish service (same as triggered by [Publish Workshop] button)
        await service.PublishWorkshopAsync(workshopId, adminId, CancellationToken.None);

        var updated = await db.Workshops.FindAsync(workshopId);
        Assert.NotNull(updated);
        Assert.Equal(WorkshopStatus.Published, updated.Status);
        Assert.NotNull(updated.StartUtc);
        Assert.NotNull(updated.EndUtc);

        // Invariant: no new row created, price preserved
        var finalCount = await db.Workshops.CountAsync();
        Assert.Equal(initialCount, finalCount);
        Assert.Equal(799m, updated.Price);

        // Invariant: publish validation cannot be bypassed (e.g. missing portrait throws)
        var invalidWorkshop = CreateFullyConfiguredWorkshop(Guid.NewGuid(), Guid.NewGuid());
        invalidWorkshop.Status = WorkshopStatus.Approved;
        invalidWorkshop.ImageUrl = null;
        db.Workshops.Add(invalidWorkshop);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PublishWorkshopAsync(invalidWorkshop.Id, adminId, CancellationToken.None));
    }

    [Fact]
    public async Task Test24_UnpublishedWorkshop_CanBeRepublishedFromCard()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var r2 = new TrackingR2StorageService();
        var service = new AdminWorkshopService(db, audit, null, r2);

        var adminId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();
        var w = CreateFullyConfiguredWorkshop(workshopId, Guid.NewGuid());
        w.Status = WorkshopStatus.Unpublished;
        w.Price = 899m;
        w.ImageUrl = "https://r2.ethosdance.com/workshops/portrait.jpg";
        db.Workshops.Add(w);
        await db.SaveChangesAsync();

        var initialCount = await db.Workshops.CountAsync();

        // Call authoritative publish service
        await service.PublishWorkshopAsync(workshopId, adminId, CancellationToken.None);

        var updated = await db.Workshops.FindAsync(workshopId);
        Assert.NotNull(updated);
        Assert.Equal(WorkshopStatus.Published, updated.Status);
        Assert.Equal(initialCount, await db.Workshops.CountAsync());
    }
}
