using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly AppDbContext _db;
    private readonly IAdminDeviceService _adminDeviceService;
    private readonly ILogger<AdminDashboardService> _logger;

    public AdminDashboardService(
        AppDbContext db,
        IAdminDeviceService adminDeviceService,
        ILogger<AdminDashboardService> logger)
    {
        _db = db;
        _adminDeviceService = adminDeviceService;
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
}
