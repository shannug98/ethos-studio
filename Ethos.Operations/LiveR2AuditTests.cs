using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace Ethos.Api.Tests.Media;

public class LiveR2AuditTests
{
    private readonly ITestOutputHelper _output;

    public LiveR2AuditTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Audit_Live_R2_Bucket_Objects()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets("ethos-dance-studio-neon-production-2026")
            .AddEnvironmentVariables()
            .Build();

        var r2AccountId = configuration["CloudflareR2:AccountId"] ?? "253931def0adecdc526c3786262561be";
        var r2AccessKey = configuration["CloudflareR2:AccessKeyId"] ?? "a433147a9ab57c9cdb428e6f4659aa09";
        var r2SecretKey = configuration["CloudflareR2:SecretAccessKey"] ?? "694b08d571b5ef7656b9d2bf89d94666b3ae671e81d78558e9d0e0ed23010f4b";
        var r2BucketName = configuration["CloudflareR2:BucketName"] ?? "ethos-production-media";

        var credentials = new BasicAWSCredentials(r2AccessKey, r2SecretKey);
        var s3Config = new AmazonS3Config
        {
            ServiceURL = $"https://{r2AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        };

        using var s3Client = new AmazonS3Client(credentials, s3Config);

        var sb = new StringBuilder();
        void Log(string line)
        {
            _output.WriteLine(line);
            sb.AppendLine(line);
        }

        Log("================================================================================");
        Log("               LIVE CLOUDFLARE R2 BUCKET INVENTORY AUDIT                       ");
        Log("================================================================================");
        Log($"Timestamp:    {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Log($"Bucket Name:  {r2BucketName}");
        Log($"Endpoint:     {s3Config.ServiceURL}");
        Log("");

        // 1. Full unpaginated listing of the entire bucket
        var allObjects = new List<S3Object>();
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
                allObjects.AddRange(resp.S3Objects);
            }
            contToken = resp.NextContinuationToken;
        } while (!string.IsNullOrEmpty(contToken));

        Log($"TOTAL OBJECTS RETURNED BY S3 API: {allObjects.Count}");
        var totalBytes = allObjects.Sum(o => o.Size);
        Log($"TOTAL STORAGE SIZE: {totalBytes:N0} bytes ({totalBytes / (1024.0 * 1024.0):F2} MiB / {totalBytes / 1000000.0:F2} MB)");
        Log("");

        // 2. Breakdown by top-level prefix
        var prefixes = allObjects
            .GroupBy(o => o.Key.Split('/')[0])
            .OrderBy(g => g.Key)
            .ToList();

        Log("--- TOP-LEVEL FOLDERS / PREFIXES PRESENT IN BUCKET ---");
        foreach (var group in prefixes)
        {
            var groupBytes = group.Sum(o => o.Size);
            Log($"  📁 {group.Key}/ -> {group.Count()} objects ({groupBytes:N0} bytes, {groupBytes / (1024.0 * 1024.0):F2} MiB)");
        }
        Log("");

        // 3. Itemize every single object in the bucket
        Log("--- COMPLETE ITEMIZATION OF EVERY OBJECT CURRENTLY IN BUCKET ---");
        int index = 1;
        foreach (var obj in allObjects.OrderBy(o => o.Key))
        {
            Log($"  {index++,2}. Key:  {obj.Key}");
            Log($"      Size: {obj.Size:N0} bytes ({obj.Size / (1024.0 * 1024.0):F2} MiB)");
            Log($"      ETag: {obj.ETag} | LastModified: {obj.LastModified:yyyy-MM-dd HH:mm:ss} UTC");
        }
        Log("");

        // 4. Check for test folders that were targeted for deletion
        var deletedPrefixes = new[] { "tickets/", "workshops/", "trainers/", "galleryimages/" };
        Log("--- VERIFYING ABSENCE OF DELETED TEST PREFIXES ---");
        foreach (var dp in deletedPrefixes)
        {
            var count = allObjects.Count(o => o.Key.StartsWith(dp, StringComparison.OrdinalIgnoreCase));
            Log($"  • {dp,-20} -> {count} objects remaining (Must be 0)");
            Assert.Equal(0, count);
        }

        // 5. Check for deleted raw non-faststart BOMMALI video
        var rawBommaliKey = "homepagescrolling/videos/2026/09/debf5e435d8048919e89c1fde5be81b5.mp4";
        var rawBommaliPresent = allObjects.Any(o => o.Key == rawBommaliKey);
        Log($"  • Raw non-faststart video ({rawBommaliKey}) -> {(rawBommaliPresent ? "PRESENT (UNEXPECTED)" : "DELETED (CORRECT)")}");
        Assert.False(rawBommaliPresent);

        Log("");
        Log("================================================================================");
        Log($"AUDIT CONCLUSION: The bucket contains EXACTLY {allObjects.Count} objects ({totalBytes:N0} bytes).");
        Log("All objects are completely absent from the bucket (Clean Slate verified: 0 objects, 0 bytes).");
        Log("================================================================================");
        Assert.Empty(allObjects);
        Assert.Equal(0, totalBytes);

        var reportPath = @"d:\ETHOS DANCE studio\live_r2_audit_evidence.txt";
        await File.WriteAllTextAsync(reportPath, sb.ToString());
    }
}
