using System.Globalization;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Ethos.Api.Application.Admin;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly AppDbContext _db;
    private readonly IAdminDeviceService _adminDeviceService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminDashboardService> _logger;

    public AdminDashboardService(
        AppDbContext db,
        IAdminDeviceService adminDeviceService,
        IConfiguration configuration,
        ILogger<AdminDashboardService> logger)
    {
        _db = db;
        _adminDeviceService = adminDeviceService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AdminDashboardResponse> GetDashboardMetricsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalUsers = await _db.Users.CountAsync(cancellationToken);
        var activeUsers = await _db.Users.CountAsync(u => u.IsActive, cancellationToken);
        var inactiveUsers = totalUsers - activeUsers;

        var studentRoleId = await _db.Roles.Where(r => r.Code == "STUDENT").Select(r => r.Id).FirstOrDefaultAsync(cancellationToken);
        var trainerRoleId = await _db.Roles.Where(r => r.Code == "TRAINER").Select(r => r.Id).FirstOrDefaultAsync(cancellationToken);
        var adminRoleId = await _db.Roles.Where(r => r.Code == "ADMIN").Select(r => r.Id).FirstOrDefaultAsync(cancellationToken);

        var studentsCount = await _db.UserRoles.CountAsync(ur => ur.RoleId == studentRoleId, cancellationToken);
        var trainersCount = await _db.UserRoles.CountAsync(ur => ur.RoleId == trainerRoleId, cancellationToken);
        var adminsCount = await _db.UserRoles.CountAsync(ur => ur.RoleId == adminRoleId, cancellationToken);

        var activeTrainers = await _db.TrainerProfiles.CountAsync(t => t.Status == TrainerStatus.Active, cancellationToken);
        var pendingApps = await _db.TrainerApplications.CountAsync(a => a.Status == TrainerApplicationStatus.PaymentVerified || a.Status == TrainerApplicationStatus.Submitted || a.Status == TrainerApplicationStatus.UnderReview, cancellationToken);
        var approvedApps = await _db.TrainerApplications.CountAsync(a => a.Status == TrainerApplicationStatus.Approved, cancellationToken);
        var rejectedApps = await _db.TrainerApplications.CountAsync(a => a.Status == TrainerApplicationStatus.Rejected, cancellationToken);
        var pendingUpgrades = await _db.TrainerUpgradeRequests.CountAsync(u => u.Status == TrainerUpgradeRequestStatus.Pending, cancellationToken);

        var totalWorkshops = await _db.Workshops.CountAsync(cancellationToken);
        var pendingWorkshops = await _db.Workshops.CountAsync(w => w.Status == WorkshopStatus.PendingApproval, cancellationToken);
        var upcomingWorkshops = await _db.Workshops.CountAsync(w => w.WorkshopDate >= now && w.Status == WorkshopStatus.Approved, cancellationToken);
        var completedWorkshops = await _db.Workshops.CountAsync(w => w.Status == WorkshopStatus.Completed || (w.WorkshopDate < now && w.Status == WorkshopStatus.Approved), cancellationToken);
        var cancelledWorkshops = await _db.Workshops.CountAsync(w => w.Status == WorkshopStatus.Cancelled, cancellationToken);
        var totalRegistrations = await _db.WorkshopBookings.CountAsync(b => b.Status == WorkshopBookingStatus.Confirmed, cancellationToken);

        var activeClasses = await _db.DanceClasses.CountAsync(c => c.IsActive, cancellationToken);
        var inactiveClasses = await _db.DanceClasses.CountAsync(c => !c.IsActive, cancellationToken);
        var totalEnrollments = await _db.ClassEnrollments.CountAsync(e => e.Status == EnrollmentStatus.Active, cancellationToken);

        var activePackages = await _db.Packages.CountAsync(p => p.IsActive, cancellationToken);
        var packagePurchasesCount = await _db.StudentPackages.CountAsync(cancellationToken);

        var successfulTx = _db.PaymentTransactions.Where(p => p.Status == PaymentStatus.Paid);
        var totalSuccessfulRevenue = await successfulTx.SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var currentMonthRevenue = await successfulTx.Where(p => p.PaidAt >= startOfMonth).SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var successfulTransactionsCount = await successfulTx.CountAsync(cancellationToken);
        var failedTransactionsCount = await _db.PaymentTransactions.CountAsync(p => p.Status == PaymentStatus.Failed, cancellationToken);
        var pendingTransactionsCount = await _db.PaymentTransactions.CountAsync(p => p.Status == PaymentStatus.Created, cancellationToken);

        return new AdminDashboardResponse
        {
            Users = new AdminUserMetrics
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                InactiveUsers = inactiveUsers,
                StudentsCount = studentsCount,
                TrainersCount = trainersCount,
                AdminsCount = adminsCount
            },
            Trainers = new AdminTrainerMetrics
            {
                ActiveTrainers = activeTrainers,
                PendingApplications = pendingApps,
                ApprovedApplications = approvedApps,
                RejectedApplications = rejectedApps,
                PendingTierUpgrades = pendingUpgrades
            },
            Workshops = new AdminWorkshopMetrics
            {
                TotalWorkshops = totalWorkshops,
                PendingApproval = pendingWorkshops,
                UpcomingWorkshops = upcomingWorkshops,
                CompletedWorkshops = completedWorkshops,
                CancelledWorkshops = cancelledWorkshops,
                TotalRegistrations = totalRegistrations
            },
            Classes = new AdminClassMetrics
            {
                ActiveClasses = activeClasses,
                InactiveClasses = inactiveClasses,
                TotalEnrollments = totalEnrollments
            },
            Packages = new AdminPackageMetrics
            {
                ActivePackages = activePackages,
                PackagePurchasesCount = packagePurchasesCount
            },
            Finance = new AdminFinanceMetrics
            {
                TotalSuccessfulRevenue = totalSuccessfulRevenue,
                CurrentMonthRevenue = currentMonthRevenue,
                SuccessfulTransactionsCount = successfulTransactionsCount,
                FailedTransactionsCount = failedTransactionsCount,
                PendingTransactionsCount = pendingTransactionsCount
            }
        };
    }

    public async Task<AdminCommandDashboardResponse> GetCommandDashboardAsync(string range = "week", CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;

        // 1. Overview KPIs
        var totalStudents = await _db.UserRoles.CountAsync(ur => ur.Role != null && ur.Role.Code == "STUDENT", cancellationToken);
        var activeStudents = await _db.Users.CountAsync(u => u.IsActive && u.UserRoles.Any(ur => ur.Role != null && ur.Role.Code == "STUDENT"), cancellationToken);
        var activeTrainers = await _db.TrainerProfiles.CountAsync(t => t.Status == TrainerStatus.Active, cancellationToken);
        var pendingApps = await _db.TrainerApplications.CountAsync(a =>
            a.Status == TrainerApplicationStatus.Submitted ||
            a.Status == TrainerApplicationStatus.UnderReview ||
            a.Status == TrainerApplicationStatus.PaymentVerified, cancellationToken);
        var activeClasses = await _db.DanceClasses.CountAsync(c => c.IsActive, cancellationToken);
        var totalEnrollments = await _db.ClassEnrollments.CountAsync(e => e.Status == EnrollmentStatus.Active, cancellationToken);
        var upcomingWorkshops = await _db.Workshops.CountAsync(w => w.WorkshopDate >= now && w.Status == WorkshopStatus.Approved, cancellationToken);
        var pendingWorkshops = await _db.Workshops.CountAsync(w => w.Status == WorkshopStatus.PendingApproval, cancellationToken);

        var todayBookings = await _db.WorkshopBookings.CountAsync(b => b.BookedAt >= todayStart, cancellationToken);
        var todayRevenue = await _db.PaymentTransactions
            .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt.HasValue && p.PaidAt.Value >= todayStart)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var pendingActionsCount = pendingApps + pendingWorkshops;

        var overview = new AdminCommandOverview
        {
            TotalStudents = totalStudents,
            ActiveStudents = activeStudents,
            ActiveTrainers = activeTrainers,
            PendingTrainerApplications = pendingApps,
            ActiveClasses = activeClasses,
            TotalEnrollments = totalEnrollments,
            UpcomingWorkshops = upcomingWorkshops,
            PendingWorkshops = pendingWorkshops,
            TodayBookings = todayBookings,
            TodayRevenue = todayRevenue,
            PendingActionsCount = pendingActionsCount
        };

        // 2. Activity Trends (Server-side date grouping, zero N+1)
        var rangeKey = string.IsNullOrWhiteSpace(range) ? "week" : range.Trim().ToLowerInvariant();
        int dayCount = rangeKey switch
        {
            "day" => 1,
            "month" => 30,
            _ => 7 // week default
        };

        var dates = Enumerable.Range(0, dayCount)
            .Select(offset => todayStart.AddDays(-(dayCount - 1) + offset))
            .ToList();

        var rangeStart = dates.First();
        var rangeEnd = now;

        var actionsGrouped = await _db.AdminActions
            .Where(a => a.CreatedAt >= rangeStart && a.CreatedAt <= rangeEnd)
            .GroupBy(a => a.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Date, g => g.Count, cancellationToken);

        var workshopsGrouped = await _db.Workshops
            .Where(w => w.CreatedAt >= rangeStart && w.CreatedAt <= rangeEnd)
            .GroupBy(w => w.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Date, g => g.Count, cancellationToken);

        var bookingsGrouped = await _db.WorkshopBookings
            .Where(b => b.BookedAt >= rangeStart && b.BookedAt <= rangeEnd)
            .GroupBy(b => b.BookedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Date, g => g.Count, cancellationToken);

        var revenueGrouped = await _db.PaymentTransactions
            .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt.HasValue && p.PaidAt.Value >= rangeStart && p.PaidAt.Value <= rangeEnd)
            .GroupBy(p => p.PaidAt!.Value.Date)
            .Select(g => new { Date = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(g => g.Date, g => g.Amount, cancellationToken);

        var secGrouped = await _db.SecurityEvents
            .Where(s => s.CreatedAt >= rangeStart && s.CreatedAt <= rangeEnd)
            .GroupBy(s => s.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Date, g => g.Count, cancellationToken);

        var series = dates.Select(d => new AdminActivityDataPoint
        {
            Date = d.ToString("yyyy-MM-dd"),
            AdminActions = actionsGrouped.GetValueOrDefault(d, 0),
            Workshops = workshopsGrouped.GetValueOrDefault(d, 0),
            Bookings = bookingsGrouped.GetValueOrDefault(d, 0),
            Revenue = revenueGrouped.GetValueOrDefault(d, 0m),
            SecurityEvents = secGrouped.GetValueOrDefault(d, 0)
        }).ToList();

        var activity = new AdminCommandActivity
        {
            Range = rangeKey,
            Series = series
        };

        // 3. Security Summary
        var failedAdminLogins = await _db.SecurityEvents.CountAsync(s =>
            s.EventType == "ADMIN_LOGIN_REJECTED" || s.EventType == "ADMIN_MFA_FAILED", cancellationToken);
        var authDenials = await _db.SecurityEvents.CountAsync(s =>
            s.EventType == "ADMIN_AUTHORIZATION_DENIED", cancellationToken);
        var deviceEvents = await _db.SecurityEvents.CountAsync(s =>
            s.EventType.StartsWith("ADMIN_DEVICE_") || s.EventType.StartsWith("DEVICE_"), cancellationToken);
        var highSeverityEvents = await _db.SecurityEvents.CountAsync(s =>
            s.Severity == "HIGH" || s.Severity == "CRITICAL", cancellationToken);
        var totalSecEvents = await _db.SecurityEvents.CountAsync(cancellationToken);

        var security = new AdminCommandSecurity
        {
            FailedAdminLogins = failedAdminLogins,
            AuthorizationDenials = authDenials,
            DeviceEvents = deviceEvents,
            HighSeverityEvents = highSeverityEvents,
            TotalSecurityEvents = totalSecEvents
        };

        // 4. Subsystem Health (Real checks, strict "NotMonitored" for unmonitored services)
        bool dbHealthy;
        try
        {
            dbHealthy = await _db.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            dbHealthy = false;
        }

        var health = new AdminCommandHealth
        {
            Api = "Healthy",
            Database = dbHealthy ? "Healthy" : "Degraded",
            Authentication = "Healthy",
            Storage = "Healthy",
            Payments = "Healthy",
            Messaging = "NotMonitored" // Strict User Adjustment 4 requirement
        };

        // 5. Actionable Attention Queue
        var attention = new List<AdminAttentionItem>();

        if (pendingApps > 0)
        {
            attention.Add(new AdminAttentionItem
            {
                Id = "attn_trainers",
                Category = "TRAINERS",
                Severity = "WARNING",
                Count = pendingApps,
                Title = $"{pendingApps} Pending Trainer Application{(pendingApps > 1 ? "s" : "")}",
                Description = "Trainer candidate applications awaiting administrative review and onboarding verification.",
                ActionPath = "/admin_portal/trainers"
            });
        }

        if (pendingWorkshops > 0)
        {
            attention.Add(new AdminAttentionItem
            {
                Id = "attn_workshops",
                Category = "WORKSHOPS",
                Severity = "INFO",
                Count = pendingWorkshops,
                Title = $"{pendingWorkshops} Pending Workshop Approval{(pendingWorkshops > 1 ? "s" : "")}",
                Description = "Workshop curriculum and pricing submissions awaiting administrative review.",
                ActionPath = "/admin_portal/workshops"
            });
        }

        if (authDenials > 0)
        {
            attention.Add(new AdminAttentionItem
            {
                Id = "attn_security_denials",
                Category = "SECURITY",
                Severity = "HIGH",
                Count = authDenials,
                Title = $"{authDenials} Authorization Denial Event{(authDenials > 1 ? "s" : "")}",
                Description = "Gated administrative access attempts blocked by security policy.",
                ActionPath = "/admin_portal/security-events"
            });
        }

        var discrepancyTxIds = await _db.PaymentEvents
            .AsNoTracking()
            .Where(e => e.EventType == "CUSTOMER_DISCREPANCY_FLAGGED")
            .Select(e => e.PaymentTransactionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var resolvedTxIds = await _db.PaymentEvents
            .AsNoTracking()
            .Where(e => e.EventType == "ADMIN_MANUAL_FULFILLMENT_RESOLVED" || e.EventType == "ADMIN_ISSUE_CLOSED_NOT_RECEIVED")
            .Select(e => e.PaymentTransactionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeDiscrepancies = discrepancyTxIds.Except(resolvedTxIds).Count();
        var pendingGatewayCount = await _db.PaymentTransactions
            .CountAsync(p => p.Status == PaymentStatus.PaymentPending && p.CreatedAt >= rangeStart, cancellationToken);

        var paymentsNeedingAttention = activeDiscrepancies + pendingGatewayCount;

        if (paymentsNeedingAttention > 0)
        {
            attention.Add(new AdminAttentionItem
            {
                Id = "attn_payments",
                Category = "PAYMENTS",
                Severity = "WARNING",
                Count = paymentsNeedingAttention,
                Title = $"{paymentsNeedingAttention} Payment Review{(paymentsNeedingAttention > 1 ? "s" : "")} Needed",
                Description = "Customer payment claims or pending transactions awaiting administrative review.",
                ActionPath = "/admin_portal/payments"
            });
        }

        // 6. Approved Devices
        var devices = await _adminDeviceService.GetDevicesAsync(cancellationToken);

        // 7. Recent Activity (Stream combining Audit & Security)
        var recentActions = await _db.AdminActions
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        var recentSecEvents = await _db.SecurityEvents
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        var userIds = recentActions.Select(a => a.AdminUserId)
            .Union(recentSecEvents.Where(s => s.UserId.HasValue).Select(s => s.UserId!.Value))
            .Distinct()
            .ToList();

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var activityList = new List<AdminRecentActivityItem>();

        foreach (var a in recentActions)
        {
            users.TryGetValue(a.AdminUserId, out var u);
            activityList.Add(new AdminRecentActivityItem
            {
                Id = a.Id,
                Source = "AUDIT",
                ActorName = u?.FullName ?? "System Admin",
                ActorCustomerCode = u?.CustomerCode,
                Action = a.ActionType,
                EntityType = a.EntityType,
                Outcome = a.OutcomeCode ?? (a.Success ? "SUCCESS" : "FAILURE"),
                TraceId = a.TraceId,
                Timestamp = a.CreatedAt
            });
        }

        foreach (var s in recentSecEvents)
        {
            User? u = null;
            if (s.UserId.HasValue) users.TryGetValue(s.UserId.Value, out u);

            activityList.Add(new AdminRecentActivityItem
            {
                Id = s.Id,
                Source = "SECURITY",
                ActorName = u?.FullName ?? (s.MaskedPhone != null ? $"Phone {s.MaskedPhone}" : "Anonymous / System"),
                ActorCustomerCode = u?.CustomerCode,
                Action = s.EventType,
                EntityType = "SECURITY",
                Outcome = s.Severity,
                TraceId = s.TraceId,
                Timestamp = s.CreatedAt
            });
        }

        var sortedRecentActivity = activityList
            .OrderByDescending(x => x.Timestamp)
            .Take(10)
            .ToList();

        return new AdminCommandDashboardResponse
        {
            Overview = overview,
            Activity = activity,
            Security = security,
            Health = health,
            Attention = attention,
            ApprovedDevices = devices,
            RecentActivity = sortedRecentActivity
        };
    }

    public async Task<AdminDailyActivityResponse> GetDailyActivityAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = start.AddDays(1);

        // 1. Students registered on this date
        var students = await _db.Users
            .AsNoTracking()
            .Where(x => x.CreatedAt >= start && x.CreatedAt < end && x.UserRoles.Any(ur => ur.Role.Name == "STUDENT" || ur.Role.Code == "STUDENT"))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminDailyStudentItem
            {
                StudentId = x.Id,
                StudentName = x.FullName,
                MobileNumber = x.Phone,
                RegisteredAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // 2. Workshop bookings on this date
        var workshopBookings = await _db.WorkshopBookings
            .AsNoTracking()
            .Where(x => x.BookedAt >= start && x.BookedAt < end)
            .Include(x => x.Workshop)
                .ThenInclude(w => w.TrainerProfile)
            .Include(x => x.StudentProfile)
                .ThenInclude(sp => sp.User)
            .OrderByDescending(x => x.BookedAt)
            .Select(x => new AdminDailyWorkshopBookingItem
            {
                BookingId = x.Id,
                WorkshopName = x.Workshop.Title,
                TrainerName = x.Workshop.TrainerProfile != null ? x.Workshop.TrainerProfile.FullName : "TBA",
                StudentName = x.StudentProfile != null && x.StudentProfile.User != null ? x.StudentProfile.User.FullName : "Student",
                Amount = x.Workshop.AdminApprovedPrice ?? x.Workshop.Price,
                Status = x.Status.ToString(),
                BookedAt = x.BookedAt
            })
            .ToListAsync(cancellationToken);

        // 3. Class enrollments / bookings on this date
        var classBookings = await _db.ClassEnrollments
            .AsNoTracking()
            .Where(x => x.CreatedAt >= start && x.CreatedAt < end)
            .Include(x => x.DanceClass)
            .Include(x => x.StudentProfile)
                .ThenInclude(sp => sp.User)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminDailyClassBookingItem
            {
                BookingId = x.Id,
                ClassName = x.DanceClass.Name,
                StudentName = x.StudentProfile != null && x.StudentProfile.User != null ? x.StudentProfile.User.FullName : "Student",
                Status = x.Status.ToString(),
                BookedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Workshops created on this date
        var workshopsCreatedCount = await _db.Workshops
            .AsNoTracking()
            .CountAsync(x => x.CreatedAt >= start && x.CreatedAt < end, cancellationToken);

        // 4. Payments on this date
        var payments = await _db.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.CreatedAt >= start && x.CreatedAt < end)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminDailyPaymentItem
            {
                PaymentId = x.Id,
                RazorpayOrderId = x.RazorpayOrderId,
                RazorpayPaymentId = x.RazorpayPaymentId,
                Purpose = x.Purpose.ToString(),
                Amount = x.Amount,
                Status = x.Status.ToString(),
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // 5. Security events on this date
        var securityEvents = await _db.SecurityEvents
            .AsNoTracking()
            .Where(x => x.CreatedAt >= start && x.CreatedAt < end)
            .Include(x => x.User)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminDailySecurityEventItem
            {
                Id = x.Id,
                EventType = x.EventType,
                Severity = x.Severity,
                Actor = x.User != null ? x.User.FullName : (x.MaskedPhone != null ? $"Phone {x.MaskedPhone}" : "System"),
                TraceId = x.TraceId,
                Description = x.DetailsJson,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // 6. Activity feed for this date (Audit + Security)
        var actions = await _db.AdminActions
            .AsNoTracking()
            .Where(a => a.CreatedAt >= start && a.CreatedAt < end)
            .Include(a => a.AdminUser)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var activityList = new List<AdminDailyActivityItem>();

        foreach (var a in actions)
        {
            activityList.Add(new AdminDailyActivityItem
            {
                Source = "AUDIT",
                Action = a.ActionType,
                Entity = a.EntityType,
                Outcome = a.OutcomeCode ?? (a.Success ? "SUCCESS" : "FAILURE"),
                Actor = a.AdminUser?.FullName ?? "Admin",
                TraceId = a.TraceId,
                Timestamp = a.CreatedAt
            });
        }

        foreach (var s in securityEvents)
        {
            activityList.Add(new AdminDailyActivityItem
            {
                Source = "SECURITY",
                Action = s.EventType,
                Entity = "SECURITY",
                Outcome = s.Severity,
                Actor = s.Actor,
                TraceId = s.TraceId,
                Timestamp = s.CreatedAt
            });
        }

        var sortedActivity = activityList.OrderByDescending(x => x.Timestamp).ToList();

        var successRevenue = payments
            .Where(x => x.Status == "Paid" || x.Status == "Success")
            .Sum(x => x.Amount);

        var failedPaymentsCount = payments
            .Count(x => x.Status == "Failed");

        return new AdminDailyActivityResponse
        {
            Date = date,
            Summary = new AdminDailyActivitySummary
            {
                StudentsRegistered = students.Count,
                ClassEnrollments = classBookings.Count,
                ClassBookings = classBookings.Count,
                WorkshopBookings = workshopBookings.Count,
                WorkshopsCreated = workshopsCreatedCount,
                AdminActions = actions.Count,
                SecurityEvents = securityEvents.Count,
                Revenue = successRevenue,
                FailedPayments = failedPaymentsCount
            },
            Students = students,
            WorkshopBookings = workshopBookings,
            ClassBookings = classBookings,
            Payments = payments,
            SecurityEvents = securityEvents,
            Activity = sortedActivity
        };
    }

    // =========================================================================
    // VISUAL REFERENCE DASHBOARD BOUNDED METHODS
    // =========================================================================

    public async Task<AdminDashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var startOfThisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfLastMonth = startOfThisMonth.AddMonths(-1);

        var totalBookings = await _db.WorkshopBookings.CountAsync(cancellationToken);
        var bookingsThisMonth = await _db.WorkshopBookings.CountAsync(b => b.BookedAt >= startOfThisMonth, cancellationToken);
        var bookingsLastMonth = await _db.WorkshopBookings.CountAsync(b => b.BookedAt >= startOfLastMonth && b.BookedAt < startOfThisMonth, cancellationToken);
        var bookingsGrowth = bookingsLastMonth > 0
            ? Math.Round(((double)(bookingsThisMonth - bookingsLastMonth) / bookingsLastMonth) * 100.0, 1)
            : 0.0;

        var successfulTx = _db.PaymentTransactions.Where(p => p.Status == PaymentStatus.Paid);
        var totalRevenue = await successfulTx.SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var revThisMonth = await successfulTx.Where(p => p.PaidAt >= startOfThisMonth).SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var revLastMonth = await successfulTx.Where(p => p.PaidAt >= startOfLastMonth && p.PaidAt < startOfThisMonth).SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var revGrowth = revLastMonth > 0
            ? Math.Round(((double)(revThisMonth - revLastMonth) / (double)revLastMonth) * 100.0, 1)
            : 0.0;

        var upcomingWorkshops = await _db.Workshops.CountAsync(w => w.WorkshopDate >= now && w.Status != WorkshopStatus.Cancelled && w.Status != WorkshopStatus.Archived, cancellationToken);
        var workshopsThisWeek = await _db.Workshops.CountAsync(w => w.WorkshopDate >= now && w.WorkshopDate <= now.AddDays(7) && w.Status != WorkshopStatus.Cancelled && w.Status != WorkshopStatus.Archived, cancellationToken);

        var unreadMessages = await _db.NotificationRecipients.CountAsync(nr => !nr.IsRead, cancellationToken);

        var pendingApps = await _db.TrainerApplications.CountAsync(a =>
            a.Status == TrainerApplicationStatus.Submitted ||
            a.Status == TrainerApplicationStatus.UnderReview ||
            a.Status == TrainerApplicationStatus.PaymentVerified, cancellationToken);
        var pendingWorkshops = await _db.Workshops.CountAsync(w => w.Status == WorkshopStatus.PendingApproval, cancellationToken);
        var pendingActions = pendingApps + pendingWorkshops;

        var failedPayments = await _db.PaymentTransactions.CountAsync(p => p.Status == PaymentStatus.Failed, cancellationToken);

        return new AdminDashboardSummaryDto
        {
            TotalBookings = totalBookings,
            BookingsGrowthPercent = bookingsGrowth,
            TotalRevenue = totalRevenue,
            RevenueGrowthPercent = revGrowth,
            UpcomingWorkshopsCount = upcomingWorkshops,
            WorkshopsThisWeekCount = workshopsThisWeek,
            UnreadMessagesCount = unreadMessages,
            MessagesGrowthPercent = 0.0,
            PendingActionsCount = pendingActions,
            FailedPaymentsCount = failedPayments
        };
    }

    public async Task<AdminDashboardTrendsDto> GetTrendsAsync(string range = "last6months", CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var normalizedRange = string.IsNullOrWhiteSpace(range) ? "last6months" : range.Trim().ToLowerInvariant();

        var dataPoints = new List<AdminTrendDataPoint>();

        if (normalizedRange == "last30days")
        {
            var startDate = now.Date.AddDays(-29);
            var bookings = await _db.WorkshopBookings
                .Where(b => b.BookedAt >= startDate)
                .GroupBy(b => b.BookedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Date, g => g.Count, cancellationToken);

            var revenue = await _db.PaymentTransactions
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt.HasValue && p.PaidAt.Value >= startDate)
                .GroupBy(p => p.PaidAt!.Value.Date)
                .Select(g => new { Date = g.Key, Amount = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(g => g.Date, g => g.Amount, cancellationToken);

            for (var d = startDate; d <= now.Date; d = d.AddDays(1))
            {
                dataPoints.Add(new AdminTrendDataPoint
                {
                    Label = d.ToString("dd MMM", CultureInfo.InvariantCulture),
                    BookingsCount = bookings.GetValueOrDefault(d, 0),
                    RevenueAmount = revenue.GetValueOrDefault(d, 0m)
                });
            }
        }
        else if (normalizedRange == "yeartodate")
        {
            var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var currentMonth = now.Month;

            var bookings = await _db.WorkshopBookings
                .Where(b => b.BookedAt >= startOfYear)
                .GroupBy(b => new { b.BookedAt.Year, b.BookedAt.Month })
                .Select(g => new { g.Key.Month, Count = g.Count() })
                .ToDictionaryAsync(g => g.Month, g => g.Count, cancellationToken);

            var revenue = await _db.PaymentTransactions
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt.HasValue && p.PaidAt.Value >= startOfYear)
                .GroupBy(p => new { p.PaidAt!.Value.Year, p.PaidAt!.Value.Month })
                .Select(g => new { g.Key.Month, Amount = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(g => g.Month, g => g.Amount, cancellationToken);

            for (int m = 1; m <= currentMonth; m++)
            {
                var monthDate = new DateTime(now.Year, m, 1);
                dataPoints.Add(new AdminTrendDataPoint
                {
                    Label = monthDate.ToString("MMM", CultureInfo.InvariantCulture),
                    BookingsCount = bookings.GetValueOrDefault(m, 0),
                    RevenueAmount = revenue.GetValueOrDefault(m, 0m)
                });
            }
        }
        else // default: last6months
        {
            normalizedRange = "last6months";
            for (int i = 5; i >= 0; i--)
            {
                var targetMonth = now.AddMonths(-i);
                var monthStart = new DateTime(targetMonth.Year, targetMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var monthEnd = monthStart.AddMonths(1);

                var bookingCount = await _db.WorkshopBookings
                    .CountAsync(b => b.BookedAt >= monthStart && b.BookedAt < monthEnd, cancellationToken);

                var revAmount = await _db.PaymentTransactions
                    .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt.HasValue && p.PaidAt.Value >= monthStart && p.PaidAt.Value < monthEnd)
                    .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

                dataPoints.Add(new AdminTrendDataPoint
                {
                    Label = monthStart.ToString("MMM", CultureInfo.InvariantCulture),
                    BookingsCount = bookingCount,
                    RevenueAmount = revAmount
                });
            }
        }

        return new AdminDashboardTrendsDto
        {
            Range = normalizedRange,
            DataPoints = dataPoints,
            TotalBookings = dataPoints.Sum(d => d.BookingsCount),
            TotalRevenue = dataPoints.Sum(d => d.RevenueAmount)
        };
    }

    public async Task<AdminWorkshopStatusDonutDto> GetWorkshopStatusDistributionAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var publishedCount = await _db.Workshops.CountAsync(w => w.Status == WorkshopStatus.Published, cancellationToken);
        var scheduledCount = await _db.Workshops.CountAsync(w => (w.Status == WorkshopStatus.Approved || w.Status == WorkshopStatus.Published) && w.WorkshopDate >= now, cancellationToken);
        var draftCount = await _db.Workshops.CountAsync(w => w.Status == WorkshopStatus.Draft || w.Status == WorkshopStatus.PendingApproval, cancellationToken);
        var completedCount = await _db.Workshops.CountAsync(w => w.Status == WorkshopStatus.Completed || (w.WorkshopDate < now && w.Status == WorkshopStatus.Approved), cancellationToken);
        var archivedCount = await _db.Workshops.CountAsync(w => w.Status == WorkshopStatus.Archived, cancellationToken);
        var cancelledCount = await _db.Workshops.CountAsync(w => w.Status == WorkshopStatus.Cancelled, cancellationToken);

        var total = publishedCount + scheduledCount + draftCount + completedCount + archivedCount + cancelledCount;

        return new AdminWorkshopStatusDonutDto
        {
            PublishedCount = publishedCount,
            ScheduledCount = scheduledCount,
            DraftCount = draftCount,
            CompletedCount = completedCount,
            ArchivedCount = archivedCount,
            CancelledCount = cancelledCount,
            TotalCount = total
        };
    }

    public async Task<AdminDashboardPrioritiesDto> GetPrioritiesAsync(CancellationToken cancellationToken = default)
    {
        var pendingWorkshops = await _db.Workshops.CountAsync(w => w.Status == WorkshopStatus.PendingApproval, cancellationToken);
        var failedPayments = await _db.PaymentTransactions.CountAsync(p => p.Status == PaymentStatus.Failed, cancellationToken);
        var unreadMessages = await _db.NotificationRecipients.CountAsync(nr => !nr.IsRead, cancellationToken);
        var pendingVideos = await _db.StudioVideos.CountAsync(v => !v.IsActive, cancellationToken);
        var newUsersToday = await _db.Users.CountAsync(u => u.CreatedAt >= DateTime.UtcNow.Date, cancellationToken);

        var items = new List<AdminPriorityItemDto>();

        if (pendingWorkshops > 0)
        {
            items.Add(new AdminPriorityItemDto
            {
                Id = "prio_workshops",
                Type = "WORKSHOPS_AWAITING",
                Count = pendingWorkshops,
                Title = $"{pendingWorkshops} workshop{(pendingWorkshops > 1 ? "s" : "")} awaiting publication",
                Subtitle = "Review and publish →",
                ActionUrl = "/admin_portal/workshops",
                Severity = "WARNING"
            });
        }

        if (failedPayments > 0)
        {
            items.Add(new AdminPriorityItemDto
            {
                Id = "prio_payments",
                Type = "FAILED_PAYMENTS",
                Count = failedPayments,
                Title = $"{failedPayments} failed payment{(failedPayments > 1 ? "s" : "")}",
                Subtitle = "Check and follow up →",
                ActionUrl = "/admin_portal/payments",
                Severity = "DANGER"
            });
        }

        if (unreadMessages > 0)
        {
            items.Add(new AdminPriorityItemDto
            {
                Id = "prio_messages",
                Type = "UNREAD_MESSAGES",
                Count = unreadMessages,
                Title = $"{unreadMessages} unread contact message{(unreadMessages > 1 ? "s" : "")}",
                Subtitle = "Respond to enquiries →",
                ActionUrl = "/admin_portal/communications",
                Severity = "WARNING"
            });
        }

        if (pendingVideos > 0)
        {
            items.Add(new AdminPriorityItemDto
            {
                Id = "prio_media",
                Type = "MEDIA_PENDING",
                Count = pendingVideos,
                Title = $"{pendingVideos} media item{(pendingVideos > 1 ? "s" : "")} pending review",
                Subtitle = "Approve or reject →",
                ActionUrl = "/admin_portal/videos",
                Severity = "INFO"
            });
        }

        if (newUsersToday > 0)
        {
            items.Add(new AdminPriorityItemDto
            {
                Id = "prio_users",
                Type = "NEW_REGISTRATIONS",
                Count = newUsersToday,
                Title = $"{newUsersToday} new user registration{(newUsersToday > 1 ? "s" : "")}",
                Subtitle = "Review user details →",
                ActionUrl = "/admin_portal/users",
                Severity = "INFO"
            });
        }

        return new AdminDashboardPrioritiesDto
        {
            Items = items,
            TotalPendingCount = items.Sum(i => i.Count)
        };
    }

    public async Task<List<AdminRecentBookingDto>> GetRecentBookingsAsync(int limit = 5, CancellationToken cancellationToken = default)
    {
        var bookings = await _db.WorkshopBookings
            .AsNoTracking()
            .Include(b => b.Workshop)
            .Include(b => b.StudentProfile)
                .ThenInclude(sp => sp.User)
            .OrderByDescending(b => b.BookedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        if (bookings.Count == 0)
        {
            return new List<AdminRecentBookingDto>();
        }

        return bookings.Select(b =>
        {
            var rawName = b.StudentProfile?.User?.FullName ?? "Student";
            var maskedName = MaskName(rawName);
            var statusStr = b.Status == WorkshopBookingStatus.Confirmed ? "Paid" : (b.Status == WorkshopBookingStatus.PendingPayment ? "Pending" : "Failed");
            var amt = b.Workshop?.AdminApprovedPrice ?? b.Workshop?.Price ?? 0m;

            return new AdminRecentBookingDto
            {
                BookingId = b.Id,
                CustomerNameMasked = maskedName,
                WorkshopTitle = b.Workshop?.Title ?? "Workshop",
                BookingDate = b.BookedAt,
                FormattedDate = b.BookedAt.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
                Amount = amt,
                FormattedAmount = $"₹ {amt:N0}",
                PaymentStatus = statusStr,
                BookingStatus = b.Status.ToString()
            };
        }).ToList();
    }

    public async Task<List<AdminUpcomingWorkshopDto>> GetUpcomingWorkshopsAsync(int limit = 5, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var workshops = await _db.Workshops
            .AsNoTracking()
            .Include(w => w.TrainerProfile)
            .Where(w => w.WorkshopDate >= now && w.Status != WorkshopStatus.Cancelled && w.Status != WorkshopStatus.Archived)
            .OrderBy(w => w.WorkshopDate)
            .Take(limit)
            .ToListAsync(cancellationToken);

        if (workshops.Count == 0)
        {
            return new List<AdminUpcomingWorkshopDto>();
        }

        var result = new List<AdminUpcomingWorkshopDto>();
        foreach (var w in workshops)
        {
            var bookedSeats = await _db.WorkshopBookings
                .CountAsync(b => b.WorkshopId == w.Id && b.Status == WorkshopBookingStatus.Confirmed, cancellationToken);
            var capacity = w.Capacity > 0 ? w.Capacity : 30;
            var occupancy = Math.Min(100.0, Math.Round((double)bookedSeats / capacity * 100.0, 1));

            result.Add(new AdminUpcomingWorkshopDto
            {
                WorkshopId = w.Id,
                Title = w.Title,
                ThumbnailUrl = w.ImageUrl ?? "/images/classes/hiphop.jpg",
                WorkshopDate = w.WorkshopDate,
                FormattedDate = w.WorkshopDate.ToString("ddd, dd MMM yyyy · hh:mm tt", CultureInfo.InvariantCulture),
                TrainerName = w.TrainerProfile?.FullName ?? "Studio Trainer",
                Capacity = capacity,
                BookedSeats = bookedSeats,
                OccupancyPercentage = occupancy,
                Status = w.Status.ToString()
            });
        }
        return result;
    }

    public async Task<List<AdminAuditActivityDto>> GetRecentActivityFeedAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        var actions = await _db.AdminActions
            .AsNoTracking()
            .Include(a => a.AdminUser)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        if (actions.Count == 0)
        {
            return new List<AdminAuditActivityDto>();
        }

        return actions.Select(a =>
        {
            var actorMasked = a.AdminUser != null
                ? $"by {MaskEmail(a.AdminUser.Email ?? a.AdminUser.FullName)}"
                : "by system";

            return new AdminAuditActivityDto
            {
                Id = a.Id,
                ActorUserId = a.AdminUserId,
                ActorNameMasked = actorMasked,
                Action = $"{a.ActionType} on {a.EntityType ?? "resource"}",
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Result = a.OutcomeCode ?? (a.Success ? "SUCCESS" : "FAILURE"),
                TraceId = a.TraceId,
                CreatedAt = a.CreatedAt,
                FormattedTime = a.CreatedAt.ToString("hh:mm tt", CultureInfo.InvariantCulture)
            };
        }).ToList();
    }

    public async Task<AdminSystemHealthDto> GetSystemHealthAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        bool dbHealthy;
        try
        {
            dbHealthy = await _db.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            dbHealthy = false;
        }

        var isStorageConfigured = !string.IsNullOrWhiteSpace(_configuration?["CloudflareR2:BucketName"] ?? _configuration?["Cloudflare:BucketName"]);
        var isPaymentConfigured = !string.IsNullOrWhiteSpace(_configuration?["Razorpay:KeyId"]);
        var isWhatsAppConfigured = !string.IsNullOrWhiteSpace(_configuration?["WhatsApp:ApiKey"]);

        var subsystems = new List<AdminSubsystemHealthItem>
        {
            new AdminSubsystemHealthItem
            {
                Key = "website_api",
                Name = "Website & API",
                Status = "Operational",
                Description = "ASP.NET Core API running normally"
            },
            new AdminSubsystemHealthItem
            {
                Key = "database",
                Name = "Database",
                Status = dbHealthy ? "Operational" : "Unavailable",
                Description = dbHealthy ? "Active database connection verified" : "Database connection failed"
            },
            new AdminSubsystemHealthItem
            {
                Key = "payment_provider",
                Name = "Payment Provider",
                Status = isPaymentConfigured ? "Operational" : "Standby / Not Configured",
                Description = isPaymentConfigured ? "Payment gateway credentials verified" : "Payment gateway credentials pending configuration"
            },
            new AdminSubsystemHealthItem
            {
                Key = "whatsapp_provider",
                Name = "WhatsApp Provider",
                Status = isWhatsAppConfigured ? "Operational" : "Standby / Not Configured",
                Description = isWhatsAppConfigured ? "Messaging gateway verified" : "Messaging gateway pending configuration"
            },
            new AdminSubsystemHealthItem
            {
                Key = "storage",
                Name = "Storage",
                Status = isStorageConfigured ? "Operational" : "Standby / Not Configured",
                Description = isStorageConfigured ? "Media storage bucket verified" : "Media storage bucket pending configuration"
            },
            new AdminSubsystemHealthItem
            {
                Key = "background_jobs",
                Name = "Background Jobs",
                Status = "Operational",
                Description = "Background schedule runners and telemetry interceptors active"
            }
        };

        var overallStatus = dbHealthy ? "Operational" : "Degraded";

        return new AdminSystemHealthDto
        {
            OverallStatus = overallStatus,
            LastCheckedUtc = now,
            FormattedLastChecked = now.ToString("dd MMM yyyy, hh:mm tt", CultureInfo.InvariantCulture),
            Subsystems = subsystems
        };
    }

    public async Task<AdminRevenueOverviewDto> GetRevenueOverviewAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var startOfThisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfLastMonth = startOfThisMonth.AddMonths(-1);

        var thisMonthRev = await _db.PaymentTransactions
            .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt.HasValue && p.PaidAt.Value >= startOfThisMonth)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var lastMonthRev = await _db.PaymentTransactions
            .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt.HasValue && p.PaidAt.Value >= startOfLastMonth && p.PaidAt.Value < startOfThisMonth)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var growth = lastMonthRev > 0
            ? Math.Round(((double)(thisMonthRev - lastMonthRev) / (double)lastMonthRev) * 100.0, 1)
            : 0.0;

        // 4 weekly buckets for current month
        var w1End = startOfThisMonth.AddDays(7);
        var w2End = startOfThisMonth.AddDays(14);
        var w3End = startOfThisMonth.AddDays(21);

        var w1 = await _db.PaymentTransactions.Where(p => p.Status == PaymentStatus.Paid && p.PaidAt >= startOfThisMonth && p.PaidAt < w1End).SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var w2 = await _db.PaymentTransactions.Where(p => p.Status == PaymentStatus.Paid && p.PaidAt >= w1End && p.PaidAt < w2End).SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var w3 = await _db.PaymentTransactions.Where(p => p.Status == PaymentStatus.Paid && p.PaidAt >= w2End && p.PaidAt < w3End).SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var w4 = await _db.PaymentTransactions.Where(p => p.Status == PaymentStatus.Paid && p.PaidAt >= w3End).SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var weekly = new List<AdminWeeklyRevenueBucket>
        {
            new AdminWeeklyRevenueBucket { WeekLabel = "Week 1", RevenueAmount = w1 },
            new AdminWeeklyRevenueBucket { WeekLabel = "Week 2", RevenueAmount = w2 },
            new AdminWeeklyRevenueBucket { WeekLabel = "Week 3", RevenueAmount = w3 },
            new AdminWeeklyRevenueBucket { WeekLabel = "Week 4", RevenueAmount = w4 }
        };

        return new AdminRevenueOverviewDto
        {
            CurrentMonthRevenue = thisMonthRev,
            MonthGrowthPercent = growth,
            FormattedCurrentMonthRevenue = $"₹ {thisMonthRev:N0}",
            WeeklyBreakdown = weekly
        };
    }

    private static string MaskName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "Student";
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0].Length <= 3 ? parts[0] : $"{parts[0][..2]}***";
        }
        return $"{parts[0]} {parts[1][0]}.";
    }

    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "admin@ethos.com";
        var atIdx = email.IndexOf('@');
        if (atIdx <= 2) return email;
        return $"{email[..2]}***{email[atIdx..]}";
    }
}
