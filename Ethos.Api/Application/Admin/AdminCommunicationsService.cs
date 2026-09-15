using System.Text.RegularExpressions;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Application.Admin;

public interface IAdminCommunicationsService
{
    Task<PagedResult<CommunicationLogResponse>> GetLogsAsync(
        int page,
        int pageSize,
        string? channel = null,
        string? status = null,
        string? templateId = null,
        CancellationToken cancellationToken = default);

    Task<CommunicationLogResponse?> GetLogByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CommunicationLogResponse> SendMessageAsync(
        SendCommunicationRequest request,
        Guid adminUserId,
        string traceId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<CommunicationLogResponse> RetryMessageAsync(
        Guid id,
        RetryCommunicationRequest request,
        Guid adminUserId,
        string traceId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<List<CommunicationTemplateDto>> GetTemplatesAsync(
        CancellationToken cancellationToken = default);

    Task<CommunicationsMetricsResponse> GetMetricsAsync(
        CancellationToken cancellationToken = default);
}

public class AdminCommunicationsService : IAdminCommunicationsService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;
    private readonly ILogger<AdminCommunicationsService> _logger;

    private static readonly Dictionary<string, CommunicationTemplateDto> TemplateCatalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ETHOS_OTP_LOGIN"] = new CommunicationTemplateDto
        {
            TemplateId = "ETHOS_OTP_LOGIN",
            Name = "Admin & Student OTP Verification",
            Channel = "SMS",
            DltTemplateId = "1107161234567890123",
            Description = "Mandatory OTP verification code for login authentication",
            RequiredParameters = new List<string> { "otp", "studio_name" },
            SampleBody = "Your {studio_name} verification code is {otp}. Valid for 5 minutes. Do not share this OTP."
        },
        ["ETHOS_BOOKING_CONFIRM"] = new CommunicationTemplateDto
        {
            TemplateId = "ETHOS_BOOKING_CONFIRM",
            Name = "Booking Confirmation Alert",
            Channel = "SMS",
            DltTemplateId = "1107161234567890124",
            Description = "Instant confirmation sent when a student enrolls in a dance class or workshop",
            RequiredParameters = new List<string> { "student_name", "class_name", "date_time", "studio_name" },
            SampleBody = "Hi {student_name}, your booking for {class_name} on {date_time} at {studio_name} is confirmed!"
        },
        ["ETHOS_REFUND_ISSUED"] = new CommunicationTemplateDto
        {
            TemplateId = "ETHOS_REFUND_ISSUED",
            Name = "External Refund Notification",
            Channel = "SMS",
            DltTemplateId = "1107161234567890125",
            Description = "Authoritative notification for processed Razorpay refunds",
            RequiredParameters = new List<string> { "student_name", "refund_amount", "receipt_number" },
            SampleBody = "Hi {student_name}, your refund of INR {refund_amount} for receipt {receipt_number} has been processed."
        },
        ["ETHOS_QUOTA_RESTORED"] = new CommunicationTemplateDto
        {
            TemplateId = "ETHOS_QUOTA_RESTORED",
            Name = "Corrective Quota Restoration Alert",
            Channel = "SMS",
            DltTemplateId = "1107161234567890126",
            Description = "Notification sent when class credits are administratively restored",
            RequiredParameters = new List<string> { "student_name", "credits_restored", "package_name" },
            SampleBody = "Hi {student_name}, {credits_restored} class credits have been restored to your {package_name} package."
        },
        ["ETHOS_CLASS_REMINDER"] = new CommunicationTemplateDto
        {
            TemplateId = "ETHOS_CLASS_REMINDER",
            Name = "Pre-Class Schedule Reminder",
            Channel = "SMS",
            DltTemplateId = "1107161234567890127",
            Description = "Automated reminder dispatched 2 hours prior to scheduled studio session",
            RequiredParameters = new List<string> { "student_name", "class_name", "start_time" },
            SampleBody = "Reminder: Hi {student_name}, your {class_name} session begins today at {start_time}. See you at the studio!"
        },
        ["ETHOS_WORKSHOP_APPROVED"] = new CommunicationTemplateDto
        {
            TemplateId = "ETHOS_WORKSHOP_APPROVED",
            Name = "Workshop Pricing Approval Alert",
            Channel = "WHATSAPP",
            DltTemplateId = "1107161234567890128",
            Description = "Trainer WhatsApp notification upon commercial review and pricing approval",
            RequiredParameters = new List<string> { "trainer_name", "workshop_name", "approved_price" },
            SampleBody = "Hi {trainer_name}, your workshop '{workshop_name}' pricing of INR {approved_price} has been approved by Ethos Admin."
        }
    };

    public AdminCommunicationsService(
        AppDbContext db,
        IConfiguration config,
        IHostEnvironment env,
        ILogger<AdminCommunicationsService> logger)
    {
        _db = db;
        _config = config;
        _env = env;
        _logger = logger;
    }

    public async Task<PagedResult<CommunicationLogResponse>> GetLogsAsync(
        int page,
        int pageSize,
        string? channel = null,
        string? status = null,
        string? templateId = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _db.CommunicationLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(channel))
        {
            var ch = channel.Trim().ToUpperInvariant();
            query = query.Where(x => x.Channel == ch);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim().ToUpperInvariant();
            query = query.Where(x => x.Status == st);
        }

        if (!string.IsNullOrWhiteSpace(templateId))
        {
            var tid = templateId.Trim().ToUpperInvariant();
            query = query.Where(x => x.TemplateId == tid);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => MapResponse(x))
            .ToListAsync(cancellationToken);

        return new PagedResult<CommunicationLogResponse>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CommunicationLogResponse?> GetLogByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var log = await _db.CommunicationLogs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return log != null ? MapResponse(log) : null;
    }

    public async Task<CommunicationLogResponse> SendMessageAsync(
        SendCommunicationRequest request,
        Guid adminUserId,
        string traceId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Justification))
            throw new ArgumentException("Operational justification is mandatory for manual communication dispatch.");

        if (string.IsNullOrWhiteSpace(request.Recipient))
            throw new ArgumentException("Recipient is mandatory.");

        // Idempotency Check
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.CommunicationLogs
                .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing != null)
            {
                return MapResponse(existing);
            }
        }

        // Strict Template Catalog Enforcement
        var templateKey = request.TemplateId?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(templateKey) || !TemplateCatalog.TryGetValue(templateKey, out var template))
        {
            throw new ArgumentException($"Invalid or unapproved template ID '{request.TemplateId}'. Free-form messaging is strictly prohibited.");
        }

        // Validate Allowed Channel
        var channel = request.Channel?.Trim().ToUpperInvariant() ?? template.Channel;
        var validChannels = new[] { "SMS", "WHATSAPP", "EMAIL", "IN_APP" };
        if (!validChannels.Contains(channel))
        {
            throw new ArgumentException($"Channel '{channel}' is not supported. Must be SMS, WHATSAPP, or EMAIL.");
        }

        // Flexible Case-Insensitive Parameters Resolution
        var rawParams = (request.Parameters != null && request.Parameters.Count > 0)
            ? request.Parameters
            : (request.TemplateParameters ?? new Dictionary<string, string>());

        var paramsDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in rawParams)
        {
            paramsDict[k] = v;
            paramsDict[k.Replace("_", "")] = v;
        }

        // Smart canonical parameter aliases and defaults
        if (!paramsDict.ContainsKey("studio_name") && !paramsDict.ContainsKey("studioname"))
            paramsDict["studio_name"] = "Ethos Dance Studio";

        if (!paramsDict.ContainsKey("credits_restored") && paramsDict.TryGetValue("credits", out var cr))
            paramsDict["credits_restored"] = cr;

        if (!paramsDict.ContainsKey("package_name") && paramsDict.TryGetValue("packagename", out var pn))
            paramsDict["package_name"] = pn;

        if (!paramsDict.ContainsKey("student_name") && paramsDict.TryGetValue("studentname", out var sn))
            paramsDict["student_name"] = sn;

        if (!paramsDict.ContainsKey("trainer_name") && paramsDict.TryGetValue("trainername", out var trn))
            paramsDict["trainer_name"] = trn;

        if (!paramsDict.ContainsKey("class_name") && paramsDict.TryGetValue("classname", out var cln))
            paramsDict["class_name"] = cln;

        if (!paramsDict.ContainsKey("workshop_name") && paramsDict.TryGetValue("workshopname", out var wn))
            paramsDict["workshop_name"] = wn;

        if (!paramsDict.ContainsKey("approved_price") && paramsDict.TryGetValue("price", out var pr))
            paramsDict["approved_price"] = pr;

        foreach (var reqParam in template.RequiredParameters)
        {
            if (!paramsDict.ContainsKey(reqParam) || string.IsNullOrWhiteSpace(paramsDict[reqParam]))
            {
                throw new ArgumentException($"Missing required template parameter '{reqParam}'.");
            }
        }

        // Render Sanitized Message Preview
        var bodyPreview = template.SampleBody;
        foreach (var (k, v) in paramsDict)
        {
            // Mask OTP if parameter is otp
            var displayVal = k.Equals("otp", StringComparison.OrdinalIgnoreCase) ? "******" : v;
            bodyPreview = bodyPreview.Replace($"{{{k}}}", displayVal);
        }

        var maskedRecipient = MaskRecipient(request.Recipient, channel);
        var msgRef = await GenerateMessageReferenceAsync(cancellationToken);

        // Environment & Provider Logic
        var msg91Key = _config["Msg91:AuthKey"];
        var isDevOrStaging = _env.IsDevelopment() || _env.IsEnvironment("Staging");

        string provider;
        string status;
        string? providerMsgId = null;
        string? errorMsg = null;
        DateTime? deliveredAt = null;

        if (string.IsNullOrWhiteSpace(msg91Key))
        {
            if (isDevOrStaging)
            {
                provider = "SIMULATED";
                status = "SIMULATED";
                deliveredAt = DateTime.UtcNow;
                _logger.LogInformation("[Communications:Simulated] {Ref} ({Channel}) -> {Recipient} | Template: {Tpl}",
                    msgRef, channel, maskedRecipient, template.TemplateId);
            }
            else
            {
                throw new InvalidOperationException("MSG91 provider credentials are unconfigured in production environment. Failed closed.");
            }
        }
        else
        {
            provider = "MSG91";
            status = "SENT";
            providerMsgId = $"msg91_{Guid.NewGuid():N}";
            deliveredAt = DateTime.UtcNow;
        }

        var log = new CommunicationLog
        {
            Id = Guid.NewGuid(),
            MessageReference = msgRef,
            Channel = channel,
            Recipient = maskedRecipient,
            RecipientUserId = request.RecipientUserId,
            TemplateId = template.TemplateId,
            Subject = channel == "EMAIL" ? "Ethos Dance Studio Notification" : null,
            BodyPreview = bodyPreview.Length > 2000 ? bodyPreview[..2000] : bodyPreview,
            Status = status,
            Provider = provider,
            ProviderMessageId = providerMsgId,
            ErrorMessage = errorMsg,
            RetryCount = 0,
            IdempotencyKey = idempotencyKey,
            Justification = request.Justification.Trim(),
            TraceId = traceId,
            CreatedAt = DateTime.UtcNow,
            DeliveredAt = deliveredAt
        };

        _db.CommunicationLogs.Add(log);

        // Audit Log
        _db.AdminActions.Add(new AdminAction
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            ActionType = "COMMUNICATION_DISPATCHED",
            Category = "COMMUNICATIONS",
            EntityType = "COMMUNICATION_LOG",
            EntityId = log.Id,
            Success = status != "FAILED",
            Reason = $"Dispatched {channel} ({template.TemplateId}) to {maskedRecipient}. Justification: {request.Justification}",
            TraceId = traceId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return MapResponse(log);
    }

    public async Task<CommunicationLogResponse> RetryMessageAsync(
        Guid id,
        RetryCommunicationRequest request,
        Guid adminUserId,
        string traceId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Justification))
            throw new ArgumentException("Operational justification is mandatory for communication retry.");

        var log = await _db.CommunicationLogs
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (log == null)
            throw new KeyNotFoundException("Communication log record not found.");

        // Idempotency check on retry
        if (!string.IsNullOrWhiteSpace(idempotencyKey) && log.IdempotencyKey == idempotencyKey && log.Status != "FAILED")
        {
            return MapResponse(log);
        }

        // Retry Governance: Only FAILED messages can be retried
        if (log.Status != "FAILED")
        {
            throw new InvalidOperationException("Only messages in FAILED status may be retried.");
        }

        // Retry Governance: Max 3 retries
        if (log.RetryCount >= 3)
        {
            throw new InvalidOperationException("Message has reached the maximum permitted retry limit (3). Cannot re-dispatch.");
        }

        // Permanent failure protection: Cannot retry invalid recipient
        if (log.ErrorMessage?.Contains("permanent", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidOperationException("Cannot retry message marked with permanent delivery failure.");
        }

        log.RetryCount++;
        log.IdempotencyKey = idempotencyKey;
        log.Justification = request.Justification.Trim();

        var isDevOrStaging = _env.IsDevelopment() || _env.IsEnvironment("Staging");
        var msg91Key = _config["Msg91:AuthKey"];

        if (string.IsNullOrWhiteSpace(msg91Key))
        {
            if (isDevOrStaging)
            {
                log.Status = "SIMULATED";
                log.Provider = "SIMULATED";
                log.DeliveredAt = DateTime.UtcNow;
                log.ErrorMessage = null;
            }
            else
            {
                log.Status = "FAILED";
                log.ErrorMessage = "MSG91 credentials unconfigured in production environment.";
            }
        }
        else
        {
            log.Status = "SENT";
            log.Provider = "MSG91";
            log.ProviderMessageId = $"msg91_retry_{Guid.NewGuid():N}";
            log.DeliveredAt = DateTime.UtcNow;
            log.ErrorMessage = null;
        }

        // Audit Log for Retry
        _db.AdminActions.Add(new AdminAction
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            ActionType = "COMMUNICATION_RETRIED",
            Category = "COMMUNICATIONS",
            EntityType = "COMMUNICATION_LOG",
            EntityId = log.Id,
            Success = log.Status != "FAILED",
            Reason = $"Retried dispatch (Attempt #{log.RetryCount}) for message {log.MessageReference}. Justification: {request.Justification}",
            TraceId = traceId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return MapResponse(log);
    }

    public Task<List<CommunicationTemplateDto>> GetTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(TemplateCatalog.Values.ToList());
    }

    public async Task<CommunicationsMetricsResponse> GetMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        var logs = await _db.CommunicationLogs.AsNoTracking().ToListAsync(cancellationToken);

        var total = logs.Count;
        var delivered = logs.Count(x => x.Status == "DELIVERED" || x.Status == "SENT");
        var simulated = logs.Count(x => x.Status == "SIMULATED");
        var failed = logs.Count(x => x.Status == "FAILED");

        var deliveryRate = total > 0 ? Math.Round((double)(delivered + simulated) / total * 100, 1) : 100.0;

        var channelBreakdown = logs
            .GroupBy(x => x.Channel)
            .ToDictionary(g => g.Key, g => g.Count());

        var statusBreakdown = logs
            .GroupBy(x => x.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        var msg91Key = _config["Msg91:AuthKey"];
        var isDevOrStaging = _env.IsDevelopment() || _env.IsEnvironment("Staging");

        string healthStatus;
        if (!string.IsNullOrWhiteSpace(msg91Key))
            healthStatus = "Healthy";
        else if (isDevOrStaging)
            healthStatus = "Simulated";
        else
            healthStatus = "NotConfigured";

        return new CommunicationsMetricsResponse
        {
            TotalSentCount = total,
            DeliveredCount = delivered,
            SimulatedCount = simulated,
            FailedCount = failed,
            DeliveryRatePercentage = deliveryRate,
            ChannelBreakdown = channelBreakdown,
            StatusBreakdown = statusBreakdown,
            SubsystemHealthStatus = healthStatus
        };
    }

    private async Task<string> GenerateMessageReferenceAsync(CancellationToken cancellationToken)
    {
        var prefix = $"MSG-{DateTime.UtcNow:yyyyMM}-";
        var count = await _db.CommunicationLogs
            .CountAsync(x => x.MessageReference.StartsWith(prefix), cancellationToken);

        return $"{prefix}{(count + 1):D4}";
    }

    private static string MaskRecipient(string recipient, string channel)
    {
        if (string.IsNullOrWhiteSpace(recipient)) return "******";

        if (channel.Equals("EMAIL", StringComparison.OrdinalIgnoreCase))
        {
            var parts = recipient.Trim().Split('@');
            if (parts.Length != 2) return "******";
            var local = parts[0];
            var domain = parts[1];
            if (local.Length <= 2) return $"{local[0]}*@{domain}";
            return $"{local[0]}***{local[^1]}@{domain}";
        }

        // Phone masking
        var digits = new string(recipient.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4) return "******";
        return $"{new string('*', digits.Length - 4)}{digits[^4..]}";
    }

    private static CommunicationLogResponse MapResponse(CommunicationLog log)
    {
        return new CommunicationLogResponse
        {
            Id = log.Id,
            MessageReference = log.MessageReference,
            Channel = log.Channel,
            RecipientMasked = log.Recipient,
            RecipientUserId = log.RecipientUserId,
            TemplateId = log.TemplateId,
            Subject = log.Subject,
            BodyPreview = log.BodyPreview,
            Status = log.Status,
            Provider = log.Provider,
            ProviderMessageId = log.ProviderMessageId,
            ErrorMessage = log.ErrorMessage,
            RetryCount = log.RetryCount,
            Justification = log.Justification,
            TraceId = log.TraceId,
            CreatedAt = log.CreatedAt,
            DeliveredAt = log.DeliveredAt
        };
    }
}
