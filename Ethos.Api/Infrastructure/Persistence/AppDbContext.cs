using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();

    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();

    public DbSet<TrainerProfile> TrainerProfiles => Set<TrainerProfile>();

    public DbSet<TrainerApplication> TrainerApplications => Set<TrainerApplication>();

    public DbSet<TrainerApplicationVideo> TrainerApplicationVideos => Set<TrainerApplicationVideo>();

    public DbSet<TrainerGalleryImage> TrainerGalleryImages => Set<TrainerGalleryImage>();

    public DbSet<TrainerTier> TrainerTiers => Set<TrainerTier>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<TrainerTierPermission> TrainerTierPermissions => Set<TrainerTierPermission>();

    public DbSet<TrainerPermissionOverride> TrainerPermissionOverrides => Set<TrainerPermissionOverride>();

    public DbSet<TrainerTierHistory> TrainerTierHistories => Set<TrainerTierHistory>();

    public DbSet<TrainerAvailability> TrainerAvailabilities => Set<TrainerAvailability>();

    public DbSet<TrainerUpgradeRequest> TrainerUpgradeRequests => Set<TrainerUpgradeRequest>();

    public DbSet<TrainerPerformanceSnapshot> TrainerPerformanceSnapshots => Set<TrainerPerformanceSnapshot>();

    public DbSet<AdminAction> AdminActions => Set<AdminAction>();

    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();

    public DbSet<AdminDevice> AdminDevices => Set<AdminDevice>();

    public DbSet<AdminSession> AdminSessions => Set<AdminSession>();

    public DbSet<Package> Packages => Set<Package>();

    public DbSet<StudentPackage> StudentPackages => Set<StudentPackage>();

    public DbSet<DanceClass> DanceClasses => Set<DanceClass>();

    public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();

    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();

    public DbSet<ClassEnrollment> ClassEnrollments => Set<ClassEnrollment>();

    public DbSet<ClassFeedback> ClassFeedbacks => Set<ClassFeedback>();

    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    public DbSet<Workshop> Workshops => Set<Workshop>();

    public DbSet<WorkshopPricingTier> WorkshopPricingTiers => Set<WorkshopPricingTier>();

    public DbSet<WorkshopBooking> WorkshopBookings => Set<WorkshopBooking>();
 
    public DbSet<WorkshopTicket> WorkshopTickets => Set<WorkshopTicket>();

    public DbSet<WorkshopAttendance> WorkshopAttendances => Set<WorkshopAttendance>();

    public DbSet<WorkshopAttendanceEvent> WorkshopAttendanceEvents => Set<WorkshopAttendanceEvent>();


    public DbSet<WorkshopFeedback> WorkshopFeedbacks => Set<WorkshopFeedback>();

    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();

    public DbSet<ApiRequestLog> ApiRequestLogs => Set<ApiRequestLog>();

    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<IncidentUpdate> IncidentUpdates => Set<IncidentUpdate>();

    public DbSet<CorrectiveAction> CorrectiveActions => Set<CorrectiveAction>();

    public DbSet<CommunicationLog> CommunicationLogs => Set<CommunicationLog>();

    public DbSet<WorkshopFeedbackToken> WorkshopFeedbackTokens => Set<WorkshopFeedbackToken>();

    public DbSet<MediaItem> MediaItems => Set<MediaItem>();

    public DbSet<StudioVideo> StudioVideos => Set<StudioVideo>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Safe role invariant guard:
        // Inspects newly created (EntityState.Added) active User entities, ensuring
        // a corresponding UserRole is present in navigation or change tracker.
        var addedActiveUsers = ChangeTracker.Entries<User>()
            .Where(e => e.State == EntityState.Added && e.Entity.IsActive)
            .Select(e => e.Entity)
            .ToList();

        if (addedActiveUsers.Count > 0)
        {
            var trackedUserRoleUserIds = ChangeTracker.Entries<UserRole>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Unchanged)
                .Select(e => e.Entity.UserId)
                .ToHashSet();

            foreach (var user in addedActiveUsers)
            {
                var hasRoleInNav = user.UserRoles != null && user.UserRoles.Count > 0;
                var hasRoleInTracker = trackedUserRoleUserIds.Contains(user.Id);

                if (!hasRoleInNav && !hasRoleInTracker)
                {
                    throw new InvalidOperationException(
                        $"Cannot create an active Ethos account for '{user.FullName}' ({user.CustomerCode}) without at least one assigned role.");
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}

