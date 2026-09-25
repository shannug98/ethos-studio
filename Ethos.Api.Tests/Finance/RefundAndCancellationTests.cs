using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Finance;
using Ethos.Api.Application.Students;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Finance;

public class RefundAndCancellationTests
{
    private class DummyWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Ethos.Api";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private class DummyRefundService : IRefundService
    {
        public Task<RefundResult> RefundPaymentAsync(Guid paymentId, string reason, Guid adminUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(new RefundResult { Success = true, Status = RefundStatus.Processed });

        public Task<RefundResult> ProcessRefundJobAsync(Guid refundJobId, CancellationToken cancellationToken = default)
            => Task.FromResult(new RefundResult { Success = true, Status = RefundStatus.Processed });
    }

    private class DummyAuditService : IAdminAuditService
    {
        public readonly List<(string Action, Guid EntityId, string? Reason)> Logs = new();

        public void AddAuditLog(
            Guid adminUserId, string actionType, string entityType, Guid entityId,
            string? reason, string category = "OPERATIONS", Guid? adminDeviceId = null,
            Guid? adminSessionId = null, string? traceId = null, string? ipAddress = null,
            string? userAgent = null, string? metadataJson = null)
        {
            Logs.Add((actionType, entityId, reason));
        }

        public Task LogActionAsync(
            Guid adminUserId, string actionType, string category, string entityType,
            Guid entityId, bool success = true, string? outcomeCode = null, string? reason = null,
            Guid? adminDeviceId = null, Guid? adminSessionId = null, string? traceId = null,
            string? requestId = null, string? ipAddress = null, string? userAgent = null,
            object? metadata = null, CancellationToken cancellationToken = default)
        {
            Logs.Add((actionType, entityId, reason));
            return Task.CompletedTask;
        }

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

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task RefundPaymentAsync_SuccessfulRefund_UpdatesPaymentBookingTicketsAndQueuesWhatsApp()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var env = new DummyWebHostEnvironment();
        var rzpOptions = Options.Create(new RazorpaySettings { KeyId = "rzp_test_placeholder", KeySecret = "secret" });
        var service = new RefundService(db, rzpOptions, audit, env, NullLogger<RefundService>.Instance);

        var adminId = Guid.NewGuid();
        var payment = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 1500m,
            Currency = "INR",
            Status = PaymentStatus.Paid,
            RazorpayPaymentId = "pay_test_12345",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.PaymentTransactions.Add(payment);

        var role = new Role { Id = Guid.NewGuid(), Name = "STUDENT", Code = "STUDENT" };
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Test Dancer",
            Phone = "9876543210",
            Email = "dancer@example.com",
            CustomerCode = "C-001",
            IsActive = true
        };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });
        var student = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user
        };
        db.Roles.Add(role);
        db.Users.Add(user);
        db.StudentProfiles.Add(student);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = Guid.NewGuid(),
            StudentProfileId = student.Id,
            PaymentTransactionId = payment.Id,
            Quantity = 2,
            TotalPrice = 1500m,
            Status = WorkshopBookingStatus.Confirmed,
            GuestPhone = "9876543210",
            GuestName = "Test Dancer",
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);

        var ticket1 = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            TicketNumber = "TK-001",
            QrTokenHash = "hash1",
            WorkshopBookingId = booking.Id,
            WorkshopId = booking.WorkshopId,
            UserId = booking.StudentProfileId,
            PaymentTransactionId = payment.Id,
            AttendeeName = "Test Dancer",
            AttendeePhone = "9876543210",
            Status = TicketStatus.Issued
        };
        var ticket2 = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            TicketNumber = "TK-002",
            QrTokenHash = "hash2",
            WorkshopBookingId = booking.Id,
            WorkshopId = booking.WorkshopId,
            UserId = booking.StudentProfileId,
            PaymentTransactionId = payment.Id,
            AttendeeName = "Friend Dancer",
            AttendeePhone = "9876543210",
            Status = TicketStatus.Issued
        };
        db.WorkshopTickets.AddRange(ticket1, ticket2);
        await db.SaveChangesAsync();

        // Act
        var result = await service.RefundPaymentAsync(payment.Id, "Customer requested cancellation", adminId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(RefundStatus.Processed, result.Status);
        Assert.NotNull(result.RazorpayRefundId);

        // Verify entities
        var reloadedPayment = await db.PaymentTransactions.FindAsync(payment.Id);
        Assert.Equal(PaymentStatus.Refunded, reloadedPayment!.Status);

        var reloadedBooking = await db.WorkshopBookings.FindAsync(booking.Id);
        Assert.Equal(WorkshopBookingStatus.Cancelled, reloadedBooking!.Status);
        Assert.NotNull(reloadedBooking.CancelledAt);

        var tickets = await db.WorkshopTickets.Where(t => t.WorkshopBookingId == booking.Id).ToListAsync();
        Assert.All(tickets, t => Assert.Equal(TicketStatus.Refunded, t.Status));

        // Verify WhatsApp outbox
        var waNotification = await db.WhatsAppNotifications.FirstOrDefaultAsync(w => w.BookingId == booking.Id);
        Assert.NotNull(waNotification);
        Assert.Equal(WhatsAppNotificationType.BookingCancelled, waNotification!.NotificationType);
        Assert.Equal("9876543210", waNotification.RecipientPhone);

        // Verify audit log
        Assert.Contains(audit.Logs, l => l.Action == "ADMIN_BOOKING_REFUNDED");
    }

    [Fact]
    public async Task RefundPaymentAsync_IdempotentSecondCall_ReturnsExistingRefundWithoutDoubleExecution()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var env = new DummyWebHostEnvironment();
        var rzpOptions = Options.Create(new RazorpaySettings { KeyId = "rzp_test_placeholder", KeySecret = "secret" });
        var service = new RefundService(db, rzpOptions, audit, env, NullLogger<RefundService>.Instance);

        var adminId = Guid.NewGuid();
        var payment = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 1000m,
            Currency = "INR",
            Status = PaymentStatus.Paid,
            RazorpayPaymentId = "pay_test_idem_123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.PaymentTransactions.Add(payment);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = Guid.NewGuid(),
            StudentProfileId = Guid.NewGuid(),
            PaymentTransactionId = payment.Id,
            Quantity = 1,
            TotalPrice = 1000m,
            Status = WorkshopBookingStatus.Confirmed,
            GuestPhone = "9876543210",
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);
        await db.SaveChangesAsync();

        // First call
        var firstResult = await service.RefundPaymentAsync(payment.Id, "Reason 1", adminId);
        Assert.True(firstResult.Success);
        Assert.Equal(RefundStatus.Processed, firstResult.Status);

        // Second call (idempotent)
        var secondResult = await service.RefundPaymentAsync(payment.Id, "Reason 2", adminId);
        Assert.True(secondResult.Success);
        Assert.Equal(firstResult.RefundId, secondResult.RefundId);
        Assert.Equal(firstResult.RazorpayRefundId, secondResult.RazorpayRefundId);

        // Only one PaymentRefund should exist in DB
        var totalRefunds = await db.PaymentRefunds.CountAsync(r => r.PaymentId == payment.Id);
        Assert.Equal(1, totalRefunds);
    }

    [Fact]
    public void ValidateTicketTypePricingTiers_RejectsTierExceedingTotalCapacity()
    {
        var pricingService = new WorkshopPricingService(null!, null!);

        var invalidTiers = new List<AdminWorkshopPricingTierItem>
        {
            new() { TierNumber = 1, TierName = "Tier 1", MinTickets = 1, MaxTickets = 10, Price = 500 },
            new() { TierNumber = 2, TierName = "Tier 2", MinTickets = 11, MaxTickets = 144, Price = 700 } // Exceeds 50 capacity!
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            pricingService.ValidateTicketTypePricingTiers(50, invalidTiers));

        Assert.Contains("exceeds total ticket capacity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateTicketTypePricingTiers_AcceptsCorrectlyBoundedTiers()
    {
        var pricingService = new WorkshopPricingService(null!, null!);

        var validTiers = new List<AdminWorkshopPricingTierItem>
        {
            new() { TierNumber = 1, TierName = "Early Bird", MinTickets = 1, MaxTickets = 20, Price = 499 },
            new() { TierNumber = 2, TierName = "Regular", MinTickets = 21, MaxTickets = 50, Price = 699 }
        };

        // Should not throw
        pricingService.ValidateTicketTypePricingTiers(50, validTiers);
    }

    [Fact]
    public async Task CancelWorkshopAsync_CancelsWorkshopAndQueuesRefundJobsForPaidBookings()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var workshopService = new AdminWorkshopService(db, audit);

        var adminId = Guid.NewGuid();
        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Masterclass Weekend",
            DanceStyle = "Hip Hop",
            Level = "All Levels",
            WorkshopDate = DateTime.UtcNow.Date.AddDays(7),
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(14),
            Venue = "Studio A",
            Capacity = 30,
            Price = 1200m,
            Status = WorkshopStatus.Published,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Workshops.Add(workshop);

        var payment1 = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 1200m,
            Status = PaymentStatus.Paid,
            RazorpayPaymentId = "pay_ws_001"
        };
        var booking1 = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = Guid.NewGuid(),
            PaymentTransactionId = payment1.Id,
            Quantity = 1,
            TotalPrice = 1200m,
            Status = WorkshopBookingStatus.Confirmed,
            GuestPhone = "9999911111",
            BookedAt = DateTime.UtcNow
        };
        var ticket1 = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            TicketNumber = "TK-WS-1",
            QrTokenHash = "hash1",
            WorkshopBookingId = booking1.Id,
            WorkshopId = workshop.Id,
            UserId = booking1.StudentProfileId,
            PaymentTransactionId = payment1.Id,
            AttendeeName = "Dancer 1",
            Status = TicketStatus.Issued
        };

        db.PaymentTransactions.Add(payment1);
        db.WorkshopBookings.Add(booking1);
        db.WorkshopTickets.Add(ticket1);
        await db.SaveChangesAsync();

        // Act: Cancel workshop
        await workshopService.CancelWorkshopAsync(workshop.Id, adminId, "Instructor emergency", CancellationToken.None);

        // Assert
        var reloadedWorkshop = await db.Workshops.FindAsync(workshop.Id);
        Assert.Equal(WorkshopStatus.Cancelled, reloadedWorkshop!.Status);

        var reloadedBooking = await db.WorkshopBookings.FindAsync(booking1.Id);
        Assert.Equal(WorkshopBookingStatus.Cancelled, reloadedBooking!.Status);

        var reloadedTicket = await db.WorkshopTickets.FindAsync(ticket1.Id);
        Assert.Equal(TicketStatus.Cancelled, reloadedTicket!.Status);

        var refundJobs = await db.RefundJobs.Where(j => j.WorkshopId == workshop.Id).ToListAsync();
        Assert.Single(refundJobs);
        var job = refundJobs[0];
        Assert.Equal(booking1.Id, job.BookingId);
        Assert.Equal(payment1.Id, job.PaymentTransactionId);
        Assert.Equal(120000, job.AmountPaise); // 1200 INR = 120000 paise
        Assert.Equal(RefundStatus.Requested, job.Status);
    }

    [Fact]
    public async Task RefundOutboxDispatcher_RecoversAbandonedProcessingRefundsToReconciliationRequired()
    {
        using var db = CreateDbContext();
        var dummyRefundService = new DummyRefundService();
        var dispatcher = new RefundOutboxDispatcher(db, dummyRefundService, NullLogger<RefundOutboxDispatcher>.Instance);

        // Abandoned refund: Processing started 30 minutes ago
        var abandonedRefund = new PaymentRefund
        {
            Id = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            AmountPaise = 50000,
            Status = RefundStatus.Processing,
            ProcessingStartedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-30)
        };
        db.PaymentRefunds.Add(abandonedRefund);
        await db.SaveChangesAsync();

        // Act: Recover abandoned
        var recovered = await dispatcher.RecoverAbandonedRefundsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, recovered);
        var reloaded = await db.PaymentRefunds.FindAsync(abandonedRefund.Id);
        Assert.Equal(RefundStatus.ReconciliationRequired, reloaded!.Status);
        Assert.Contains("timed out in Processing state", reloaded.FailureReason);
    }

    [Fact]
    public async Task CreateWorkshopAsync_MultiSession_BypassesParentEndTimeValidationAndDerivesDates()
    {
        using var db = CreateDbContext();
        var audit = new DummyAuditService();
        var workshopService = new AdminWorkshopService(db, audit);

        var adminId = Guid.NewGuid();
        var date1 = DateTime.UtcNow.Date.AddDays(5);
        var date2 = DateTime.UtcNow.Date.AddDays(6);

        // Parent request has EndTime <= StartTime (e.g. Day 2 session ends earlier in the day than Day 1 starts)
        var request = new AdminCreateWorkshopRequest
        {
            Title = "2-Day Choreography Intensive",
            DanceStyle = "Contemporary",
            Level = "Intermediate",
            Venue = "Main Studio",
            WorkshopDate = date1,
            StartTime = TimeSpan.FromHours(18), // 6:00 PM
            EndTime = TimeSpan.FromHours(12),   // 12:00 PM (earlier time of day, but on Day 2!)
            Capacity = 40,
            Price = 2500m,
            Sessions = new List<Ethos.Api.Contracts.Workshops.AdminWorkshopSessionItem>
            {
                new()
                {
                    Title = "Day 1 Evening",
                    SessionDate = date1,
                    StartTime = TimeSpan.FromHours(18),
                    EndTime = TimeSpan.FromHours(21),
                    Capacity = 40
                },
                new()
                {
                    Title = "Day 2 Morning",
                    SessionDate = date2,
                    StartTime = TimeSpan.FromHours(9),
                    EndTime = TimeSpan.FromHours(12),
                    Capacity = 40
                }
            }
        };

        // Act: Should succeed without throwing "Workshop end time must be after start time."
        var response = await workshopService.CreateWorkshopAsync(adminId, request, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("2-Day Choreography Intensive", response.Title);
        Assert.Equal(2, response.Sessions.Count);

        var createdWorkshop = await db.Workshops.Include(w => w.Sessions).FirstOrDefaultAsync(w => w.Id == response.Id);
        Assert.NotNull(createdWorkshop);
        Assert.Equal(date1.Date, createdWorkshop!.WorkshopDate.Date);
        Assert.Equal(TimeSpan.FromHours(18), createdWorkshop.StartTime);
        Assert.Equal(TimeSpan.FromHours(12), createdWorkshop.EndTime);
    }
}
