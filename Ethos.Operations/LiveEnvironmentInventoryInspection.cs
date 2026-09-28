using System.Text;
using System.Text.Json;
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

public class LiveEnvironmentInventoryInspection
{
    private readonly ITestOutputHelper _output;

    public LiveEnvironmentInventoryInspection(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Run_Live_Inventory_Audit_And_Generate_Manifest()
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
        Log("           PHASE C — COMPLETE TEST-DATA RESET (READ-ONLY INVENTORY AUDIT)        ");
        Log("================================================================================");
        Log($"Audit Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Log($"Target Database: Neon PostgreSQL (neondb)");
        Log($"Target Storage:  Cloudflare R2 ({r2BucketName})");
        Log("");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        using var db = new AppDbContext(optionsBuilder.Options);

        // ============================================================================
        // C1. NEON POSTGRESQL LIVE INVENTORY
        // ============================================================================
        Log("################################################################################");
        Log("              C1. NEON POSTGRESQL LIVE TABLE INVENTORY                          ");
        Log("################################################################################");

        // 1. System / Reference Tables
        var roles = await db.Roles.AsNoTracking().ToListAsync();
        var permissions = await db.Permissions.AsNoTracking().ToListAsync();
        var trainerTiers = await db.TrainerTiers.AsNoTracking().ToListAsync();
        var trainerTierPerms = await db.TrainerTierPermissions.AsNoTracking().ToListAsync();
        var packages = await db.Packages.AsNoTracking().ToListAsync();

        Log("--- 1. SYSTEM REFERENCE TABLES (PRESERVED) ---");
        Log($"  • Roles:                     {roles.Count} records ({string.Join(", ", roles.Select(r => r.Code))})");
        Log($"  • Permissions:               {permissions.Count} records");
        Log($"  • Trainer Tiers:             {trainerTiers.Count} records ({string.Join(", ", trainerTiers.Select(t => t.Code))})");
        Log($"  • Trainer Tier Permissions:  {trainerTierPerms.Count} records");
        Log($"  • Packages (Default Catalog):{packages.Count} records");
        Log("");

        // 2. Identity & Access (Admin vs Test Users)
        var allUsers = await db.Users.AsNoTracking().Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ToListAsync();
        var adminUsers = allUsers.Where(u => u.UserRoles.Any(ur => ur.Role.Code == "SUPERADMIN" || ur.Role.Code == "ADMIN")).ToList();
        var nonAdminUsers = allUsers.Where(u => !u.UserRoles.Any(ur => ur.Role.Code == "SUPERADMIN" || ur.Role.Code == "ADMIN")).ToList();
        var studentProfiles = await db.StudentProfiles.AsNoTracking().ToListAsync();
        var adminDevices = await db.AdminDevices.AsNoTracking().ToListAsync();
        var adminSessions = await db.AdminSessions.AsNoTracking().ToListAsync();
        var passwordResetTokens = await db.PasswordResetTokens.AsNoTracking().ToListAsync();

        Log("--- 2. USERS & IDENTITY ---");
        Log($"  • Total Users:               {allUsers.Count}");
        Log($"  • Admin/SuperAdmin Users:    {adminUsers.Count} (PROTECTED)");
        foreach (var u in adminUsers)
        {
            var roleNames = string.Join(", ", u.UserRoles.Select(ur => ur.Role.Code));
            Log($"    - [ADMIN] ID: {u.Id} | Phone: {u.Phone} | Email: {u.Email ?? "(none)"} | Name: \"{u.FullName}\" | Roles: [{roleNames}]");
        }
        Log($"  • Non-Admin / Test Users:    {nonAdminUsers.Count} (CANDIDATE FOR RESET)");
        foreach (var u in nonAdminUsers)
        {
            var roleNames = string.Join(", ", u.UserRoles.Select(ur => ur.Role.Code));
            Log($"    - [USER] ID: {u.Id} | Phone: {u.Phone} | Email: {u.Email ?? "(none)"} | Name: \"{u.FullName}\" | Code: {u.CustomerCode} | Roles: [{roleNames}]");
        }
        Log($"  • Student Profiles:          {studentProfiles.Count}");
        Log($"  • Admin Devices:             {adminDevices.Count}");
        Log($"  • Admin Sessions:            {adminSessions.Count}");
        Log($"  • Password Reset Tokens:     {passwordResetTokens.Count}");
        Log("");

        // 3. Trainers & Faculty
        var trainerProfiles = await db.TrainerProfiles.AsNoTracking().ToListAsync();
        var trainerApplications = await db.TrainerApplications.AsNoTracking().ToListAsync();
        var trainerAppVideos = await db.TrainerApplicationVideos.AsNoTracking().ToListAsync();
        var trainerGalleryImages = await db.TrainerGalleryImages.AsNoTracking().ToListAsync();
        var trainerPermOverrides = await db.TrainerPermissionOverrides.AsNoTracking().ToListAsync();
        var trainerTierHistories = await db.TrainerTierHistories.AsNoTracking().ToListAsync();
        var trainerAvailabilities = await db.TrainerAvailabilities.AsNoTracking().ToListAsync();
        var trainerUpgradeRequests = await db.TrainerUpgradeRequests.AsNoTracking().ToListAsync();
        var trainerPerfSnapshots = await db.TrainerPerformanceSnapshots.AsNoTracking().ToListAsync();

        Log("--- 3. TRAINERS & APPLICATIONS ---");
        Log($"  • Trainer Profiles:          {trainerProfiles.Count}");
        foreach (var t in trainerProfiles)
        {
            Log($"    - Trainer ID: {t.Id} | Code: {t.TrainerCode} | Name: \"{t.FullName}\" | Status: {t.Status} | PhotoUrl: {t.ProfilePhotoUrl ?? "(none)"}");
        }
        Log($"  • Trainer Applications:      {trainerApplications.Count}");
        Log($"  • Trainer App Videos:        {trainerAppVideos.Count}");
        Log($"  • Trainer Gallery Images:    {trainerGalleryImages.Count}");
        Log($"  • Trainer Perm Overrides:    {trainerPermOverrides.Count}");
        Log($"  • Trainer Tier Histories:    {trainerTierHistories.Count}");
        Log($"  • Trainer Availabilities:    {trainerAvailabilities.Count}");
        Log($"  • Trainer Upgrade Requests:  {trainerUpgradeRequests.Count}");
        Log($"  • Trainer Perf Snapshots:    {trainerPerfSnapshots.Count}");
        Log("");

        // 4. Workshops & Associated Scheduling
        var workshops = await db.Workshops.AsNoTracking().ToListAsync();
        var workshopTrainers = await db.WorkshopTrainers.AsNoTracking().ToListAsync();
        var workshopSessions = await db.WorkshopSessions.AsNoTracking().ToListAsync();
        var workshopSessionTrainers = await db.WorkshopSessionTrainers.AsNoTracking().ToListAsync();
        var workshopPassTypes = await db.WorkshopPassTypes.AsNoTracking().ToListAsync();
        var workshopPricingTiers = await db.WorkshopPricingTiers.AsNoTracking().ToListAsync();
        var workshopDrafts = await db.WorkshopDrafts.AsNoTracking().ToListAsync();

        Log("--- 4. WORKSHOPS & SCHEDULE ---");
        Log($"  • Workshops:                 {workshops.Count}");
        foreach (var w in workshops)
        {
            Log($"    - Workshop ID: {w.Id} | Title: \"{w.Title}\" | Status: {w.Status} | Date: {w.WorkshopDate:yyyy-MM-dd} | ImageUrl: {w.ImageUrl ?? "(none)"}");
        }
        Log($"  • Workshop Trainers (Faculty):{workshopTrainers.Count}");
        Log($"  • Workshop Sessions:         {workshopSessions.Count}");
        Log($"  • Workshop Session Trainers: {workshopSessionTrainers.Count}");
        Log($"  • Workshop Pass Types:       {workshopPassTypes.Count}");
        Log($"  • Workshop Pricing Tiers:    {workshopPricingTiers.Count}");
        Log($"  • Workshop Drafts:           {workshopDrafts.Count}");
        Log("");

        // 5. Bookings, Tickets, Attendance & Feedback
        var workshopBookings = await db.WorkshopBookings.AsNoTracking().ToListAsync();
        var bookingSessions = await db.WorkshopBookingSessions.AsNoTracking().ToListAsync();
        var workshopTickets = await db.WorkshopTickets.AsNoTracking().ToListAsync();
        var ticketPdfs = await db.TicketPdfs.AsNoTracking().ToListAsync();
        var workshopAttendances = await db.WorkshopAttendances.AsNoTracking().ToListAsync();
        var attendanceEvents = await db.WorkshopAttendanceEvents.AsNoTracking().ToListAsync();
        var workshopFeedbacks = await db.WorkshopFeedbacks.AsNoTracking().ToListAsync();
        var feedbackTokens = await db.WorkshopFeedbackTokens.AsNoTracking().ToListAsync();

        Log("--- 5. BOOKINGS & TICKETING ---");
        Log($"  • Workshop Bookings:         {workshopBookings.Count}");
        Log($"  • Booking Sessions:          {bookingSessions.Count}");
        Log($"  • Workshop Tickets:          {workshopTickets.Count}");
        Log($"  • Generated Ticket PDFs:     {ticketPdfs.Count}");
        Log($"  • Workshop Attendances:      {workshopAttendances.Count}");
        Log($"  • Attendance Events:         {attendanceEvents.Count}");
        Log($"  • Workshop Feedbacks:        {workshopFeedbacks.Count}");
        Log($"  • Feedback Tokens:           {feedbackTokens.Count}");
        Log("");

        // 6. Payments & Financial Ledgers
        var paymentTransactions = await db.PaymentTransactions.AsNoTracking().ToListAsync();
        var paymentEvents = await db.PaymentEvents.AsNoTracking().ToListAsync();
        var paymentRefunds = await db.PaymentRefunds.AsNoTracking().ToListAsync();
        var refundJobs = await db.RefundJobs.AsNoTracking().ToListAsync();

        Log("--- 6. PAYMENTS & REFUNDS ---");
        Log($"  • Payment Transactions:      {paymentTransactions.Count}");
        Log($"  • Payment Events:            {paymentEvents.Count}");
        Log($"  • Payment Refunds:           {paymentRefunds.Count}");
        Log($"  • Refund Jobs:               {refundJobs.Count}");
        Log("");

        // 7. Classes, Enrolments & Attendance
        var danceClasses = await db.DanceClasses.AsNoTracking().ToListAsync();
        var classSchedules = await db.ClassSchedules.AsNoTracking().ToListAsync();
        var classSessions = await db.ClassSessions.AsNoTracking().ToListAsync();
        var classEnrollments = await db.ClassEnrollments.AsNoTracking().ToListAsync();
        var classFeedbacks = await db.ClassFeedbacks.AsNoTracking().ToListAsync();
        var attendanceRecords = await db.AttendanceRecords.AsNoTracking().ToListAsync();
        var studentPackages = await db.StudentPackages.AsNoTracking().ToListAsync();

        Log("--- 7. CLASSES & ENROLLMENTS ---");
        Log($"  • Dance Classes:             {danceClasses.Count}");
        Log($"  • Class Schedules:           {classSchedules.Count}");
        Log($"  • Class Sessions:            {classSessions.Count}");
        Log($"  • Class Enrollments:         {classEnrollments.Count}");
        Log($"  • Class Feedbacks:           {classFeedbacks.Count}");
        Log($"  • Attendance Records:        {attendanceRecords.Count}");
        Log($"  • Student Packages:          {studentPackages.Count}");
        Log("");

        // 8. Notifications & Communications
        var notifications = await db.Notifications.AsNoTracking().ToListAsync();
        var notificationRecipients = await db.NotificationRecipients.AsNoTracking().ToListAsync();
        var whatsAppNotifications = await db.WhatsAppNotifications.AsNoTracking().ToListAsync();
        var commLogs = await db.CommunicationLogs.AsNoTracking().ToListAsync();

        Log("--- 8. NOTIFICATIONS & COMMUNICATIONS ---");
        Log($"  • Notifications:             {notifications.Count}");
        Log($"  • Notification Recipients:   {notificationRecipients.Count}");
        Log($"  • WhatsApp Notifications:    {whatsAppNotifications.Count}");
        Log($"  • Communication Logs:        {commLogs.Count}");
        Log("");

        // 9. Media & Content Placements
        var mediaItems = await db.MediaItems.AsNoTracking().Include(m => m.Placements).ToListAsync();
        var mediaPlacements = await db.MediaPlacements.AsNoTracking().ToListAsync();
        var studioVideos = await db.StudioVideos.AsNoTracking().ToListAsync();

        Log("--- 9. MEDIA & PLACEMENTS ---");
        Log($"  • Total MediaItems in DB:    {mediaItems.Count}");
        Log($"  • Total MediaPlacements:     {mediaPlacements.Count}");
        Log($"  • Studio Videos:             {studioVideos.Count}");
        Log("");

        // 10. Audit, Security, Telemetry
        var adminActions = await db.AdminActions.AsNoTracking().ToListAsync();
        var securityEvents = await db.SecurityEvents.AsNoTracking().ToListAsync();
        var apiRequestLogs = await db.ApiRequestLogs.AsNoTracking().ToListAsync();
        var incidents = await db.Incidents.AsNoTracking().ToListAsync();
        var incidentUpdates = await db.IncidentUpdates.AsNoTracking().ToListAsync();
        var correctiveActions = await db.CorrectiveActions.AsNoTracking().ToListAsync();

        Log("--- 10. AUDIT & TELEMETRY ---");
        Log($"  • Admin Actions (Audit):     {adminActions.Count}");
        Log($"  • Security Events:           {securityEvents.Count}");
        Log($"  • Api Request Logs:          {apiRequestLogs.Count}");
        Log($"  • Incidents:                 {incidents.Count}");
        Log($"  • Incident Updates:          {incidentUpdates.Count}");
        Log($"  • Corrective Actions:        {correctiveActions.Count}");
        Log("");

        // ============================================================================
        // C2. CLOUDFLARE R2 LIVE STORAGE INVENTORY
        // ============================================================================
        Log("################################################################################");
        Log("              C2. CLOUDFLARE R2 LIVE STORAGE INVENTORY                          ");
        Log("################################################################################");

        var s3Config = new AmazonS3Config
        {
            ServiceURL = $"https://{r2AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        };
        var credentials = new BasicAWSCredentials(r2AccessKey, r2SecretKey);
        using var s3Client = new AmazonS3Client(credentials, s3Config);

        var allR2Objects = new List<S3Object>();
        string? continuationToken = null;

        try
        {
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
        }
        catch (Exception ex)
        {
            Log($"[R2 ERROR] Failed to list R2 objects: {ex.Message}");
        }

        Log($"Total Objects in R2 Bucket '{r2BucketName}': {allR2Objects.Count}");
        var totalBytes = allR2Objects.Sum(o => o.Size);
        Log($"Total Storage Size in R2: {totalBytes:N0} bytes ({(totalBytes / (1024.0 * 1024.0)):F2} MB)");
        Log("");

        var prefixGroups = allR2Objects
            .GroupBy(o => o.Key.Contains('/') ? o.Key[..o.Key.IndexOf('/')] : "(root)")
            .OrderBy(g => g.Key)
            .ToList();

        Log("R2 Object Distribution by Prefix:");
        foreach (var group in prefixGroups)
        {
            var groupBytes = group.Sum(o => o.Size);
            Log($"  • {group.Key.PadRight(20)}: {group.Count().ToString().PadLeft(4)} objects | {(groupBytes / (1024.0 * 1024.0)):F2} MB");
        }
        Log("");

        // Cross-reference with DB
        var dbObjectKeys = new HashSet<string>(mediaItems.Select(m => m.ObjectKey), StringComparer.OrdinalIgnoreCase);
        var dbTicketPdfKeys = new HashSet<string>(ticketPdfs.Select(tp => tp.StorageKey), StringComparer.OrdinalIgnoreCase);

        var r2Keys = allR2Objects.Select(o => o.Key).ToList();
        var matchingMediaKeys = r2Keys.Where(k => dbObjectKeys.Contains(k)).ToList();
        var matchingTicketKeys = r2Keys.Where(k => dbTicketPdfKeys.Contains(k)).ToList();
        var orphanR2Keys = r2Keys.Where(k => !dbObjectKeys.Contains(k) && !dbTicketPdfKeys.Contains(k)).ToList();

        Log("Cross-Reference DB vs R2:");
        Log($"  • R2 Objects matched to MediaItems:   {matchingMediaKeys.Count}");
        Log($"  • R2 Objects matched to TicketPdfs:   {matchingTicketKeys.Count}");
        Log($"  • Orphan R2 Objects (not in DB):      {orphanR2Keys.Count}");
        Log("");

        Log("Itemized R2 Objects Breakdown:");
        foreach (var obj in allR2Objects.OrderBy(o => o.Key))
        {
            string association;
            if (dbObjectKeys.Contains(obj.Key))
            {
                var mi = mediaItems.First(m => string.Equals(m.ObjectKey, obj.Key, StringComparison.OrdinalIgnoreCase));
                var pl = mi.Placements?.Count > 0 ? string.Join(", ", mi.Placements.Select(p => $"{p.Section}[Slot:{p.DisplayOrder}]")) : "No Placement";
                association = $"[DB MediaItem: \"{mi.Title}\" | Section: {pl}]";
            }
            else if (dbTicketPdfKeys.Contains(obj.Key))
            {
                association = $"[DB TicketPdf]";
            }
            else
            {
                association = $"[ORPHAN R2 OBJECT]";
            }
            Log($"    - {obj.Key.PadRight(70)} ({obj.Size:N0} bytes) {association}");
        }
        Log("");

        // Reconciliation of the 11 Preserved Objects vs 100 Deletion Objects
        var preservedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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

        var preservedObjects = allR2Objects.Where(o => preservedKeys.Contains(o.Key)).ToList();
        var deletionObjects = allR2Objects.Where(o => !preservedKeys.Contains(o.Key)).ToList();

        long preservedBytes = preservedObjects.Sum(o => o.Size ?? 0);
        long deletionBytes = deletionObjects.Sum(o => o.Size ?? 0);
        long grandTotalBytes = allR2Objects.Sum(o => o.Size ?? 0);

        Log("================================================================================");
        Log("         PRECISE RECONCILIATION: PRESERVED (11) vs DELETION (100) OBJECTS       ");
        Log("================================================================================");
        Log($"Total Objects in Bucket:       {allR2Objects.Count}");
        Log($"  - Preserved Objects Count:   {preservedObjects.Count}");
        Log($"  - Deletion Objects Count:    {deletionObjects.Count}");
        Log($"  - Check Sum:                 {preservedObjects.Count + deletionObjects.Count} (Must equal {allR2Objects.Count})");
        Log("");

        Log("Byte-Level Totals:");
        Log($"  - Preserved Exact Bytes:     {preservedBytes:N0} bytes");
        Log($"    • Preserved in MiB (binary 1024^2):  {(preservedBytes / (1024.0 * 1024.0)):F4} MiB ({(preservedBytes / (1024.0 * 1024.0)):F2} MB)");
        Log($"    • Preserved in MB (decimal 10^6):    {(preservedBytes / 1000000.0):F4} MB");
        Log("");
        Log($"  - Deletion Exact Bytes:      {deletionBytes:N0} bytes");
        Log($"    • Deletion in MiB (binary 1024^2):   {(deletionBytes / (1024.0 * 1024.0)):F4} MiB ({(deletionBytes / (1024.0 * 1024.0)):F2} MB)");
        Log($"    • Deletion in MB (decimal 10^6):     {(deletionBytes / 1000000.0):F4} MB");
        Log("");
        Log($"  - Grand Total Exact Bytes:   {grandTotalBytes:N0} bytes");
        Log($"    • Grand Total in MiB:               {(grandTotalBytes / (1024.0 * 1024.0)):F4} MiB ({(grandTotalBytes / (1024.0 * 1024.0)):F2} MB)");
        Log($"    • Grand Total in MB:                 {(grandTotalBytes / 1000000.0):F4} MB");
        Log($"  - Arithmetic Verification:   {preservedBytes:N0} + {deletionBytes:N0} = {preservedBytes + deletionBytes:N0} (Matches: {preservedBytes + deletionBytes == grandTotalBytes})");
        Log("");

        Log("Itemized Breakdown of the 11 Preserved Objects:");
        foreach (var p in preservedObjects.OrderBy(o => o.Key))
        {
            Log($"  • {p.Key.PadRight(75)} | {p.Size,12:N0} bytes | {(p.Size / (1024.0 * 1024.0)),8:F2} MiB | {(p.Size / 1000000.0),8:F2} MB");
        }
        Log("");

        // Write to audit manifest file
        var outputPath = @"d:\ETHOS DANCE studio\phase_c_live_inventory_manifest.txt";
        await File.WriteAllTextAsync(outputPath, sb.ToString());
        Log($"Full live inventory and manifest written to: {outputPath}");
    }
}
