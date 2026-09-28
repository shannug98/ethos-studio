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

public class ExecuteProductionMediaCleanSlateReset
{
    private readonly ITestOutputHelper _output;

    private static readonly HashSet<string> Target11R2Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        "aboutethos/images/2026/09/042a84e06f0a4150a2a79c7622b4bf3a.jpg",
        "founders/images/2026/09/a5c0a84e6ef74037ae52e110f43eaccf.jpg",
        "galleryslideshow/images/2026/09/26934d7262ae435d8baf92df327633b9.jpg",
        "galleryslideshow/images/2026/09/b7f08ab33a284fa3ac6192add5e8ccb5.jpg",
        "galleryslideshow/images/2026/09/c487a3b671b446cab07d14f2c1ea82b2.jpg",
        "homepagereels/videos/2026/09/2b5338182ae249a2b6ed21343bc128ad.mp4",
        "homepagereels/videos/2026/09/6014633c51164acf8fa66c9bff927c76.mp4",
        "homepagescrolling/images/2026/09/209485a5b3f24cc483dccab1dfaa59b0.jpg",
        "homepagescrolling/images/2026/09/284ba62bea664d438de4dec6136219d4.jpeg",
        "homepagescrolling/images/2026/09/6dffde15140141e2830ab12f477ec581.jpg",
        "homepagescrolling/videos/2026/09/debf5e435d8048919e89c1fde5be81b5_faststart.mp4"
    };

    public ExecuteProductionMediaCleanSlateReset(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Execute_Production_Media_Clean_Slate_Reset()
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
        Log("      EXPLICIT PRODUCTION MEDIA CLEAN-SLATE RESET (CONTROLLED OPERATION)        ");
        Log("================================================================================");
        Log($"Timestamp:    {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Log($"Target DB:    Neon PostgreSQL (neondb)");
        Log($"Target R2:    Cloudflare R2 ({r2BucketName})");
        Log("");

        var credentials = new BasicAWSCredentials(r2AccessKey, r2SecretKey);
        var s3Config = new AmazonS3Config
        {
            ServiceURL = $"https://{r2AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        };
        using var s3Client = new AmazonS3Client(credentials, s3Config);

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        using var db = new AppDbContext(optionsBuilder.Options);

        // ============================================================================
        // PHASE 1: PRE-CONDITION SAFETY CHECKS (FAIL CLOSED)
        // ============================================================================
        Log("--- PHASE 1: PRE-CONDITION SAFETY AUDIT ---");
        var preMediaItems = await db.MediaItems.AsNoTracking().ToListAsync();
        var prePlacements = await db.MediaPlacements.AsNoTracking().ToListAsync();
        var adminUsers = await db.Users.AsNoTracking().Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.UserRoles.Any(ur => ur.Role.Code == "ADMIN" || ur.Role.Code == "SUPERADMIN"))
            .ToListAsync();

        Log($"  • Current DB MediaItems:       {preMediaItems.Count} (Clean state target: 0)");
        Log($"  • Current DB MediaPlacements:   {prePlacements.Count} (Clean state target: 0)");
        Log($"  • Protected Admin Accounts:    {adminUsers.Count} (Must be exactly 2)");

        Assert.Equal(2, adminUsers.Count);

        // Paginated R2 pre-check
        var preR2Objects = new List<S3Object>();
        string? contToken = null;
        do
        {
            var req = new ListObjectsV2Request
            {
                BucketName = r2BucketName,
                ContinuationToken = contToken
            };
            var resp = await s3Client.ListObjectsV2Async(req);
            if (resp.S3Objects != null && resp.S3Objects.Count > 0)
            {
                preR2Objects.AddRange(resp.S3Objects);
            }
            contToken = resp.NextContinuationToken;
        } while (!string.IsNullOrEmpty(contToken));

        Log($"  • Current R2 Objects in Bucket: {preR2Objects.Count} (Clean state target: 0)");

        // Verify any remaining objects are strictly in the target list
        foreach (var obj in preR2Objects)
        {
            if (!Target11R2Keys.Contains(obj.Key))
            {
                throw new InvalidOperationException($"SAFETY ABORT: Found unexpected R2 object key '{obj.Key}'. Deletion halted!");
            }
        }
        Log("  ✓ Safety Pre-conditions PASSED.");
        Log("");

        // ============================================================================
        // PHASE 2: ATOMIC DATABASE RESET (REVERSE-FK)
        // ============================================================================
        Log("--- PHASE 2: DATABASE ATOMIC DELETION ---");
        if (preMediaItems.Count > 0 || prePlacements.Count > 0)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var placementsToDelete = await db.MediaPlacements.ToListAsync();
                Log($"  [DB 1/2] Deleting MediaPlacements: {placementsToDelete.Count} records");
                db.MediaPlacements.RemoveRange(placementsToDelete);
                await db.SaveChangesAsync();

                var itemsToDelete = await db.MediaItems.ToListAsync();
                Log($"  [DB 2/2] Deleting MediaItems:      {itemsToDelete.Count} records");
                db.MediaItems.RemoveRange(itemsToDelete);
                await db.SaveChangesAsync();

                await tx.CommitAsync();
                Log("  ✓ Database transaction committed successfully!");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                Log($"  ✗ DATABASE TRANSACTION FAILED: {ex.Message}");
                throw;
            }
        }
        else
        {
            Log("  ✓ Database MediaItems & MediaPlacements already zeroed!");
        }

        // Post-DB verification
        using var verifyDb = new AppDbContext(optionsBuilder.Options);
        var postMediaItemsCount = await verifyDb.MediaItems.CountAsync();
        var postPlacementsCount = await verifyDb.MediaPlacements.CountAsync();
        Log($"  • Post-cleanup MediaItems:     {postMediaItemsCount} (Must be 0)");
        Log($"  • Post-cleanup MediaPlacements:{postPlacementsCount} (Must be 0)");
        Assert.Equal(0, postMediaItemsCount);
        Assert.Equal(0, postPlacementsCount);
        Log("");

        // ============================================================================
        // PHASE 3: CLOUDFLARE R2 OBJECT CLEANUP
        // ============================================================================
        Log("--- PHASE 3: CLOUDFLARE R2 STORAGE CLEANUP ---");
        if (preR2Objects.Count > 0)
        {
            int deletedR2Count = 0;
            foreach (var key in Target11R2Keys)
            {
                try
                {
                    await s3Client.DeleteObjectAsync(new DeleteObjectRequest
                    {
                        BucketName = r2BucketName,
                        Key = key
                    });
                    deletedR2Count++;
                    Log($"  [R2 DELETED] {key}");
                }
                catch (Exception ex)
                {
                    Log($"  [R2 DELETE FAILED] {key}: {ex.Message}");
                    throw;
                }
            }
            Log($"  ✓ Deleted {deletedR2Count} R2 objects.");
        }
        else
        {
            Log("  ✓ R2 storage already zeroed (0 objects)!");
        }
        Log("");

        // Paginated post-cleanup verification
        Log("--- PHASE 4: PAGINATED R2 POST-CLEANUP VERIFICATION ---");
        var finalR2Objects = new List<S3Object>();
        string? finalContToken = null;
        int pageIndex = 1;
        do
        {
            var req = new ListObjectsV2Request
            {
                BucketName = r2BucketName,
                ContinuationToken = finalContToken
            };
            var resp = await s3Client.ListObjectsV2Async(req);
            if (resp.S3Objects != null && resp.S3Objects.Count > 0)
            {
                finalR2Objects.AddRange(resp.S3Objects);
            }
            Log($"  • R2 Page {pageIndex++}: returned {resp.S3Objects?.Count ?? 0} objects (IsTruncated: {resp.IsTruncated})");
            finalContToken = resp.NextContinuationToken;
        } while (!string.IsNullOrEmpty(finalContToken));

        var finalTotalBytes = finalR2Objects.Sum(o => o.Size);
        Log($"  • Total Objects in Bucket: {finalR2Objects.Count} (Must be 0)");
        Log($"  • Total Storage Size:      {finalTotalBytes} bytes (Must be 0)");

        Assert.Empty(finalR2Objects);
        Assert.Equal(0, finalTotalBytes);
        Log("  ✓ R2 Bucket 'ethos-production-media' is completely clean (0 objects, 0 bytes)!");
        Log("");

        // ============================================================================
        // PHASE 5: PROTECTED ADMIN STATE AUDIT
        // ============================================================================
        Log("--- PHASE 5: PROTECTED ADMIN STATE VERIFICATION ---");
        var finalAdmins = await verifyDb.Users.AsNoTracking().Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.UserRoles.Any(ur => ur.Role.Code == "ADMIN" || ur.Role.Code == "SUPERADMIN"))
            .ToListAsync();
        Log($"  • Preserved Admin Accounts: {finalAdmins.Count} (Must be 2)");
        foreach (var a in finalAdmins)
        {
            Log($"    - Admin: {a.FullName} | Phone: {a.Phone} | Active: {a.IsActive}");
            Assert.True(a.IsActive);
        }
        Assert.Equal(2, finalAdmins.Count);

        var finalDevices = await verifyDb.AdminDevices.CountAsync();
        var finalSessions = await verifyDb.AdminSessions.CountAsync();
        Log($"  • Active Admin Devices:     {finalDevices}");
        Log($"  • Active Admin Sessions:    {finalSessions}");

        Log("");
        Log("================================================================================");
        Log("   CLEAN-SLATE RESET COMPLETE: 0 DB RECORDS, 0 R2 OBJECTS, 2 ADMINS PRESERVED   ");
        Log("================================================================================");

        var reportPath = @"d:\ETHOS DANCE studio\clean_slate_media_reset_report.txt";
        await File.WriteAllTextAsync(reportPath, sb.ToString());
    }
}
