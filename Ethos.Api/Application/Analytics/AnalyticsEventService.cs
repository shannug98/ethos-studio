using System.Security.Claims;
using System.Text.Json;
using Ethos.Api.Contracts.Analytics;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Application.Analytics;

public class AnalyticsEventService : IAnalyticsEventService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AnalyticsEventService> _logger;

    public AnalyticsEventService(
        AppDbContext db,
        ILogger<AnalyticsEventService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<RecordAnalyticsEventResponse> RecordEventAsync(
        RecordAnalyticsEventRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        // 1. Server-authoritative EventName derivation from enum
        var eventName = request.EventType.ToString();

        // 2. Optional authenticated user ID (strictly resolved server-side from claims)
        Guid? userId = null;
        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? user.FindFirst("sub")?.Value
                   ?? user.FindFirst("userId")?.Value;

            if (Guid.TryParse(sub, out var parsedId))
            {
                userId = parsedId;
            }
        }

        var metadataDict = request.Metadata != null ? new Dictionary<string, string>(request.Metadata) : new Dictionary<string, string>();

        // Privacy-safe coarse geographic header extraction (Cloudflare / Edge CDN headers)
        if (httpContext.Request.Headers.TryGetValue("CF-IPCity", out var cityHeader) && !string.IsNullOrWhiteSpace(cityHeader))
        {
            metadataDict["city"] = cityHeader.ToString().Trim();
        }
        if (httpContext.Request.Headers.TryGetValue("CF-Region", out var regionHeader) && !string.IsNullOrWhiteSpace(regionHeader))
        {
            metadataDict["region"] = regionHeader.ToString().Trim();
        }
        if (httpContext.Request.Headers.TryGetValue("CF-IPCountry", out var countryHeader) && !string.IsNullOrWhiteSpace(countryHeader))
        {
            metadataDict["country"] = countryHeader.ToString().Trim();
        }

        string? metadataJson = null;
        if (metadataDict.Count > 0)
        {
            try
            {
                metadataJson = JsonSerializer.Serialize(metadataDict);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to serialize analytics event metadata. Omitting metadata.");
            }
        }

        // 4. Path & Referrer truncation if needed
        var path = request.Path;
        if (path != null && path.Length > 500) path = path[..500];

        var referrer = request.Referrer;
        if (referrer != null && referrer.Length > 500) referrer = referrer[..500];

        // 5. User-Agent capturing for real privacy-safe device classification
        string? userAgent = null;
        if (httpContext.Request.Headers.TryGetValue("User-Agent", out var uaValues))
        {
            var uaStr = uaValues.ToString();
            if (!string.IsNullOrWhiteSpace(uaStr))
            {
                userAgent = uaStr.Length > 500 ? uaStr[..500] : uaStr;
            }
        }

        var analyticsEvent = new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            EventType = request.EventType,
            EventName = eventName,
            OccurredAtUtc = DateTime.UtcNow,
            VisitorId = request.VisitorId.Trim(),
            SessionId = request.SessionId.Trim(),
            WorkshopId = request.WorkshopId,
            Path = path,
            Referrer = referrer,
            MetadataJson = metadataJson,
            IpAddress = null, // Data minimization: Technical IP logging belongs strictly in ApiRequestLog
            UserAgent = null, // Data minimization: Raw UA logging avoided on business analytics events
            UserId = userId
        };

        _db.AnalyticsEvents.Add(analyticsEvent);
        await _db.SaveChangesAsync(cancellationToken);

        return new RecordAnalyticsEventResponse
        {
            Success = true,
            EventId = analyticsEvent.Id,
            OccurredAtUtc = analyticsEvent.OccurredAtUtc
        };
    }
}
