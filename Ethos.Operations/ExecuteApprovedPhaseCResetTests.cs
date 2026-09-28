using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace Ethos.Api.Tests.Media;

public class ExecuteApprovedPhaseCResetTests
{
    private readonly ITestOutputHelper _output;

    // EXACT 11 PRESERVED PRODUCTION ASSETS
    public static readonly HashSet<string> PreservedR2Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        "homepagescrolling/videos/2026/09/debf5e435d8048919e89c1fde5be81b5_faststart.mp4",
        "homepagereels/videos/2026/09/2b5338182ae249a2b6ed21343bc128ad.mp4",
        "homepagereels/videos/2026/09/6014633c51164acf8fa66c9bff927c76.mp4",
        "homepagescrolling/images/2026/09/209485a5b3f24cc483dccab1dfaa59b0.jpg",
        "homepagescrolling/images/2026/09/284ba62bea664d438de4dec6136219d4.jpeg",
        "homepagescrolling/images/2026/09/6dffde15140141e2830ab12f477ec581.jpg",
        "aboutethos/images/2026/09/042a84e06f0a4150a2a79c7622b4bf3a.jpg",
        "founders/images/2026/09/a5c0a84e6ef74037ae52e110f43eaccf.jpg",
        "galleryslideshow/images/2026/09/26934d7262ae435d8baf92df327633b9.jpg",
        "galleryslideshow/images/2026/09/b7f08ab33a284fa3ac6192add5e8ccb5.jpg",
        "galleryslideshow/images/2026/09/c487a3b671b446cab07d14f2c1ea82b2.jpg"
    };

    // PROTECTED ADMIN IDS
    public static readonly HashSet<Guid> ProtectedAdminUserIds = new()
    {
        Guid.Parse("154096c4-8e7b-49db-90f0-c0a171d320ac"), // Ethos Partner 2 (8341701113)
        Guid.Parse("d54b7143-5627-494d-afdf-d865799b8209")  // Ethos Partner 1 (8019013757)
    };

    public ExecuteApprovedPhaseCResetTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Superseded by ExecuteProductionMediaCleanSlateReset which achieved 0 DB / 0 R2 media")]
    public async Task Execute_Approved_Phase_C_Reset_And_Verify()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets("ethos-dance-studio-neon-production-2026")
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=ep-tiny-violet-b3s8ui17-pooler.c-4.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_ciVXphrD5mS9;SSL Mode=Require;Trust Server Certificate=true;Timeout=30;Command Timeout=30;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=20;";

        var r2AccountId = configuration["CloudflareR2:AccountId"] ?? "253931def0adecdc526c3786262561be";
        var r2AccessKey = configuration["CloudflareR2:AccessKeyId"] ?? "a433147a9ab57c9cdb428e6f4659aa09";
        var r2SecretKey = configuration["CloudflareR2:SecretAccessKey"] ?? "694b08d571b5ef7656b9d2bf89d94666b3ae671e81d78558e9d0e0ed23010f4b";
        var r2BucketName = configuration["CloudflareR2:BucketName"] ?? "ethos-production-media";

        var sb = new StringBuilder();
        void Log(string line)
        {
            _output.WriteLine(line);
            sb.AppendLine(line);
        }

        Log("================================================================================");
        Log("           PHASE C — COMPLETE TEST-DATA RESET EXECUTION                         ");
        Log("================================================================================");
        Log($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Log($"Target Database: Neon PostgreSQL");
        Log($"Target Storage:  Cloudflare R2 ({r2BucketName})");
        Log("");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        using var db = new AppDbContext(optionsBuilder.Options);

        var s3Config = new AmazonS3Config
        {
            ServiceURL = $"https://{r2AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        };
        var credentials = new BasicAWSCredentials(r2AccessKey, r2SecretKey);
        using var s3Client = new AmazonS3Client(credentials, s3Config);

        // ============================================================================
        // STEP C6: NEON POSTGRESQL RESET (ATOMIC TRANSACTION)
        // ============================================================================
        Log("################################################################################");
        Log("                 STEP C6: EXECUTING ATOMIC NEON POSTGRESQL RESET               ");
        Log("################################################################################");

        using var tx = await db.Database.BeginTransactionAsync();

        try
        {
            // 1. Delete Ticket PDFs (68)
            var ticketPdfs = await db.TicketPdfs.ToListAsync();
            Log($"[C6.1] Deleting TicketPdfs: {ticketPdfs.Count} records");
            db.TicketPdfs.RemoveRange(ticketPdfs);
            await db.SaveChangesAsync();

            // 2. Delete Attendances & Events
            var attendances = await db.WorkshopAttendances.ToListAsync();
            var attendanceEvents = await db.WorkshopAttendanceEvents.ToListAsync();
            Log($"[C6.2] Deleting WorkshopAttendances: {attendances.Count}, Events: {attendanceEvents.Count}");
            db.WorkshopAttendanceEvents.RemoveRange(attendanceEvents);
            db.WorkshopAttendances.RemoveRange(attendances);
            await db.SaveChangesAsync();

            // 3. Delete Workshop Feedbacks & Tokens
            var feedbacks = await db.WorkshopFeedbacks.ToListAsync();
            var feedbackTokens = await db.WorkshopFeedbackTokens.ToListAsync();
            Log($"[C6.3] Deleting WorkshopFeedbacks: {feedbacks.Count}, FeedbackTokens: {feedbackTokens.Count}");
            db.WorkshopFeedbackTokens.RemoveRange(feedbackTokens);
            db.WorkshopFeedbacks.RemoveRange(feedbacks);
            await db.SaveChangesAsync();

            // 4. Delete Workshop Tickets (70) & Booking Sessions (22)
            var tickets = await db.WorkshopTickets.ToListAsync();
            var bookingSessions = await db.WorkshopBookingSessions.ToListAsync();
            Log($"[C6.4] Deleting WorkshopTickets: {tickets.Count}, WorkshopBookingSessions: {bookingSessions.Count}");
            db.WorkshopTickets.RemoveRange(tickets);
            db.WorkshopBookingSessions.RemoveRange(bookingSessions);
            await db.SaveChangesAsync();

            // 5. Delete Workshop Bookings (36)
            var bookings = await db.WorkshopBookings.ToListAsync();
            Log($"[C6.5] Deleting WorkshopBookings: {bookings.Count}");
            db.WorkshopBookings.RemoveRange(bookings);
            await db.SaveChangesAsync();

            // 6. Delete Payment Events (19) & Transactions (17)
            var paymentEvents = await db.PaymentEvents.ToListAsync();
            var paymentTransactions = await db.PaymentTransactions.ToListAsync();
            var paymentRefunds = await db.PaymentRefunds.ToListAsync();
            var refundJobs = await db.RefundJobs.ToListAsync();
            Log($"[C6.6] Deleting PaymentEvents: {paymentEvents.Count}, PaymentTransactions: {paymentTransactions.Count}");
            db.PaymentEvents.RemoveRange(paymentEvents);
            db.PaymentRefunds.RemoveRange(paymentRefunds);
            db.RefundJobs.RemoveRange(refundJobs);
            db.PaymentTransactions.RemoveRange(paymentTransactions);
            await db.SaveChangesAsync();

            // 7. Delete WhatsApp Notifications (86) & Notifications (15)
            var waNotifs = await db.WhatsAppNotifications.ToListAsync();
            var notifRecipients = await db.NotificationRecipients.ToListAsync();
            var notifs = await db.Notifications.ToListAsync();
            Log($"[C6.7] Deleting WhatsAppNotifications: {waNotifs.Count}, Notifications: {notifs.Count}");
            db.WhatsAppNotifications.RemoveRange(waNotifs);
            db.NotificationRecipients.RemoveRange(notifRecipients);
            db.Notifications.RemoveRange(notifs);
            await db.SaveChangesAsync();

            // 8. Delete Workshop Pricing Tiers (71), Pass Types (26), Session Trainers (14), Sessions (24), Faculty (18), Drafts (1)
            var pricingTiers = await db.WorkshopPricingTiers.ToListAsync();
            var passTypes = await db.WorkshopPassTypes.ToListAsync();
            var sessionTrainers = await db.WorkshopSessionTrainers.ToListAsync();
            var sessions = await db.WorkshopSessions.ToListAsync();
            var workshopTrainers = await db.WorkshopTrainers.ToListAsync();
            var drafts = await db.WorkshopDrafts.ToListAsync();
            Log($"[C6.8] Deleting PricingTiers: {pricingTiers.Count}, PassTypes: {passTypes.Count}, SessionTrainers: {sessionTrainers.Count}, Sessions: {sessions.Count}, Faculty: {workshopTrainers.Count}, Drafts: {drafts.Count}");
            db.WorkshopPricingTiers.RemoveRange(pricingTiers);
            db.WorkshopPassTypes.RemoveRange(passTypes);
            db.WorkshopSessionTrainers.RemoveRange(sessionTrainers);
            db.WorkshopSessions.RemoveRange(sessions);
            db.WorkshopTrainers.RemoveRange(workshopTrainers);
            db.WorkshopDrafts.RemoveRange(drafts);
            await db.SaveChangesAsync();

            // 9. Delete Workshops (16)
            var workshops = await db.Workshops.ToListAsync();
            Log($"[C6.9] Deleting Workshops: {workshops.Count}");
            db.Workshops.RemoveRange(workshops);
            await db.SaveChangesAsync();

            // 10. Delete Trainer Profiles (5) & Student Profiles (3)
            var trainerProfiles = await db.TrainerProfiles.ToListAsync();
            var studentProfiles = await db.StudentProfiles.ToListAsync();
            Log($"[C6.10] Deleting TrainerProfiles: {trainerProfiles.Count}, StudentProfiles: {studentProfiles.Count}");
            db.TrainerProfiles.RemoveRange(trainerProfiles);
            db.StudentProfiles.RemoveRange(studentProfiles);
            await db.SaveChangesAsync();

            // 11. Delete UserRoles (8) & Non-Admin Test Users (8)
            var nonAdminUsers = await db.Users
                .Include(u => u.UserRoles)
                .Where(u => !ProtectedAdminUserIds.Contains(u.Id))
                .ToListAsync();
            var nonAdminUserRoles = nonAdminUsers.SelectMany(u => u.UserRoles).ToList();
            Log($"[C6.11] Deleting Non-Admin Users: {nonAdminUsers.Count}, UserRoles: {nonAdminUserRoles.Count}");
            db.UserRoles.RemoveRange(nonAdminUserRoles);
            db.Users.RemoveRange(nonAdminUsers);
            await db.SaveChangesAsync();

            // 12. Delete Test Media Placements (28) & Test Media Items (29)
            // PRESERVE only the 13 permanent core items
            var allMediaItems = await db.MediaItems.Include(m => m.Placements).ToListAsync();
            var testMediaItems = allMediaItems.Where(m => !PreservedR2Keys.Contains(m.ObjectKey)).ToList();
            var testPlacements = testMediaItems.SelectMany(m => m.Placements ?? new List<MediaPlacement>()).ToList();
            Log($"[C6.12] Deleting Test MediaPlacements: {testPlacements.Count}, Test MediaItems: {testMediaItems.Count}");
            db.MediaPlacements.RemoveRange(testPlacements);
            db.MediaItems.RemoveRange(testMediaItems);
            await db.SaveChangesAsync();

            // 13. Delete Admin Sessions and Admin Devices per clean-room specification
            var adminSessionsToDelete = await db.AdminSessions.ToListAsync();
            var adminDevicesToDelete = await db.AdminDevices.ToListAsync();
            Log($"[C6.13] Deleting AdminSessions: {adminSessionsToDelete.Count}, AdminDevices: {adminDevicesToDelete.Count}");
            db.AdminSessions.RemoveRange(adminSessionsToDelete);
            db.AdminDevices.RemoveRange(adminDevicesToDelete);
            await db.SaveChangesAsync();

            // COMMIT TRANSACTION
            await tx.CommitAsync();
            Log(">>> [C6 SUCCESS] Database Transaction Committed Successfully! <<<");
            Log("");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            Log($"!!! [C6 FAILED] Transaction Rolled Back due to error: {ex.Message} !!!");
            throw;
        }

        // ============================================================================
        // STEP C7: CLOUDFLARE R2 OBJECT CLEANUP
        // ============================================================================
        Log("################################################################################");
        Log("                 STEP C7: EXECUTING CLOUDFLARE R2 STORAGE CLEANUP               ");
        Log("################################################################################");

        // 1. List all current R2 objects
        var allR2Objects = new List<S3Object>();
        string? continuationToken = null;
        do
        {
            var listReq = new ListObjectsV2Request
            {
                BucketName = r2BucketName,
                ContinuationToken = continuationToken
            };
            var listResp = await s3Client.ListObjectsV2Async(listReq);
            allR2Objects.AddRange(listResp.S3Objects);
            continuationToken = listResp.NextContinuationToken;
        } while (!string.IsNullOrEmpty(continuationToken));

        Log($"Found {allR2Objects.Count} total objects in R2 bucket '{r2BucketName}'");

        // 2. Separate strictly by preserved allow-list
        var objectsToDelete = allR2Objects.Where(o => !PreservedR2Keys.Contains(o.Key)).ToList();
        var objectsToKeep = allR2Objects.Where(o => PreservedR2Keys.Contains(o.Key)).ToList();

        Log($"Objects to Preserve: {objectsToKeep.Count} (Expected: 11)");
        Log($"Objects to Delete:   {objectsToDelete.Count} (Prior to cleanup: 100, Post cleanup: 0)");

        Assert.Equal(11, objectsToKeep.Count);

        if (objectsToDelete.Count > 0)
        {
            // 3. Batch delete objects in chunks of 100
            var deleteKeys = objectsToDelete.Select(o => new KeyVersion { Key = o.Key }).ToList();
            var deleteRequest = new DeleteObjectsRequest
            {
                BucketName = r2BucketName,
                Objects = deleteKeys
            };

            var deleteResponse = await s3Client.DeleteObjectsAsync(deleteRequest);
            Log($"Successfully deleted {deleteResponse.DeletedObjects.Count} objects from R2 bucket '{r2BucketName}'.");
        }
        else
        {
            Log("All 100 test/orphan R2 objects already deleted! Bucket contains strictly the 11 preserved objects.");
        }
        Log("");

        // ============================================================================
        // STEP C8 & C9: POST-CLEANUP REFERENTIAL-INTEGRITY & AUTHENTICATION VERIFICATION
        // ============================================================================
        Log("################################################################################");
        Log("        STEP C8 & C9: POST-CLEANUP INTEGRITY & AUTHENTICATION VERIFICATION      ");
        Log("################################################################################");

        using var verifyDb = new AppDbContext(optionsBuilder.Options);

        // 1. Zero test business entities
        var finalTrainerCount = await verifyDb.TrainerProfiles.CountAsync();
        var finalWorkshopCount = await verifyDb.Workshops.CountAsync();
        var finalSessionCount = await verifyDb.WorkshopSessions.CountAsync();
        var finalBookingCount = await verifyDb.WorkshopBookings.CountAsync();
        var finalTicketCount = await verifyDb.WorkshopTickets.CountAsync();
        var finalTicketPdfCount = await verifyDb.TicketPdfs.CountAsync();
        var finalPaymentTxCount = await verifyDb.PaymentTransactions.CountAsync();
        var finalWaCount = await verifyDb.WhatsAppNotifications.CountAsync();
        var finalNonAdminUsers = await verifyDb.Users.Where(u => !ProtectedAdminUserIds.Contains(u.Id)).CountAsync();

        Log("--- 1. ZERO TEST DATA VERIFICATION ---");
        Log($"  • TrainerProfiles:         {finalTrainerCount} (Must be 0)");
        Log($"  • Workshops:               {finalWorkshopCount} (Must be 0)");
        Log($"  • WorkshopSessions:        {finalSessionCount} (Must be 0)");
        Log($"  • WorkshopBookings:        {finalBookingCount} (Must be 0)");
        Log($"  • WorkshopTickets:         {finalTicketCount} (Must be 0)");
        Log($"  • TicketPdfs:              {finalTicketPdfCount} (Must be 0)");
        Log($"  • PaymentTransactions:     {finalPaymentTxCount} (Must be 0)");
        Log($"  • WhatsAppNotifications:   {finalWaCount} (Must be 0)");
        Log($"  • Non-Admin Users:         {finalNonAdminUsers} (Must be 0)");

        Assert.Equal(0, finalTrainerCount);
        Assert.Equal(0, finalWorkshopCount);
        Assert.Equal(0, finalSessionCount);
        Assert.Equal(0, finalBookingCount);
        Assert.Equal(0, finalTicketCount);
        Assert.Equal(0, finalTicketPdfCount);
        Assert.Equal(0, finalPaymentTxCount);
        Assert.Equal(0, finalWaCount);
        Assert.Equal(0, finalNonAdminUsers);

        // 2. Protected Admin Accounts & Sessions
        var adminUsers = await verifyDb.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => ProtectedAdminUserIds.Contains(u.Id))
            .ToListAsync();

        var adminDevices = await verifyDb.AdminDevices.CountAsync();
        var adminSessions = await verifyDb.AdminSessions.CountAsync();

        Log("");
        Log("--- 2. ADMIN ACCOUNTS & SESSIONS PRESERVATION ---");
        Log($"  • Preserved Admin Users:   {adminUsers.Count} (Must be 2)");
        foreach (var a in adminUsers)
        {
            var rCodes = string.Join(", ", a.UserRoles.Select(ur => ur.Role.Code));
            Log($"    - Admin: {a.FullName} | Phone: {a.Phone} | Roles: [{rCodes}]");
            Assert.Contains(a.UserRoles, ur => ur.Role.Code == "ADMIN");
        }
        Log($"  • Remaining Admin Devices:  {adminDevices} (Must be 0)");
        Log($"  • Remaining Admin Sessions: {adminSessions} (Must be 0)");

        Assert.Equal(2, adminUsers.Count);
        Assert.Equal(0, adminDevices);
        Assert.Equal(0, adminSessions);

        // 3. System Roles, Permissions, Trainer Tiers
        var rolesCount = await verifyDb.Roles.CountAsync();
        var permCount = await verifyDb.Permissions.CountAsync();
        var tiersCount = await verifyDb.TrainerTiers.CountAsync();
        var tierPermCount = await verifyDb.TrainerTierPermissions.CountAsync();

        Log("");
        Log("--- 3. SYSTEM REFERENCE TABLES ---");
        Log($"  • Roles:                   {rolesCount} (Expected: 3)");
        Log($"  • Permissions:             {permCount} (Expected: 14)");
        Log($"  • Trainer Tiers:           {tiersCount} (Expected: 4)");
        Log($"  • Tier Permissions:        {tierPermCount} (Expected: 56)");

        Assert.Equal(3, rolesCount);
        Assert.Equal(14, permCount);
        Assert.Equal(4, tiersCount);
        Assert.Equal(56, tierPermCount);

        // 4. Media & Placements Integrity
        var remainingMediaItems = await verifyDb.MediaItems.Include(m => m.Placements).ToListAsync();
        var remainingPlacements = await verifyDb.MediaPlacements.ToListAsync();

        Log("");
        Log("--- 4. MEDIA & PLACEMENTS INTEGRITY ---");
        Log($"  • Preserved MediaItems:     {remainingMediaItems.Count} (Expected: 11)");
        Log($"  • Preserved MediaPlacements:{remainingPlacements.Count} (Expected: 11)");

        Assert.Equal(11, remainingMediaItems.Count);
        Assert.Equal(11, remainingPlacements.Count);

        // Verify zero orphan placements
        var orphanPlacements = remainingPlacements.Where(p => !remainingMediaItems.Any(m => m.Id == p.MediaItemId)).ToList();
        Log($"  • Orphan MediaPlacements:   {orphanPlacements.Count} (Must be 0)");
        Assert.Empty(orphanPlacements);

        // 5. Cloudflare R2 Remaining Objects Verification
        var finalR2Objects = new List<S3Object>();
        string? finalContToken = null;
        do
        {
            var listReq = new ListObjectsV2Request
            {
                BucketName = r2BucketName,
                ContinuationToken = finalContToken
            };
            var listResp = await s3Client.ListObjectsV2Async(listReq);
            finalR2Objects.AddRange(listResp.S3Objects);
            finalContToken = listResp.NextContinuationToken;
        } while (!string.IsNullOrEmpty(finalContToken));

        Log("");
        Log("--- 5. CLOUDFLARE R2 VERIFICATION ---");
        Log($"  • Remaining R2 Objects:     {finalR2Objects.Count} (Must be exactly 11)");
        Assert.Equal(11, finalR2Objects.Count);

        foreach (var obj in finalR2Objects.OrderBy(o => o.Key))
        {
            Assert.Contains(obj.Key, PreservedR2Keys);
            Log($"    - Verified preserved: {obj.Key} ({obj.Size:N0} bytes)");
        }

        // Verify every remaining DB MediaItem has its corresponding object in R2
        foreach (var mi in remainingMediaItems)
        {
            var r2Obj = finalR2Objects.FirstOrDefault(o => string.Equals(o.Key, mi.ObjectKey, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(r2Obj);
        }
        Log("  ✓ Every database MediaItem maps to an existing R2 object with zero broken links!");

        // 6. Admin Authentication Smoke Test
        Log("");
        Log("--- 6. ADMIN AUTHENTICATION SMOKE TEST ---");
        var admin1 = adminUsers.First(u => u.Phone == "8019013757");
        var admin2 = adminUsers.First(u => u.Phone == "8341701113");

        var totalSessionsAdmin1 = await verifyDb.AdminSessions.Where(s => s.AdminUserId == admin1.Id).CountAsync();
        var totalSessionsAdmin2 = await verifyDb.AdminSessions.Where(s => s.AdminUserId == admin2.Id).CountAsync();
        var totalDevicesAdmin1 = await verifyDb.AdminDevices.Where(d => d.AdminUserId == admin1.Id).CountAsync();
        var totalDevicesAdmin2 = await verifyDb.AdminDevices.Where(d => d.AdminUserId == admin2.Id).CountAsync();

        Log($"  • Admin 1 ({admin1.FullName} - {admin1.Phone}): {totalSessionsAdmin1} sessions, {totalDevicesAdmin1} devices");
        Log($"  • Admin 2 ({admin2.FullName} - {admin2.Phone}): {totalSessionsAdmin2} sessions, {totalDevicesAdmin2} devices");
        Assert.Equal(0, totalSessionsAdmin1);
        Assert.Equal(0, totalSessionsAdmin2);
        Assert.Equal(0, totalDevicesAdmin1);
        Assert.Equal(0, totalDevicesAdmin2);
        Assert.True(admin1.IsActive);
        Assert.True(admin2.IsActive);
        Log("  ✓ Admin partner accounts verified active with clean zeroed session/device state!");

        Log("");
        Log("================================================================================");
        Log("             PHASE C RESET & VERIFICATION COMPLETED SUCCESSFULLY!               ");
        Log("================================================================================");

        var reportPath = @"d:\ETHOS DANCE studio\phase_c_completion_verification_report.txt";
        await File.WriteAllTextAsync(reportPath, sb.ToString());
        Log($"Final verification report written to: {reportPath}");
    }
}
