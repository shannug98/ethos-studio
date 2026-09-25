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

public class InspectTrainerSLTests
{
    private readonly ITestOutputHelper _output;

    public InspectTrainerSLTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Trainer SL (TRN-2026-0004) was test data removed during Phase C pre-production reset")]
    public async Task Query_Trainer_SL_Details_And_Download_Photo()
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

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        using var db = new AppDbContext(optionsBuilder.Options);

        var trainer = await db.TrainerProfiles
            .Include(t => t.CurrentTier)
            .Include(t => t.Workshops)
            .Include(t => t.Applications)
            .FirstOrDefaultAsync(t => t.TrainerCode == "TRN-2026-0004");

        Assert.NotNull(trainer);

        var user = await db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == trainer.UserId);

        _output.WriteLine("================================================================================");
        _output.WriteLine("                     TRAINER PROFILE: TRN-2026-0004                             ");
        _output.WriteLine("================================================================================");
        _output.WriteLine($"Profile ID:             {trainer.Id}");
        _output.WriteLine($"Trainer Code:           {trainer.TrainerCode}");
        _output.WriteLine($"Full Name:              {trainer.FullName}");
        _output.WriteLine($"City:                   {trainer.City ?? "(empty)"}");
        _output.WriteLine($"Primary Dance Style:    {trainer.PrimaryDanceStyle ?? "(empty)"}");
        _output.WriteLine($"Secondary Styles:       {trainer.SecondaryDanceStyles ?? "(empty)"}");
        _output.WriteLine($"Experience (Years):     {trainer.ExperienceYears?.ToString() ?? "(empty)"}");
        _output.WriteLine($"Current Studio:         {trainer.CurrentStudio ?? "(empty)"}");
        _output.WriteLine($"Bio:                    {trainer.Bio ?? "(empty)"}");
        _output.WriteLine($"Instagram URL:          {trainer.InstagramUrl ?? "(empty)"}");
        _output.WriteLine($"YouTube URL:            {trainer.YouTubeUrl ?? "(empty)"}");
        _output.WriteLine($"Status:                 {trainer.Status}");
        _output.WriteLine($"Current Tier:           {trainer.CurrentTier?.Name ?? "(none)"} (Tier Code: {trainer.CurrentTier?.Code ?? "N/A"})");
        _output.WriteLine($"Approved At:            {trainer.ApprovedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(not approved)"}");
        _output.WriteLine($"Created At:             {trainer.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"Updated At:             {trainer.UpdatedAt:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"Profile Photo URL:      {trainer.ProfilePhotoUrl ?? "(none)"}");
        _output.WriteLine($"Assigned Workshops:     {trainer.Workshops.Count}");
        _output.WriteLine($"Applications Count:     {trainer.Applications.Count}");
        _output.WriteLine("");

        _output.WriteLine("─── ASSOCIATED USER RECORD ─────────────────────────────────────────────────────");
        _output.WriteLine($"User ID:                {user?.Id}");
        _output.WriteLine($"Customer Code:          {user?.CustomerCode}");
        _output.WriteLine($"User Full Name:         {user?.FullName}");
        _output.WriteLine($"Phone Number:           {user?.Phone}");
        _output.WriteLine($"Email Address:          {user?.Email ?? "(none)"}");
        _output.WriteLine($"Is Active:              {user?.IsActive}");
        _output.WriteLine($"User Created At:        {user?.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"User Last Login At:     {user?.LastLoginAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(never)"}");
        _output.WriteLine($"Failed Login Count:     {user?.FailedLoginCount}");
        _output.WriteLine($"User Roles Count:       {user?.UserRoles.Count}");

        // Also check if any MediaItem references this
        var mediaItem = await db.MediaItems.FirstOrDefaultAsync(m => m.ObjectKey == "trainers/images/2026/09/f20bea41d7d24b438bf46f83caf9a6c4.jpg");
        _output.WriteLine("");
        _output.WriteLine("─── ASSOCIATED MEDIA ITEM RECORD ───────────────────────────────────────────────");
        _output.WriteLine($"MediaItem ID:           {mediaItem?.Id}");
        _output.WriteLine($"MediaItem Title:        {mediaItem?.Title}");
        _output.WriteLine($"Original File Name:     {mediaItem?.OriginalFileName}");
        _output.WriteLine($"Mime Type:              {mediaItem?.MimeType}");
        _output.WriteLine($"Layout Type:            {mediaItem?.LayoutType}");
        _output.WriteLine($"Created At:             {mediaItem?.CreatedAt:yyyy-MM-dd HH:mm:ss}");

        // Download photo from R2 to artifact directory
        var s3Config = new AmazonS3Config
        {
            ServiceURL = $"https://{r2AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        };
        var credentials = new BasicAWSCredentials(r2AccessKey, r2SecretKey);
        using var s3Client = new AmazonS3Client(credentials, s3Config);

        var artifactDir = @"C:\Users\yuvar\.gemini\antigravity\brain\a1b1c899-ff0c-4ed7-bf8f-fa9cce9626e9";
        var photoPath = Path.Combine(artifactDir, "trainer_sl_profile_photo.jpg");

        try
        {
            var getResp = await s3Client.GetObjectAsync(r2BucketName, "trainers/images/2026/09/f20bea41d7d24b438bf46f83caf9a6c4.jpg");
            using var fileStream = File.Create(photoPath);
            await getResp.ResponseStream.CopyToAsync(fileStream);
            _output.WriteLine("");
            _output.WriteLine($"✓ Successfully downloaded profile photo to: {photoPath} (Size: {new FileInfo(photoPath).Length} bytes)");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"⚠️ Could not download photo from R2: {ex.Message}");
        }
    }
}
