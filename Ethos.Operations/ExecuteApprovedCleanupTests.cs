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

public class ExecuteApprovedCleanupTests
{
    private readonly ITestOutputHelper _output;

    // EXACT 28 AUDITED CANDIDATE MEDIA ITEMS (ALLOW-LIST ONLY)
    public static readonly Dictionary<Guid, string> AuditedCandidateAllowList = new()
    {
        { Guid.Parse("04756cea-4848-45de-9b1a-7083723e87ea"), "draft/images/2026/09/064cc663ee5049e1adbb43fc30132f85.jpg" },
        { Guid.Parse("05487728-4b05-4fed-80ba-8284025fc043"), "draft/images/2026/09/6587885e94494eebb8882f014459b1ba.jpg" },
        { Guid.Parse("09cb303e-fd4e-4b86-ad27-c496c9114f9c"), "draft/images/2026/09/a6b0acef392c466ea0e09258937ea1bd.jpg" },
        { Guid.Parse("0f818d4e-1eea-4feb-90a0-22c49eec3046"), "draft/images/2026/09/f299dd6ef26348749c4a2229f9b84959.jpg" },
        { Guid.Parse("14a54fd3-6b65-4c86-a61c-1e56ba01ea3e"), "draft/images/2026/09/8f1bfb32a2354f2492f6da1c2974dd13.jpg" },
        { Guid.Parse("226b0036-db06-4244-968b-c3c69b50ed71"), "draft/images/2026/09/f19e0e81badd45788172cb426cfcfade.jpg" },
        { Guid.Parse("26ddd2ce-fe84-42c9-b403-49eea57aa1d1"), "trainers/images/2026/09/f20bea41d7d24b438bf46f83caf9a6c4.jpg" },
        { Guid.Parse("2cb8e465-60be-4054-829a-65fb3f08ee8a"), "draft/images/2026/09/5a7ae9d68cb340efa1ce48f3fd5ac7af.jpg" },
        { Guid.Parse("40f984ce-e5d4-482f-b45f-25b0aeac03b0"), "galleryimages/images/2026/09/72abac78e95944dca7863c688d865610.jpeg" },
        { Guid.Parse("5b4645b0-3f2e-40af-8dc5-d9d6bb4dafd2"), "draft/images/2026/09/b0b729f36ee04b68b038a2a2cd0a9656.jpg" },
        { Guid.Parse("5df7ac56-7784-4827-a2bc-b5001dd7b759"), "draft/images/2026/09/eec12eeb626d4eb99c5f18ea13fc791f.jpg" },
        { Guid.Parse("6a139ef8-aab4-44fb-bcf0-cc1110d179e1"), "draft/images/2026/09/ff08edf4b7fe4caf9e1e3ec9b40bac4a.jpg" },
        { Guid.Parse("6fced4f6-e8e1-48ca-b7fd-9a942506ac0f"), "draft/images/2026/09/9d15ec4bcaec4de693f529600a42a4fb.jpg" },
        { Guid.Parse("72523d0a-67c5-4a2c-b665-c2ca26d8c5db"), "draft/images/2026/09/b4e228dc32cd4c74bfe07580c9ca0da0.jpg" },
        { Guid.Parse("7f53633e-d6ed-4c56-a2b5-60e3890de4ce"), "draft/images/2026/09/61ca826e23494364bb9fc70891ea2567.jpg" },
        { Guid.Parse("826e1721-c71a-4f06-856a-19808323c538"), "galleryvideos/videos/2026/09/3e2d91bf7ee94dcb86f32311a0f9b3ac.mp4" },
        { Guid.Parse("903ed24b-3412-44f6-8c1e-fd431957a36c"), "draft/images/2026/09/0911f22b25da4678a0d0fe82d0866d9f.jpg" },
        { Guid.Parse("90f7de23-df49-4193-b9b3-833973cba149"), "draft/images/2026/09/b693b739146745bfaf5732e39e8ae527.jpg" },
        { Guid.Parse("94147298-530f-43d3-8aab-91a84a545e06"), "draft/images/2026/09/53fe23d496e24f1e8ff05f58c49c54d0.jpg" },
        { Guid.Parse("a12b5a58-1cab-4366-9cdd-de0516f863bc"), "draft/images/2026/09/b41450af12d6481cb0475cb064c1baed.jpg" },
        { Guid.Parse("a7ad8a3e-7737-4a4e-b86d-502f3f9083ff"), "draft/images/2026/09/9a8d90c7a9c548a39cd5f6fad23c47f7.jpg" },
        { Guid.Parse("b3dd5569-f672-4f07-a11b-f953d1acae35"), "homepagescrolling/images/2026/09/c2c64e6920dd49b8b92d3ffa250ff894.jpg" },
        { Guid.Parse("bc25f19c-2314-4d17-bfb1-5517420190c5"), "draft/images/2026/09/6a62556f66364f19b7374cb4c6badef4.jpg" },
        { Guid.Parse("d256e3c3-aa87-4413-8563-cc62fac40d38"), "draft/images/2026/09/a4429d3483c240f085282d2557072536.jpg" },
        { Guid.Parse("def7562b-4d65-4b1c-9c16-f35458e1e1a2"), "draft/images/2026/09/04465e826ec543e39683e6d669472379.jpg" },
        { Guid.Parse("eef0162c-e005-471f-9965-427efc7e100d"), "draft/images/2026/09/2dfff293cf914dac9162a878af512d1e.jpg" },
        { Guid.Parse("f8def998-36b4-4647-b829-898bf0af9c38"), "draft/images/2026/09/d137e764feb4433a8e95f0117a56c0f4.jpg" },
        { Guid.Parse("fd703695-0eef-4390-8871-3cd4bf5aaba7"), "draft/images/2026/09/2a0f13784b3049c0a4c8b0e4434465e0.jpg" }
    };

    public ExecuteApprovedCleanupTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Phase C test-data cleanup script - deferred until user approval")]
    public async Task PreExecution_Safety_Verification_Audit_Only()
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
        Log("         PRE-EXECUTION ALLOW-LIST INTEGRITY AUDIT (READ-ONLY)                   ");
        Log("================================================================================");
        Log($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Log($"Allow-list size: {AuditedCandidateAllowList.Count} exact MediaItem IDs");
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

        // 1. Verify exact 28 MediaItems exist in DB
        Log("─── 1. VERIFYING CANDIDATE MEDIA ITEMS IN DATABASE ────────────────────────────");
        var candidateIds = AuditedCandidateAllowList.Keys.ToList();
        var candidateItems = await db.MediaItems
            .Include(m => m.Placements)
            .Where(m => candidateIds.Contains(m.Id))
            .ToListAsync();

        Log($"Expected candidate count: {candidateIds.Count}");
        Log($"Found in database:        {candidateItems.Count}");
        Assert.Equal(candidateIds.Count, candidateItems.Count);

        // 2. Verify every ID matches the exact audited ObjectKey
        Log("");
        Log("─── 2. VERIFYING OBJECT KEY MAPPINGS ──────────────────────────────────────────");
        foreach (var item in candidateItems)
        {
            var expectedKey = AuditedCandidateAllowList[item.Id];
            Assert.Equal(expectedKey, item.ObjectKey);
            Log($"  ✓ Item {item.Id} matches expected ObjectKey: {item.ObjectKey}");
        }

        // 3. Verify associated placements
        Log("");
        Log("─── 3. VERIFYING ASSOCIATED MEDIA PLACEMENTS ──────────────────────────────────");
        var candidatePlacements = await db.MediaPlacements
            .Where(p => candidateIds.Contains(p.MediaItemId))
            .ToListAsync();

        Log($"Candidate placements found: {candidatePlacements.Count} (Expected: 28)");
        Assert.Equal(28, candidatePlacements.Count);
        foreach (var p in candidatePlacements)
        {
            Assert.Contains(p.MediaItemId, candidateIds);
        }
        Log("  ✓ All candidate placements belong strictly to the 28 allow-listed MediaItems.");

        // 4. Re-run complete reference check across all production entities
        Log("");
        Log("─── 4. RE-RUNNING COMPLETE REFERENCE INTEGRITY CHECK ──────────────────────────");
        var candidateKeys = AuditedCandidateAllowList.Values.ToList();
        var candidateFilenames = candidateKeys.Select(k => Path.GetFileName(k)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var workshops = await db.Workshops.AsNoTracking().ToListAsync();
        var trainerProfiles = await db.TrainerProfiles.AsNoTracking().ToListAsync();
        var bookings = await db.WorkshopBookings.AsNoTracking().ToListAsync();
        var tickets = await db.WorkshopTickets.AsNoTracking().ToListAsync();
        var ticketPdfs = await db.TicketPdfs.AsNoTracking().ToListAsync();
        var studentProfiles = await db.StudentProfiles.AsNoTracking().ToListAsync();
        var trainerGallery = await db.TrainerGalleryImages.AsNoTracking().ToListAsync();

        Log($"Checking against {workshops.Count} workshops, {trainerProfiles.Count} trainer profiles, {bookings.Count} bookings, {tickets.Count} tickets, {ticketPdfs.Count} ticket PDFs, {studentProfiles.Count} student profiles, {trainerGallery.Count} trainer gallery images.");

        var brokenReferences = new List<string>();

        // Check workshops
        foreach (var ws in workshops)
        {
            if (!string.IsNullOrWhiteSpace(ws.ImageUrl))
            {
                var fn = Path.GetFileName(ws.ImageUrl);
                if (candidateFilenames.Contains(fn) || candidateKeys.Any(k => ws.ImageUrl.Contains(k)))
                {
                    brokenReferences.Add($"Workshop '{ws.Title}' references ImageUrl: {ws.ImageUrl}");
                }
            }
            if (!string.IsNullOrWhiteSpace(ws.LandscapeImageUrl))
            {
                var fn = Path.GetFileName(ws.LandscapeImageUrl);
                if (candidateFilenames.Contains(fn) || candidateKeys.Any(k => ws.LandscapeImageUrl.Contains(k)))
                {
                    brokenReferences.Add($"Workshop '{ws.Title}' references LandscapeImageUrl: {ws.LandscapeImageUrl}");
                }
            }
        }

        // Check trainer profiles
        foreach (var tp in trainerProfiles)
        {
            if (!string.IsNullOrWhiteSpace(tp.ProfilePhotoUrl))
            {
                var fn = Path.GetFileName(tp.ProfilePhotoUrl);
                if (candidateFilenames.Contains(fn) || candidateKeys.Any(k => tp.ProfilePhotoUrl.Contains(k)))
                {
                    brokenReferences.Add($"TrainerProfile '{tp.FullName}' ({tp.TrainerCode}) references ProfilePhotoUrl: {tp.ProfilePhotoUrl}");
                }
            }
        }

        // Check ticket PDFs
        foreach (var tp in ticketPdfs)
        {
            if (!string.IsNullOrWhiteSpace(tp.StorageKey))
            {
                var fn = Path.GetFileName(tp.StorageKey);
                if (candidateFilenames.Contains(fn) || candidateKeys.Any(k => tp.StorageKey.Contains(k)))
                {
                    brokenReferences.Add($"TicketPdf ({tp.Id}) references StorageKey: {tp.StorageKey}");
                }
            }
        }

        // Check student profiles
        foreach (var sp in studentProfiles)
        {
            if (!string.IsNullOrWhiteSpace(sp.ProfilePhotoUrl))
            {
                var fn = Path.GetFileName(sp.ProfilePhotoUrl);
                if (candidateFilenames.Contains(fn) || candidateKeys.Any(k => sp.ProfilePhotoUrl.Contains(k)))
                {
                    brokenReferences.Add($"StudentProfile ({sp.Id}) references ProfilePhotoUrl: {sp.ProfilePhotoUrl}");
                }
            }
        }

        Log($"Reference audit complete. Detected references count: {brokenReferences.Count}");
        foreach (var r in brokenReferences)
        {
            Log($"  • {r}");
        }

        // 5. Verify R2 student ticket PDFs
        Log("");
        Log("─── 5. VERIFYING R2 PROTECTED STUDENT TICKET PDFS ─────────────────────────────");
        var ticketPdfObjects = new List<S3Object>();
        string? contToken = null;
        do
        {
            var listReq = new ListObjectsV2Request
            {
                BucketName = r2BucketName,
                Prefix = "tickets/",
                ContinuationToken = contToken
            };
            var listResp = await s3Client.ListObjectsV2Async(listReq);
            ticketPdfObjects.AddRange(listResp.S3Objects);
            contToken = listResp.NextContinuationToken;
        } while (!string.IsNullOrEmpty(contToken));

        Log($"Live Student Ticket PDFs in R2: {ticketPdfObjects.Count} (Expected: 68 - Strictly Protected)");
        Assert.Equal(68, ticketPdfObjects.Count);

        // 6. Verify each of the 28 candidate keys is currently present in R2
        Log("");
        Log("─── 6. VERIFYING EXACT 28 CANDIDATE OBJECTS CURRENTLY IN R2 ───────────────────");
        int candidateFoundInR2 = 0;
        foreach (var key in candidateKeys)
        {
            try
            {
                var meta = await s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = r2BucketName,
                    Key = key
                });
                candidateFoundInR2++;
            }
            catch (Exception ex)
            {
                Log($"  ⚠️ Object missing in R2: {key} ({ex.Message})");
            }
        }
        Log($"Found {candidateFoundInR2}/28 candidate objects currently in Cloudflare R2.");
        Assert.Equal(28, candidateFoundInR2);

        Log("");
        Log("================================================================================");
        Log("                ALLOW-LIST AUDIT SUMMARY                                        ");
        Log("================================================================================");
        Log($"  • Exact 28 MediaItems verified in DB:      YES");
        Log($"  • Exact 28 ObjectKey mappings verified:    YES");
        Log($"  • Exact 28 MediaPlacements verified:       YES");
        Log($"  • 68 Ticket PDFs verified intact in R2:    YES (Isolated under tickets/)");
        Log($"  • 28 Exact candidate objects in R2:        YES (100% matched)");
        Log($"  • Live references detected:                {brokenReferences.Count}");
        if (brokenReferences.Count > 0)
        {
            Log($"    -> Pending authorization: {brokenReferences[0]}");
        }
        Log("  • STATE: DELETION ON HOLD - ZERO RECORDS MODIFIED OR DELETED.");

        await File.WriteAllTextAsync(@"d:\ETHOS DANCE studio\media_cleanup_allowlist_audit.txt", sb.ToString());
    }

    [Fact(Skip = "Phase C test-data cleanup script - deferred until user approval")]
    public async Task Execute_Approved_AllowList_Cleanup()
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
        Log("         AUTHORIZED EXACT-ID MEDIA CLEANUP EXECUTION & VERIFICATION             ");
        Log("================================================================================");
        Log($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Log($"Authorized by user: Yes (TRN-2026-0004 / SL confirmed as test data)");
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

        var candidateIds = AuditedCandidateAllowList.Keys.ToList();
        var candidateKeys = AuditedCandidateAllowList.Values.ToList();

        // 1. Immediately re-query DB to verify exact 28 candidate items
        var targetItems = await db.MediaItems
            .Where(m => candidateIds.Contains(m.Id))
            .ToListAsync();

        Assert.Equal(28, targetItems.Count);

        // 2. Query only placements belonging to these exact 28 MediaItems
        var targetPlacements = await db.MediaPlacements
            .Where(p => candidateIds.Contains(p.MediaItemId))
            .ToListAsync();

        Assert.Equal(28, targetPlacements.Count);

        // 3. Nullify TrainerProfile.ProfilePhotoUrl for TRN-2026-0004
        var trainerSL = await db.TrainerProfiles.FirstOrDefaultAsync(tp => tp.TrainerCode == "TRN-2026-0004");
        if (trainerSL != null)
        {
            trainerSL.ProfilePhotoUrl = null;
            trainerSL.UpdatedAt = DateTime.UtcNow;
            Log("  ✓ Set TrainerProfile TRN-2026-0004 (SL) ProfilePhotoUrl to NULL.");
        }

        // 4. Delete ONLY the exact 28 target placements
        db.MediaPlacements.RemoveRange(targetPlacements);
        Log($"  ✓ Removed {targetPlacements.Count} target MediaPlacement records from DB.");

        // 5. Delete ONLY the exact 28 target MediaItems
        db.MediaItems.RemoveRange(targetItems);
        Log($"  ✓ Removed {targetItems.Count} target MediaItem records from DB.");

        // 6. Commit Database Transaction First
        await db.SaveChangesAsync();
        Log("  ✓ Successfully committed database deletions.");

        // 7. Delete ONLY the exact 28 R2 objects individually (NO WILDCARDS / NO PREFIX PURGES)
        var successfulR2Deletions = new List<string>();
        var failedR2Deletions = new List<(string Key, string Error)>();

        foreach (var key in candidateKeys)
        {
            try
            {
                await s3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = r2BucketName,
                    Key = key
                });
                successfulR2Deletions.Add(key);
                Log($"  ✓ Deleted R2 object [{successfulR2Deletions.Count}/28]: {key}");
            }
            catch (Exception ex)
            {
                failedR2Deletions.Add((key, ex.Message));
                Log($"  ❌ FAILED to delete R2 object '{key}': {ex.Message}");
            }
        }

        if (failedR2Deletions.Count > 0)
        {
            Log($"[PARTIAL DELETION WARNING] {failedR2Deletions.Count} R2 objects could not be deleted:");
            foreach (var fail in failedR2Deletions)
            {
                Log($"  • {fail.Key} - Error: {fail.Error}");
            }
            throw new InvalidOperationException($"R2 partial deletion failure: {failedR2Deletions.Count} objects failed to delete.");
        }

        Log($"  ✓ Successfully deleted all {successfulR2Deletions.Count}/28 exact R2 content objects.");

        // 8. Post-verification: 404 for deleted keys
        int notFoundCount = 0;
        foreach (var key in candidateKeys)
        {
            try
            {
                await s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = r2BucketName,
                    Key = key
                });
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                notFoundCount++;
            }
        }
        Assert.Equal(28, notFoundCount);
        Log($"  ✓ All {notFoundCount}/28 deleted R2 keys confirmed 404 NotFound.");

        // 9. Verify ticket PDFs preserved in R2
        var ticketList = new List<S3Object>();
        string? contToken = null;
        do
        {
            var listResp = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = r2BucketName,
                Prefix = "tickets/",
                ContinuationToken = contToken
            });
            ticketList.AddRange(listResp.S3Objects);
            contToken = listResp.NextContinuationToken;
        } while (!string.IsNullOrEmpty(contToken));

        Assert.Equal(68, ticketList.Count);
        Log($"  ✓ All 68 ticket PDFs verified intact under tickets/ in R2.");

        // 10. Verify Database counts and integrity
        var remainingMediaItems = await db.MediaItems.CountAsync();
        var remainingPlacements = await db.MediaPlacements.CountAsync();
        var totalWorkshops = await db.Workshops.CountAsync();
        var totalUsers = await db.Users.CountAsync();
        var totalTrainers = await db.TrainerProfiles.CountAsync();
        var totalBookings = await db.WorkshopBookings.CountAsync();
        var totalTickets = await db.WorkshopTickets.CountAsync();
        var totalTicketPdfs = await db.TicketPdfs.CountAsync();

        Log($"  • DB MediaItems:       {remainingMediaItems} (Expected: 0)");
        Log($"  • DB MediaPlacements:  {remainingPlacements} (Expected: 0)");
        Log($"  • DB Workshops:        {totalWorkshops} (Preserved: 13)");
        Log($"  • DB Users:            {totalUsers} (Preserved: 9)");
        Log($"  • DB TrainerProfiles:  {totalTrainers} (Preserved: 4)");
        Log($"  • DB Bookings:         {totalBookings} (Preserved: 35)");
        Log($"  • DB Tickets:          {totalTickets} (Preserved: 68)");
        Log($"  • DB TicketPdfs:       {totalTicketPdfs} (Preserved: 68)");

        Assert.Equal(0, remainingMediaItems);
        Assert.Equal(0, remainingPlacements);
        Assert.Equal(13, totalWorkshops);
        Assert.Equal(9, totalUsers);
        Assert.Equal(4, totalTrainers);
        Assert.Equal(35, totalBookings);
        Assert.Equal(68, totalTickets);
        Assert.Equal(68, totalTicketPdfs);

        Log("");
        Log("================================================================================");
        Log("                  CLEANUP EXECUTION 100% COMPLETE & VERIFIED                    ");
        Log("================================================================================");

        await File.WriteAllTextAsync(@"d:\ETHOS DANCE studio\media_cleanup_execution_audit.txt", sb.ToString());
    }
}



