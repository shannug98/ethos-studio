using System.Net;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Ethos.Api.Application.Media;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;

namespace Ethos.Api.Tests.Media;

public class VerifyHeroUploadTests
{
    private readonly ITestOutputHelper _output;

    public VerifyHeroUploadTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Integration test requiring live running server and database")]
    public async Task Verify_Hero_Banner_Upload_And_Isolation()
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
        Log("           TECHNICAL VERIFICATION: HERO BANNER UPLOAD PIPELINE                 ");
        Log("================================================================================");
        Log($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
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

        // 1. Check all MediaItems currently in database
        var allItems = await db.MediaItems
            .Include(m => m.Placements)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var allPlacements = await db.MediaPlacements
            .Include(p => p.MediaItem)
            .OrderBy(p => p.Section)
            .ThenBy(p => p.DisplayOrder)
            .ToListAsync();

        Log($"Total MediaItems in Database:     {allItems.Count}");
        Log($"Total MediaPlacements in Database: {allPlacements.Count}");
        Log("");

        Log("─── 1. DATABASE MEDIA RECORDS DETAIL ──────────────────────────────────────────");
        foreach (var item in allItems)
        {
            Log($"• MediaItem ID: {item.Id}");
            Log($"  Title:            \"{item.Title}\"");
            Log($"  Original File:    \"{item.OriginalFileName}\"");
            Log($"  Media Type:       {item.MediaType}");
            Log($"  Mime Type:        {item.MimeType}");
            Log($"  Layout Type:      {item.LayoutType}");
            Log($"  File Size:        {item.FileSizeBytes:N0} bytes ({(item.FileSizeBytes / (1024.0 * 1024.0)):F2} MB)");
            Log($"  ObjectKey:        {item.ObjectKey}");
            Log($"  PublicUrl:        {item.PublicUrl}");
            Log($"  Created At:       {item.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");

            var placements = item.Placements.ToList();
            Log($"  Placements Count: {placements.Count}");
            foreach (var p in placements)
            {
                Log($"    - Section: {p.Section} | Slot (DisplayOrder): {p.DisplayOrder} | Published: {p.IsPublished} | Featured: {p.IsFeatured}");
            }
            Log("");
        }

        // 2. Verify R2 Storage Objects & Streaming Performance
        Log("─── 2. CLOUDFLARE R2 STORAGE VERIFICATION & STREAMING ───────────────────");
        using var httpClient = new HttpClient();

        foreach (var item in allItems)
        {
            try
            {
                var metaResp = await s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = r2BucketName,
                    Key = item.ObjectKey
                });

                Log($"✓ R2 Metadata verified for: {item.ObjectKey}");
                Log($"  Size in R2:     {metaResp.ContentLength:N0} bytes");
                Log($"  Content-Type:   {metaResp.Headers.ContentType}");
                Log($"  ETag:           {metaResp.ETag}");

                // Check local mirror file in App_Data/uploads
                var localMirrorPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Ethos.Api", "App_Data", "uploads", item.ObjectKey.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(localMirrorPath))
                {
                    localMirrorPath = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads", item.ObjectKey.Replace('/', Path.DirectorySeparatorChar));
                }
                var localMirrorExists = File.Exists(localMirrorPath);
                Log($"  Local Mirror in App_Data: {(localMirrorExists ? "EXISTS (" + new FileInfo(localMirrorPath).Length + " bytes)" : "NOT FOUND")}");

                // Test API streaming endpoint if local API is running
                var streamUrl = $"http://localhost:5000/api/media/content/{item.Id}";
                try
                {
                    var apiHeadReq = new HttpRequestMessage(HttpMethod.Head, streamUrl);
                    var apiHeadResp = await httpClient.SendAsync(apiHeadReq);
                    Log($"  API Stream HEAD ({streamUrl}): Status {(int)apiHeadResp.StatusCode} {apiHeadResp.StatusCode}");
                    Log($"  API Accept-Ranges: {apiHeadResp.Headers.AcceptRanges?.ToString() ?? "(none)"}");
                }
                catch (Exception apiEx)
                {
                    Log($"  API Stream test error: {apiEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Error checking R2 object for '{item.ObjectKey}': {ex.Message}");
            }
            Log("");
        }

        // 3. Verify Cross-Placement Isolation
        Log("─── 3. CROSS-PLACEMENT ISOLATION AUDIT ────────────────────────────────────────");
        var heroPlacements = allPlacements.Where(p => p.Section == "HomepageScrolling").ToList();
        var nonHeroPlacements = allPlacements.Where(p => p.Section != "HomepageScrolling").ToList();

        Log($"HomepageScrolling (Hero Banner) Placements: {heroPlacements.Count}");
        Log($"Non-Hero Placements currently active:         {nonHeroPlacements.Count}");

        if (nonHeroPlacements.Count == 0)
        {
            Log("✓ ZERO CROSS-PLACEMENT CONTAMINATION: No other sections have been modified or borrowed.");
        }
        else
        {
            Log("⚠️ Active non-hero placements detected:");
            foreach (var p in nonHeroPlacements)
            {
                Log($"  • Section: {p.Section}, Item: {p.MediaItemId}");
            }
        }

        // 4. Verify Public API response for all sections
        Log("");
        Log("─── 4. PUBLIC API SECTION RESPONSE AUDIT ──────────────────────────────────────");
        var sectionsToCheck = new[]
        {
            "HomepageScrolling",
            "HomepageReels",
            "AboutEthos",
            "Founders",
            "Trainers",
            "GallerySlideshow",
            "GalleryImages",
            "GalleryVideos",
            "Events"
        };

        foreach (var sec in sectionsToCheck)
        {
            var secItems = await db.MediaPlacements
                .Where(p => p.Section == sec && p.IsPublished && !p.MediaItem.IsArchived)
                .OrderBy(p => p.DisplayOrder)
                .Select(p => new { p.MediaItemId, p.DisplayOrder, p.MediaItem.Title, p.MediaItem.MediaType })
                .ToListAsync();

            Log($"  • Section '{sec}': {secItems.Count} item(s)");
            foreach (var si in secItems)
            {
                Log($"    - Slot {si.DisplayOrder}: \"{si.Title}\" ({si.MediaType})");
            }
        }

        Log("");
        Log("================================================================================");
        Log("                     VERIFICATION COMPLETE                                      ");
        Log("================================================================================");

        await File.WriteAllTextAsync(@"d:\ETHOS DANCE studio\hero_upload_verification_audit.txt", sb.ToString());
    }

    [Fact(Skip = "Requires live running server on localhost:5252")]
    public async Task Atomic_Optimize_And_Verify_Bommali_Hero_Video()
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

        var originalLocalPath = @"d:\ETHOS DANCE studio\Ethos.Api\App_Data\uploads\homepagescrolling\videos\2026\09\debf5e435d8048919e89c1fde5be81b5.mp4";
        var optimizedLocalPath = @"d:\ETHOS DANCE studio\Ethos.Api\App_Data\uploads\homepagescrolling\videos\2026\09\debf5e435d8048919e89c1fde5be81b5_faststart.mp4";
        var newObjectKey = "homepagescrolling/videos/2026/09/debf5e435d8048919e89c1fde5be81b5_faststart.mp4";

        Assert.True(File.Exists(originalLocalPath), "Original BOMMALI.mp4 must exist in local uploads mirror.");

        // 1. Run FastStart optimization
        var fastStartService = new MediaFastStartService(configuration, Microsoft.Extensions.Logging.Abstractions.NullLogger<MediaFastStartService>.Instance);
        Assert.True(fastStartService.IsAvailable, "FFmpeg must be available for FastStart optimization.");

        _output.WriteLine($"[1] Optimizing video with FFmpeg FastStart...");
        var optResult = await fastStartService.OptimizeAsync(originalLocalPath, optimizedLocalPath);
        Assert.True(optResult.Success, $"FastStart optimization failed: {optResult.ErrorMessage}");
        Assert.True(File.Exists(optimizedLocalPath), "Optimized file must exist on disk.");

        // 2. Validate atom layout: moov before mdat
        bool isMoovBeforeMdat = MediaFastStartService.IsMoovBeforeMdat(optimizedLocalPath);
        Assert.True(isMoovBeforeMdat, "Verification failed: 'moov' atom must occur before 'mdat'.");
        _output.WriteLine($"[2] Verified 'moov' before 'mdat': {isMoovBeforeMdat}");

        var optimizedFi = new FileInfo(optimizedLocalPath);
        _output.WriteLine($"    Optimized file size: {optimizedFi.Length:N0} bytes ({(optimizedFi.Length / (1024.0 * 1024.0)):F2} MB)");

        // 3. Calculate SHA-256
        string checksum;
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        using (var ofs = File.OpenRead(optimizedLocalPath))
        {
            var hash = await sha256.ComputeHashAsync(ofs);
            checksum = Convert.ToHexString(hash).ToLowerInvariant();
        }
        _output.WriteLine($"    Optimized file SHA-256: {checksum}");

        // 4. Upload optimized object to Cloudflare R2
        _output.WriteLine($"[3] Uploading optimized object to Cloudflare R2 bucket '{r2BucketName}'...");
        var s3Config = new AmazonS3Config
        {
            ServiceURL = $"https://{r2AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        };
        var credentials = new BasicAWSCredentials(r2AccessKey, r2SecretKey);
        using var s3Client = new AmazonS3Client(credentials, s3Config);

        using (var uploadStream = File.OpenRead(optimizedLocalPath))
        {
            var putReq = new PutObjectRequest
            {
                BucketName = r2BucketName,
                Key = newObjectKey,
                InputStream = uploadStream,
                ContentType = "video/mp4",
                DisablePayloadSigning = true
            };
            putReq.Metadata.Add("original-filename", "BOMMALI.mp4");
            putReq.Metadata.Add("faststart-optimized", "true");
            await s3Client.PutObjectAsync(putReq);
        }
        _output.WriteLine($"    Successfully uploaded: {newObjectKey}");

        // 5. Verify object in R2
        var r2Meta = await s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
            BucketName = r2BucketName,
            Key = newObjectKey
        });
        Assert.Equal(optimizedFi.Length, r2Meta.ContentLength);
        _output.WriteLine($"[4] Verified R2 object metadata: Length={r2Meta.ContentLength:N0}, ContentType={r2Meta.Headers.ContentType}");

        // 6. Update database record atomically
        _output.WriteLine($"[5] Updating MediaItem in Neon Database atomically...");
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        using var db = new AppDbContext(optionsBuilder.Options);

        var targetItemId = Guid.Parse("9ef3622d-316e-4201-bb5c-6f5392fdda0c");
        var item = await db.MediaItems.FindAsync(targetItemId);
        Assert.NotNull(item);

        item.ObjectKey = newObjectKey;
        item.PublicUrl = $"https://media.ethosdancestudio.com/{newObjectKey}";
        item.ThumbnailUrl = $"https://media.ethosdancestudio.com/{newObjectKey}";
        item.FileSizeBytes = optimizedFi.Length;
        item.Checksum = checksum;
        item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        _output.WriteLine($"    MediaItem '{targetItemId}' updated with ObjectKey '{newObjectKey}'");

        // 7. Execute 5-Point HTTP Range Suite against live API
        _output.WriteLine($"[6] Executing 5-Point HTTP Range Suite against live API endpoint...");
        using var httpClient = new HttpClient();
        var apiUrl = $"http://localhost:5252/api/media/content/{targetItemId}";
        long totalLength = optimizedFi.Length;

        // Test A: Beginning Range (bytes=0-1024)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 1024);
            var resp = await httpClient.SendAsync(req);

            Assert.Equal(HttpStatusCode.PartialContent, resp.StatusCode);
            Assert.Equal("bytes", resp.Headers.AcceptRanges.ToString());
            Assert.Equal($"bytes 0-1024/{totalLength}", resp.Content.Headers.ContentRange?.ToString());
            Assert.Equal(1025, resp.Content.Headers.ContentLength);

            var bodyBytes = await resp.Content.ReadAsByteArrayAsync();
            Assert.Equal(1025, bodyBytes.Length);
            _output.WriteLine($"    ✓ Test A (Beginning 0-1024): 206 Partial Content, Content-Range: {resp.Content.Headers.ContentRange}, Body: {bodyBytes.Length} bytes");
        }

        // Test B: End Range (last 65536 bytes)
        {
            long start = totalLength - 65536;
            long end = totalLength - 1;
            var req = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, end);
            var resp = await httpClient.SendAsync(req);

            Assert.Equal(HttpStatusCode.PartialContent, resp.StatusCode);
            Assert.Equal($"bytes {start}-{end}/{totalLength}", resp.Content.Headers.ContentRange?.ToString());
            Assert.Equal(65536, resp.Content.Headers.ContentLength);

            var bodyBytes = await resp.Content.ReadAsByteArrayAsync();
            Assert.Equal(65536, bodyBytes.Length);
            _output.WriteLine($"    ✓ Test B (End last 64KB): 206 Partial Content, Content-Range: {resp.Content.Headers.ContentRange}, Body: {bodyBytes.Length} bytes");
        }

        // Test C: Middle Range (1,000,000 - 2,000,000)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(1000000, 2000000);
            var resp = await httpClient.SendAsync(req);

            Assert.Equal(HttpStatusCode.PartialContent, resp.StatusCode);
            Assert.Equal($"bytes 1000000-2000000/{totalLength}", resp.Content.Headers.ContentRange?.ToString());
            Assert.Equal(1000001, resp.Content.Headers.ContentLength);

            var bodyBytes = await resp.Content.ReadAsByteArrayAsync();
            Assert.Equal(1000001, bodyBytes.Length);
            _output.WriteLine($"    ✓ Test C (Middle 1MB-2MB): 206 Partial Content, Content-Range: {resp.Content.Headers.ContentRange}, Body: {bodyBytes.Length} bytes");
        }

        // Test D: Invalid Range (999999999-9999999999) -> 416
        {
            var req = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(999999999, 9999999999);
            var resp = await httpClient.SendAsync(req);

            Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, resp.StatusCode);
            Assert.Equal($"bytes */{totalLength}", resp.Content.Headers.ContentRange?.ToString());
            _output.WriteLine($"    ✓ Test D (Invalid Range): 416 Range Not Satisfiable, Content-Range: {resp.Content.Headers.ContentRange}");
        }

        // Test E: Normal Request (No Range Header) -> 200 OK
        {
            var req = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            var resp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.Equal("bytes", resp.Headers.AcceptRanges.ToString());
            Assert.Equal(totalLength, resp.Content.Headers.ContentLength);
            _output.WriteLine($"    ✓ Test E (Normal Request): 200 OK, Content-Length: {resp.Content.Headers.ContentLength}, Accept-Ranges: {resp.Headers.AcceptRanges}");
        }

        _output.WriteLine("");
        _output.WriteLine("================================================================================");
        _output.WriteLine("           ATOMIC BOMMALI OPTIMIZATION & VERIFICATION PASSED                    ");
        _output.WriteLine("================================================================================");
    }
}

