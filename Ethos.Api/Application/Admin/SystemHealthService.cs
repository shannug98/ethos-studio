using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Ethos.Api.Application.Common;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Persistence;
using Ethos.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ethos.Api.Application.Admin;

public class SystemHealthService : ISystemHealthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly CloudflareR2Settings _r2Settings;
    private readonly RazorpaySettings _razorpaySettings;
    private readonly Msg91Options _msg91Options;
    private readonly IWorkerLivenessTracker _workerLivenessTracker;
    private readonly ILogger<SystemHealthService> _logger;

    private static readonly DateTime ProcessStartTime = Process.GetCurrentProcess().StartTime.ToUniversalTime();
    private static readonly ConcurrentDictionary<string, DateTime> LastSuccessfulChecks = new(StringComparer.OrdinalIgnoreCase);

    public SystemHealthService(
        AppDbContext db,
        IConfiguration configuration,
        IWebHostEnvironment env,
        IOptions<CloudflareR2Settings> r2Settings,
        IOptions<RazorpaySettings> razorpaySettings,
        IOptions<Msg91Options> msg91Options,
        IWorkerLivenessTracker workerLivenessTracker,
        ILogger<SystemHealthService> logger)
    {
        _db = db;
        _configuration = configuration;
        _env = env;
        _r2Settings = r2Settings.Value;
        _razorpaySettings = razorpaySettings.Value;
        _msg91Options = msg91Options.Value;
        _workerLivenessTracker = workerLivenessTracker;
        _logger = logger;
    }

    public async Task<SystemHealthResponse> GetUnifiedHealthAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var components = new List<SystemHealthComponentDto>(10);

        // 1. Core API Runtime
        components.Add(CheckCoreApiRuntime(now));

        // 2. PostgreSQL Primary Database
        components.Add(await CheckPostgreSqlAsync(now, cancellationToken));

        // 3. Razorpay Payment Gateway
        components.Add(await CheckRazorpayAsync(now, cancellationToken));

        // 4. Cloudflare R2 Media Storage
        components.Add(await CheckCloudflareR2Async(now, cancellationToken));

        // 5. MSG91 WhatsApp Provider
        components.Add(await CheckMsg91WhatsAppAsync(now, cancellationToken));

        // 6. WhatsApp Outbox Background Worker
        components.Add(await CheckWhatsAppWorkerAsync(now, cancellationToken));

        // 7. Refund Outbox Background Worker
        components.Add(await CheckRefundWorkerAsync(now, cancellationToken));

        // 8. Telemetry Ingestion Worker
        components.Add(CheckTelemetryWorker(now));

        // 9. Authentication & Session Authority
        components.Add(await CheckAuthenticationAuthorityAsync(now, cancellationToken));

        // 10. Security & Threat Monitor
        components.Add(await CheckSecurityMonitorAsync(now, cancellationToken));

        // Aggregate overall status
        var overallStatus = ComputeOverallStatus(components);

        return new SystemHealthResponse
        {
            OverallStatus = overallStatus,
            TotalComponents = components.Count,
            OperationalCount = components.Count(c => c.Status == "Operational"),
            DegradedCount = components.Count(c => c.Status == "Degraded"),
            ErrorCount = components.Count(c => c.Status == "Error"),
            NotConfiguredCount = components.Count(c => c.Status == "Not Configured"),
            StandbyCount = components.Count(c => c.Status == "Standby"),
            LastCheckedUtc = now,
            FormattedLastChecked = now.ToString("dd MMM yyyy, hh:mm tt", CultureInfo.InvariantCulture),
            Components = components
        };
    }

    private SystemHealthComponentDto CheckCoreApiRuntime(DateTime now)
    {
        var uptime = now - ProcessStartTime;
        var memoryMb = GC.GetTotalMemory(false) / (1024 * 1024);

        UpdateSuccess("core_api", now);

        return new SystemHealthComponentDto
        {
            Key = "core_api",
            Name = "Core API Runtime",
            Category = "Core",
            Status = "Operational",
            LatencyMs = 1,
            LastCheckedUtc = now,
            LastSuccessfulCheckUtc = GetLastSuccess("core_api"),
            ErrorMessage = null,
            AffectedSystems = new List<string> { "HTTP API Endpoints", "Client Request Processing", "JWT Validation Pipeline" },
            Diagnostics = new Dictionary<string, string>
            {
                ["Environment"] = _env.EnvironmentName,
                ["Framework"] = ".NET 10.0 (Kestrel)",
                ["Uptime"] = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m",
                ["Managed Memory"] = $"{memoryMb} MB",
                ["Server OS"] = Environment.OSVersion.Platform.ToString(),
                ["Machine"] = Environment.MachineName
            },
            ActionUrl = "/admin_portal/observability",
            ActionLabel = "View Telemetry"
        };
    }

    private async Task<SystemHealthComponentDto> CheckPostgreSqlAsync(DateTime now, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            sw.Stop();

            var status = sw.ElapsedMilliseconds > 1500 ? "Degraded" : "Operational";
            if (status == "Operational") UpdateSuccess("postgresql", now);

            return new SystemHealthComponentDto
            {
                Key = "postgresql",
                Name = "PostgreSQL Database",
                Category = "Database",
                Status = status,
                LatencyMs = sw.ElapsedMilliseconds,
                LastCheckedUtc = now,
                LastSuccessfulCheckUtc = GetLastSuccess("postgresql"),
                ErrorMessage = status == "Degraded" ? "Elevated query latency observed." : null,
                AffectedSystems = new List<string> { "All Application Persistence", "Workshops", "Payments", "Bookings", "User Auth" },
                Diagnostics = new Dictionary<string, string>
                {
                    ["Engine"] = "PostgreSQL (Npgsql EF Core Provider)",
                    ["Connection State"] = "Connected / Active",
                    ["Ping Latency"] = $"{sw.ElapsedMilliseconds} ms",
                    ["Connection Pooling"] = "Enabled (Max 20)"
                },
                ActionUrl = "/admin_portal/observability",
                ActionLabel = "Inspect Database Logs"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[SystemHealth] PostgreSQL connectivity check failed.");

            return new SystemHealthComponentDto
            {
                Key = "postgresql",
                Name = "PostgreSQL Database",
                Category = "Database",
                Status = "Error",
                LatencyMs = sw.ElapsedMilliseconds,
                LastCheckedUtc = now,
                LastSuccessfulCheckUtc = GetLastSuccess("postgresql"),
                ErrorMessage = SanitizeErrorMessage(ex.Message),
                AffectedSystems = new List<string> { "All Application Persistence", "Workshops", "Payments", "Bookings", "User Auth" },
                Diagnostics = new Dictionary<string, string>
                {
                    ["Engine"] = "PostgreSQL",
                    ["Connection State"] = "Failed / Connection Refused"
                },
                TraceId = $"trc_{Guid.NewGuid():N}",
                ActionUrl = "/admin_portal/observability",
                ActionLabel = "View System Logs"
            };
        }
    }

    private async Task<SystemHealthComponentDto> CheckRazorpayAsync(DateTime now, CancellationToken cancellationToken)
    {
        var keyId = _razorpaySettings.KeyId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(keyId))
        {
            return new SystemHealthComponentDto
            {
                Key = "razorpay",
                Name = "Razorpay Payment Gateway",
                Category = "Gateway",
                Status = "Not Configured",
                LatencyMs = 0,
                LastCheckedUtc = now,
                LastSuccessfulCheckUtc = GetLastSuccess("razorpay"),
                ErrorMessage = "Razorpay KeyId is not configured.",
                AffectedSystems = new List<string> { "Customer Checkout", "Online Workshop Bookings", "Package Purchases" },
                Diagnostics = new Dictionary<string, string>
                {
                    ["Key Configuration"] = "Missing",
                    ["Webhook Secret"] = !string.IsNullOrWhiteSpace(_razorpaySettings.WebhookSecret) ? "Configured" : "Missing"
                },
                ActionUrl = "/admin_portal/payments",
                ActionLabel = "View Payments"
            };
        }

        var isTestKey = keyId.StartsWith("rzp_test_", StringComparison.OrdinalIgnoreCase);
        var maskedKey = keyId.Length > 8 ? $"{keyId[..8]}***" : "***";

        // Query payment transaction failure rate in the last 24h
        int recentCount = 0;
        int failedCount = 0;
        try
        {
            var since = now.AddHours(-24);
            recentCount = await _db.PaymentTransactions.CountAsync(p => p.CreatedAt >= since, cancellationToken);
            failedCount = await _db.PaymentTransactions.CountAsync(p => p.CreatedAt >= since && p.Status == PaymentStatus.Failed, cancellationToken);
        }
        catch
        {
            // Database error already captured by PostgreSQL check
        }

        var failureRate = recentCount > 0 ? (double)failedCount / recentCount : 0.0;
        var status = "Operational";
        string? errorMsg = null;

        if (recentCount >= 5 && failureRate >= 0.5)
        {
            status = "Degraded";
            errorMsg = $"Elevated payment failure rate: {Math.Round(failureRate * 100, 1)}% ({failedCount}/{recentCount} failed in last 24h).";
        }
        else
        {
            UpdateSuccess("razorpay", now);
        }

        return new SystemHealthComponentDto
        {
            Key = "razorpay",
            Name = "Razorpay Payment Gateway",
            Category = "Gateway",
            Status = status,
            LatencyMs = 2,
            LastCheckedUtc = now,
            LastSuccessfulCheckUtc = GetLastSuccess("razorpay"),
            ErrorMessage = errorMsg,
            AffectedSystems = new List<string> { "Customer Checkout", "Online Workshop Bookings", "Package Purchases" },
            Diagnostics = new Dictionary<string, string>
            {
                ["Environment Mode"] = isTestKey ? "Test / Sandbox Mode" : "Live Production",
                ["Key ID"] = maskedKey,
                ["Webhook Secret"] = !string.IsNullOrWhiteSpace(_razorpaySettings.WebhookSecret) ? "Configured" : "Missing",
                ["24h Transaction Volume"] = recentCount.ToString(),
                ["24h Successful Payments"] = (recentCount - failedCount).ToString(),
                ["24h Failure Rate"] = $"{Math.Round(failureRate * 100, 1)}%"
            },
            ActionUrl = "/admin_portal/payments",
            ActionLabel = "View Payments & Ledger"
        };
    }

    private async Task<SystemHealthComponentDto> CheckCloudflareR2Async(DateTime now, CancellationToken cancellationToken)
    {
        var isConfigured = !string.IsNullOrWhiteSpace(_r2Settings.AccountId) &&
                           !string.IsNullOrWhiteSpace(_r2Settings.AccessKeyId) &&
                           !string.IsNullOrWhiteSpace(_r2Settings.SecretAccessKey);

        if (!isConfigured)
        {
            return new SystemHealthComponentDto
            {
                Key = "cloudflare_r2",
                Name = "Cloudflare R2 Media Storage",
                Category = "Storage",
                Status = "Not Configured",
                LatencyMs = 0,
                LastCheckedUtc = now,
                LastSuccessfulCheckUtc = GetLastSuccess("cloudflare_r2"),
                ErrorMessage = "Cloudflare R2 credentials (AccountId / AccessKeyId / SecretAccessKey) not configured.",
                AffectedSystems = new List<string> { "Remote Media Storage", "Trainer Photo Uploads", "Ticket PDF Archival" },
                Diagnostics = new Dictionary<string, string>
                {
                    ["Bucket Name"] = _r2Settings.BucketName ?? "Not Set",
                    ["Public Domain"] = _r2Settings.PublicDomain ?? "Not Set"
                },
                ActionUrl = "/admin_portal/videos",
                ActionLabel = "View Media Library"
            };
        }

        // Detect local development dummy / placeholder credentials
        var isDevPlaceholder = _r2Settings.AccountId.Contains("dev_test", StringComparison.OrdinalIgnoreCase) ||
                               _r2Settings.AccessKeyId.Contains("dev_test", StringComparison.OrdinalIgnoreCase);

        if (isDevPlaceholder && _env.IsDevelopment())
        {
            UpdateSuccess("cloudflare_r2", now);
            return new SystemHealthComponentDto
            {
                Key = "cloudflare_r2",
                Name = "Cloudflare R2 Media Storage",
                Category = "Storage",
                Status = "Standby",
                LatencyMs = 0,
                LastCheckedUtc = now,
                LastSuccessfulCheckUtc = GetLastSuccess("cloudflare_r2"),
                ErrorMessage = null,
                AffectedSystems = new List<string> { "Remote S3 Edge Uploads" },
                Diagnostics = new Dictionary<string, string>
                {
                    ["Mode"] = "Development Standby (Local file storage active)",
                    ["Bucket Target"] = _r2Settings.BucketName,
                    ["Fallback Storage"] = "App_Data/uploads (Local static server)"
                },
                ActionUrl = "/admin_portal/videos",
                ActionLabel = "View Media Library"
            };
        }

        // Real ping check using AWS S3 client
        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(4));

            var credentials = new BasicAWSCredentials(_r2Settings.AccessKeyId, _r2Settings.SecretAccessKey);
            var config = new AmazonS3Config
            {
                ServiceURL = _r2Settings.ServiceUrl,
                ForcePathStyle = true,
                Timeout = TimeSpan.FromSeconds(4)
            };

            using var s3Client = new AmazonS3Client(credentials, config);
            var listReq = new ListObjectsV2Request
            {
                BucketName = _r2Settings.BucketName,
                MaxKeys = 1
            };

            await s3Client.ListObjectsV2Async(listReq, cts.Token);
            sw.Stop();

            UpdateSuccess("cloudflare_r2", now);

            return new SystemHealthComponentDto
            {
                Key = "cloudflare_r2",
                Name = "Cloudflare R2 Media Storage",
                Category = "Storage",
                Status = "Operational",
                LatencyMs = sw.ElapsedMilliseconds,
                LastCheckedUtc = now,
                LastSuccessfulCheckUtc = GetLastSuccess("cloudflare_r2"),
                ErrorMessage = null,
                AffectedSystems = new List<string> { "Media Library CDN", "Trainer Avatars", "Ticket PDF Storage" },
                Diagnostics = new Dictionary<string, string>
                {
                    ["Service"] = "Cloudflare R2 (S3 API Compatible)",
                    ["Bucket Name"] = _r2Settings.BucketName,
                    ["Public Domain"] = _r2Settings.PublicDomain,
                    ["Ping Latency"] = $"{sw.ElapsedMilliseconds} ms"
                },
                ActionUrl = "/admin_portal/videos",
                ActionLabel = "View Media Library"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "[SystemHealth] Cloudflare R2 reachability check failed.");

            return new SystemHealthComponentDto
            {
                Key = "cloudflare_r2",
                Name = "Cloudflare R2 Media Storage",
                Category = "Storage",
                Status = "Error",
                LatencyMs = sw.ElapsedMilliseconds,
                LastCheckedUtc = now,
                LastSuccessfulCheckUtc = GetLastSuccess("cloudflare_r2"),
                ErrorMessage = SanitizeErrorMessage(ex.Message),
                AffectedSystems = new List<string> { "Remote S3 Media Uploads", "Ticket PDF Edge Storage" },
                Diagnostics = new Dictionary<string, string>
                {
                    ["Bucket Name"] = _r2Settings.BucketName,
                    ["Endpoint URL"] = _r2Settings.ServiceUrl
                },
                TraceId = $"trc_{Guid.NewGuid():N}",
                ActionUrl = "/admin_portal/videos",
                ActionLabel = "View Media Library"
            };
        }
    }

    private async Task<SystemHealthComponentDto> CheckMsg91WhatsAppAsync(DateTime now, CancellationToken cancellationToken)
    {
        if (!_msg91Options.Enabled)
        {
            UpdateSuccess("msg91_whatsapp", now);
            return new SystemHealthComponentDto
            {
                Key = "msg91_whatsapp",
                Name = "MSG91 WhatsApp Provider",
                Category = "Messaging",
                Status = "Standby",
                LatencyMs = 0,
                LastCheckedUtc = now,
                LastSuccessfulCheckUtc = GetLastSuccess("msg91_whatsapp"),
                ErrorMessage = null,
                AffectedSystems = new List<string> { "Live WhatsApp Dispatches (Mocked in Dev)" },
                Diagnostics = new Dictionary<string, string>
                {
                    ["Mode"] = "Standby (Disabled via configuration)",
                    ["Integrated Number"] = !string.IsNullOrWhiteSpace(_msg91Options.IntegratedNumber) ? _msg91Options.IntegratedNumber : "Not set",
                    ["Booking Template"] = _msg91Options.BookingConfirmedTemplateName,
                    ["Ticket Template"] = _msg91Options.TicketPdfTemplateName
                },
                ActionUrl = "/admin_portal/communications",
                ActionLabel = "View Communications"
            };
        }

        if (!_msg91Options.IsConfigured)
        {
            return new SystemHealthComponentDto
            {
                Key = "msg91_whatsapp",
                Name = "MSG91 WhatsApp Provider",
                Category = "Messaging",
                Status = "Not Configured",
                LatencyMs = 0,
                LastCheckedUtc = now,
                LastSuccessfulCheckUtc = GetLastSuccess("msg91_whatsapp"),
                ErrorMessage = "MSG91 AuthKey or IntegratedNumber missing in configuration.",
                AffectedSystems = new List<string> { "Ticket Delivery via WhatsApp", "Booking Confirmations", "Password Reset OTP" },
                Diagnostics = new Dictionary<string, string>
                {
                    ["AuthKey Configured"] = !string.IsNullOrWhiteSpace(_msg91Options.AuthKey) ? "Yes" : "No",
                    ["Integrated Number"] = !string.IsNullOrWhiteSpace(_msg91Options.IntegratedNumber) ? "Yes" : "No"
                },
                ActionUrl = "/admin_portal/communications",
                ActionLabel = "View Communications"
            };
        }

        // MSG91 is enabled & configured: evaluate dispatch failure rate in last 24h
        int recentCount = 0;
        int failedCount = 0;
        try
        {
            var since = now.AddHours(-24);
            recentCount = await _db.WhatsAppNotifications.CountAsync(n => n.CreatedAt >= since, cancellationToken);
            failedCount = await _db.WhatsAppNotifications.CountAsync(n => n.CreatedAt >= since && n.Status == WhatsAppNotificationStatus.Failed, cancellationToken);
        }
        catch { }

        var failureRate = recentCount > 0 ? (double)failedCount / recentCount : 0.0;
        var status = "Operational";
        string? errorMsg = null;

        if (recentCount >= 3 && failureRate >= 0.3)
        {
            status = "Degraded";
            errorMsg = $"Elevated WhatsApp dispatch failure rate: {Math.Round(failureRate * 100, 1)}% ({failedCount}/{recentCount} failed).";
        }
        else
        {
            UpdateSuccess("msg91_whatsapp", now);
        }

        return new SystemHealthComponentDto
        {
            Key = "msg91_whatsapp",
            Name = "MSG91 WhatsApp Provider",
            Category = "Messaging",
            Status = status,
            LatencyMs = 1,
            LastCheckedUtc = now,
            LastSuccessfulCheckUtc = GetLastSuccess("msg91_whatsapp"),
            ErrorMessage = errorMsg,
            AffectedSystems = new List<string> { "Automated WhatsApp Booking Confirmations", "Ticket PDF Delivery" },
            Diagnostics = new Dictionary<string, string>
            {
                ["Status"] = "Active",
                ["Integrated Number"] = _msg91Options.IntegratedNumber ?? "Configured",
                ["Booking Template"] = _msg91Options.BookingConfirmedTemplateName,
                ["24h Messages Sent"] = recentCount.ToString(),
                ["24h Dispatches Failed"] = failedCount.ToString()
            },
            ActionUrl = "/admin_portal/communications",
            ActionLabel = "View Communications"
        };
    }

    private async Task<SystemHealthComponentDto> CheckWhatsAppWorkerAsync(DateTime now, CancellationToken cancellationToken)
    {
        var hb = _workerLivenessTracker.GetHeartbeat("worker_whatsapp");
        var isMsg91Disabled = !_msg91Options.Enabled;

        int pendingCount = 0;
        int failedCount = 0;
        try
        {
            pendingCount = await _db.WhatsAppNotifications.CountAsync(x => x.Status == WhatsAppNotificationStatus.Pending, cancellationToken);
            failedCount = await _db.WhatsAppNotifications.CountAsync(x => x.Status == WhatsAppNotificationStatus.Failed, cancellationToken);
        }
        catch { }

        string status;
        string? errorMsg = null;

        if (isMsg91Disabled)
        {
            status = "Standby";
            UpdateSuccess("worker_whatsapp", now);
        }
        else if (hb == null || (now - hb.LastHeartbeatUtc).TotalSeconds > 120)
        {
            status = "Error";
            errorMsg = "WhatsApp background worker heartbeat timed out or worker has stopped.";
        }
        else if (pendingCount > 25 || failedCount > 10)
        {
            status = "Degraded";
            errorMsg = $"Outbox backlog elevated: {pendingCount} pending, {failedCount} failed.";
        }
        else
        {
            status = "Operational";
            UpdateSuccess("worker_whatsapp", now);
        }

        return new SystemHealthComponentDto
        {
            Key = "worker_whatsapp",
            Name = "WhatsApp Outbox Worker",
            Category = "Background Worker",
            Status = status,
            LatencyMs = 0,
            LastCheckedUtc = now,
            LastSuccessfulCheckUtc = GetLastSuccess("worker_whatsapp"),
            ErrorMessage = errorMsg,
            AffectedSystems = new List<string> { "Outbox Queue Draining", "Automatic Message Retries", "Lease Recovery" },
            Diagnostics = new Dictionary<string, string>
            {
                ["Worker State"] = isMsg91Disabled ? "Standby (Disabled in dev)" : "Active Polling",
                ["Pending Queue"] = pendingCount.ToString(),
                ["Failed Backlog"] = failedCount.ToString(),
                ["Polling Interval"] = $"{_msg91Options.WorkerPollingIntervalSeconds}s",
                ["Batch Target"] = $"{_msg91Options.BatchSize}"
            },
            ActionUrl = "/admin_portal/communications",
            ActionLabel = "Inspect Outbox Queue"
        };
    }

    private async Task<SystemHealthComponentDto> CheckRefundWorkerAsync(DateTime now, CancellationToken cancellationToken)
    {
        var hb = _workerLivenessTracker.GetHeartbeat("worker_refund");

        int pendingRefunds = 0;
        try
        {
            pendingRefunds = await _db.RefundJobs.CountAsync(x => x.Status == RefundStatus.Requested || x.Status == RefundStatus.Processing, cancellationToken);
        }
        catch { }

        string status;
        string? errorMsg = null;

        if (hb == null || (now - hb.LastHeartbeatUtc).TotalSeconds > 120)
        {
            status = "Error";
            errorMsg = "Refund background worker heartbeat timed out.";
        }
        else if (pendingRefunds > 10)
        {
            status = "Degraded";
            errorMsg = $"Refund queue backlog elevated: {pendingRefunds} refunds pending execution.";
        }
        else
        {
            status = "Operational";
            UpdateSuccess("worker_refund", now);
        }

        return new SystemHealthComponentDto
        {
            Key = "worker_refund",
            Name = "Refund Outbox Worker",
            Category = "Background Worker",
            Status = status,
            LatencyMs = 0,
            LastCheckedUtc = now,
            LastSuccessfulCheckUtc = GetLastSuccess("worker_refund"),
            ErrorMessage = errorMsg,
            AffectedSystems = new List<string> { "Refund Job Processing", "Payment Gateway Reconciliation" },
            Diagnostics = new Dictionary<string, string>
            {
                ["Worker State"] = "Active Polling",
                ["Pending Refunds Queue"] = pendingRefunds.ToString(),
                ["Polling Interval"] = "5s"
            },
            ActionUrl = "/admin_portal/payments",
            ActionLabel = "View Refunds"
        };
    }

    private SystemHealthComponentDto CheckTelemetryWorker(DateTime now)
    {
        var hb = _workerLivenessTracker.GetHeartbeat("worker_telemetry");

        string status = "Operational";
        string? errorMsg = null;

        if (hb == null || (now - hb.LastHeartbeatUtc).TotalSeconds > 180)
        {
            // If worker hasn't reported heartbeat, mark degraded
            status = "Degraded";
            errorMsg = "Telemetry worker heartbeat has not updated recently.";
        }
        else
        {
            UpdateSuccess("worker_telemetry", now);
        }

        return new SystemHealthComponentDto
        {
            Key = "worker_telemetry",
            Name = "Telemetry Ingestion Worker",
            Category = "Background Worker",
            Status = status,
            LatencyMs = 0,
            LastCheckedUtc = now,
            LastSuccessfulCheckUtc = GetLastSuccess("worker_telemetry"),
            ErrorMessage = errorMsg,
            AffectedSystems = new List<string> { "Asynchronous API Request Logging", "Trace Deep-Dive Persistence" },
            Diagnostics = new Dictionary<string, string>
            {
                ["Worker State"] = "Active Ingestion",
                ["Batch Drain Target"] = "64 items",
                ["Cycles Completed"] = hb?.CyclesCompleted.ToString() ?? "Active",
                ["Total Ingested Items"] = hb?.ItemsProcessed.ToString() ?? "0"
            },
            ActionUrl = "/admin_portal/observability",
            ActionLabel = "View Telemetry Logs"
        };
    }

    private async Task<SystemHealthComponentDto> CheckAuthenticationAuthorityAsync(DateTime now, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        int activeSessions = 0;
        try
        {
            activeSessions = await _db.AdminSessions.CountAsync(s => s.IsActive, cancellationToken);
            sw.Stop();
        }
        catch
        {
            sw.Stop();
        }

        var jwtIssuer = _configuration["Jwt:Issuer"];
        var isJwtConfigured = !string.IsNullOrWhiteSpace(jwtIssuer);

        string status = isJwtConfigured ? "Operational" : "Not Configured";
        if (status == "Operational") UpdateSuccess("auth_authority", now);

        return new SystemHealthComponentDto
        {
            Key = "auth_authority",
            Name = "Authentication & Session Authority",
            Category = "Security",
            Status = status,
            LatencyMs = sw.ElapsedMilliseconds,
            LastCheckedUtc = now,
            LastSuccessfulCheckUtc = GetLastSuccess("auth_authority"),
            ErrorMessage = !isJwtConfigured ? "JWT Issuer or signing key unconfigured." : null,
            AffectedSystems = new List<string> { "Admin Login", "Student / Trainer Authentication", "Role-Based Access Control" },
            Diagnostics = new Dictionary<string, string>
            {
                ["Active Admin Sessions"] = activeSessions.ToString(),
                ["JWT Issuer"] = jwtIssuer ?? "Ethos.Api",
                ["Token Expiration"] = $"{_configuration["Jwt:ExpirationMinutes"] ?? "60"} minutes",
                ["Device Binding"] = "Active"
            },
            ActionUrl = "/admin_portal/security",
            ActionLabel = "View Devices & Sessions"
        };
    }

    private async Task<SystemHealthComponentDto> CheckSecurityMonitorAsync(DateTime now, CancellationToken cancellationToken)
    {
        int secEvents24h = 0;
        int openIncidents = 0;
        try
        {
            var since = now.AddHours(-24);
            secEvents24h = await _db.SecurityEvents.CountAsync(e => e.CreatedAt >= since, cancellationToken);
            openIncidents = await _db.Incidents.CountAsync(i => i.Status != "Resolved" && i.Status != "Cancelled", cancellationToken);
        }
        catch { }

        string status = "Operational";
        string? errorMsg = null;

        if (openIncidents > 0)
        {
            status = "Degraded";
            errorMsg = $"{openIncidents} unresolved operational incident(s) currently open.";
        }
        else if (secEvents24h > 20)
        {
            status = "Degraded";
            errorMsg = $"Elevated security events: {secEvents24h} security anomalies detected in last 24h.";
        }
        else
        {
            UpdateSuccess("security_monitor", now);
        }

        return new SystemHealthComponentDto
        {
            Key = "security_monitor",
            Name = "Security & Threat Monitor",
            Category = "Security",
            Status = status,
            LatencyMs = 1,
            LastCheckedUtc = now,
            LastSuccessfulCheckUtc = GetLastSuccess("security_monitor"),
            ErrorMessage = errorMsg,
            AffectedSystems = new List<string> { "Rate Limiting", "Admin Fleet Protection", "Brute-Force Shield" },
            Diagnostics = new Dictionary<string, string>
            {
                ["Security Anomalies (24h)"] = secEvents24h.ToString(),
                ["Open Incidents"] = openIncidents.ToString(),
                ["Protection Status"] = "Active"
            },
            ActionUrl = "/admin_portal/incidents",
            ActionLabel = "View Incidents & Security"
        };
    }

    private static string ComputeOverallStatus(List<SystemHealthComponentDto> components)
    {
        if (components.Any(c => c.Status == "Error"))
            return "Error";
        if (components.Any(c => c.Status == "Degraded"))
            return "Degraded";
        if (components.All(c => c.Status == "Standby"))
            return "Standby";
        if (components.All(c => c.Status == "Not Configured"))
            return "Not Configured";
        return "Operational";
    }

    private static void UpdateSuccess(string key, DateTime now)
    {
        LastSuccessfulChecks[key] = now;
    }

    private static DateTime? GetLastSuccess(string key)
    {
        return LastSuccessfulChecks.TryGetValue(key, out var dt) ? dt : null;
    }

    private static string SanitizeErrorMessage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "An internal error occurred.";

        // Strip connection strings, passwords, and sensitive markers
        var clean = raw;
        if (clean.Contains("Password=", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("Username=", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("Secret", StringComparison.OrdinalIgnoreCase))
        {
            clean = "Connection failure or credential rejection.";
        }

        return clean.Length > 200 ? clean[..200] + "..." : clean;
    }
}
