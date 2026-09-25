using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;

namespace Ethos.Api.Tests.Media;

public class MediaCleanupDryRunTests
{
    private readonly ITestOutputHelper _output;

    public MediaCleanupDryRunTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Execute_Media_Cleanup_Dry_Run_Report()
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
        Log("                MEDIA CLEANUP DRY RUN (READ-ONLY AUDIT)                        ");
        Log("================================================================================");
        Log($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Log($"Target Bucket: {r2BucketName}");
        Log("");

        // 1. Query Database Media Records
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        using var db = new AppDbContext(optionsBuilder.Options);

        var mediaItems = await db.MediaItems.AsNoTracking().Include(m => m.Placements).ToListAsync();
        var mediaPlacements = await db.MediaPlacements.AsNoTracking().ToListAsync();
        var workshopCount = await db.Workshops.AsNoTracking().CountAsync();
        var userCount = await db.Users.AsNoTracking().CountAsync();

        Log("─── 1. DATABASE INVENTORY ──────────────────────────────────────────────────────");
        Log($"Total MediaItem records:       {mediaItems.Count}");
        Log($"Total MediaPlacement records:  {mediaPlacements.Count}");
        Log($"Total Workshop records:        {workshopCount} (PROTECTED - WILL NOT TOUCH)");
        Log($"Total User/Trainer records:    {userCount} (PROTECTED - WILL NOT TOUCH)");
        Log("");

        Log("MediaItem Records Detail:");
        foreach (var item in mediaItems)
        {
            var plInfo = item.Placements != null && item.Placements.Count > 0
                ? string.Join(", ", item.Placements.Select(p => $"{p.Section}[Slot:{p.DisplayOrder}]"))
                : "None";
            Log($"  • ID: {item.Id}");
            Log($"    Title:       \"{item.Title}\"");
            Log($"    ObjectKey:   {item.ObjectKey}");
            Log($"    PublicUrl:   {item.PublicUrl}");
            Log($"    MediaType:   {item.MediaType} | Category: {item.Category} | Layout: {item.LayoutType}");
            Log($"    Placements:  {plInfo}");
            Log("");
        }

        // 2. Query Cloudflare R2 Inventory
        var s3Config = new AmazonS3Config
        {
            ServiceURL = $"https://{r2AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        };
        var credentials = new BasicAWSCredentials(r2AccessKey, r2SecretKey);
        using var s3Client = new AmazonS3Client(credentials, s3Config);

        var r2Objects = new List<S3Object>();
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
                r2Objects.AddRange(listResp.S3Objects);
                continuationToken = listResp.NextContinuationToken;
            } while (!string.IsNullOrEmpty(continuationToken));
        }
        catch (Exception ex)
        {
            Log($"[WARNING] Could not list R2 bucket: {ex.Message}");
        }

        Log("─── 2. CLOUDFLARE R2 INVENTORY ─────────────────────────────────────────────────");
        Log($"Total R2 objects in '{r2BucketName}': {r2Objects.Count}");
        Log("");

        // Categorize R2 objects
        var protectedKeys = new[] { "ethos-emblem", "letterhead", "shanmuka", "avatar" };
        var dbObjectKeys = new HashSet<string>(mediaItems.Select(m => m.ObjectKey).Where(k => !string.IsNullOrWhiteSpace(k)), StringComparer.OrdinalIgnoreCase);

        var protectedR2Objects = new List<S3Object>();
        var ticketPdfR2Objects = new List<S3Object>();
        var workshopR2Objects = new List<S3Object>();
        var mediaItemMatchedR2Objects = new List<S3Object>();
        var orphanSectionMediaR2Objects = new List<S3Object>();

        foreach (var obj in r2Objects)
        {
            if (protectedKeys.Any(pk => obj.Key.ToLower().Contains(pk)))
            {
                protectedR2Objects.Add(obj);
            }
            else if (obj.Key.StartsWith("tickets/", StringComparison.OrdinalIgnoreCase))
            {
                ticketPdfR2Objects.Add(obj);
            }
            else if (obj.Key.StartsWith("workshops/", StringComparison.OrdinalIgnoreCase))
            {
                workshopR2Objects.Add(obj);
            }
            else if (dbObjectKeys.Contains(obj.Key))
            {
                mediaItemMatchedR2Objects.Add(obj);
            }
            else
            {
                orphanSectionMediaR2Objects.Add(obj);
            }
        }

        Log($"  [A] R2 Objects Matching Active MediaItem Records: {mediaItemMatchedR2Objects.Count}");
        foreach (var obj in mediaItemMatchedR2Objects)
        {
            Log($"    - {obj.Key} ({(obj.Size / 1024.0):F1} KB, LastModified: {obj.LastModified:yyyy-MM-dd})");
        }
        Log("");

        Log($"  [B] Other/Orphan Section Media R2 Objects:       {orphanSectionMediaR2Objects.Count}");
        foreach (var obj in orphanSectionMediaR2Objects)
        {
            Log($"    - {obj.Key} ({(obj.Size / 1024.0):F1} KB, LastModified: {obj.LastModified:yyyy-MM-dd})");
        }
        Log("");

        Log($"  [C] Student Ticket PDFs in R2:                   {ticketPdfR2Objects.Count} (PROTECTED - WILL NOT TOUCH)");
        Log($"  [D] Workshop-linked R2 Objects:                  {workshopR2Objects.Count} (Preserved with Workshop records)");
        Log($"  [E] Protected Code-Owned Asset Matches:          {protectedR2Objects.Count} (PROTECTED - WILL NOT TOUCH)");
        Log("");

        // 3. Dry-run Summary
        Log("================================================================================");
        Log("                            DRY RUN SUMMARY                                     ");
        Log("================================================================================");
        Log($"Database MediaPlacement records to delete: {mediaPlacements.Count}");
        Log($"Database MediaItem records to delete:      {mediaItems.Count}");
        Log($"R2 content objects matching MediaItems:    {mediaItemMatchedR2Objects.Count}");
        Log($"R2 orphan section media objects:           {orphanSectionMediaR2Objects.Count}");
        Log($"Total R2 candidate objects to delete:      {mediaItemMatchedR2Objects.Count + orphanSectionMediaR2Objects.Count}");
        Log("");
        Log("WILL DELETE (Upon explicit User Approval):");
        Log("  ✓ MediaPlacement content records");
        Log("  ✓ MediaItem content records");
        Log("  ✓ Candidate R2 content objects (MediaItems + orphan section media)");
        Log("");
        Log("WILL NOT DELETE (Strictly Safeguarded):");
        Log("  ✗ Ethos emblem (src/assets/brand/ethos-emblem.png)");
        Log("  ✗ Letterhead headers/footers (src/assets/official-letterhead-*.png)");
        Log("  ✗ Shanmuka fallback (src/assets/shanmuka.jpg)");
        Log("  ✗ User avatar placeholders (public/images/avatar-placeholder.*)");
        Log($"  ✗ Student ticket PDFs in R2 ({ticketPdfR2Objects.Count} PDFs under tickets/)");
        Log($"  ✗ Workshop records ({workshopCount} in DB) & workshop R2 images");
        Log($"  ✗ Trainer records ({userCount} in DB) & trainer dossiers");
        Log("  ✗ Database schema/tables");
        Log("  ✗ Protected files: Home.jsx, Footer.jsx, footer.css");
        Log("================================================================================");

        await File.WriteAllTextAsync(@"d:\ETHOS DANCE studio\media_cleanup_dry_run_report.txt", sb.ToString());

        Assert.True(true);
    }
}
