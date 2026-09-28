using Ethos.Api.Contracts.Analytics;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Application.Analytics;

public class AnalyticsQueryService : IAnalyticsQueryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AnalyticsQueryService> _logger;

    public AnalyticsQueryService(
        AppDbContext db,
        ILogger<AnalyticsQueryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public static (DateTime FromUtc, DateTime ToUtc) ResolveRange(string? range, string defaultRange = "last7days")
    {
        var normalized = string.IsNullOrWhiteSpace(range) ? defaultRange : range.ToLowerInvariant().Trim();
        var nowUtc = DateTime.UtcNow;
        var todayUtc = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc);
        var tomorrowUtc = todayUtc.AddDays(1);

        return normalized switch
        {
            "today" => (todayUtc, tomorrowUtc),
            "yesterday" => (todayUtc.AddDays(-1), todayUtc),
            "week" or "last7days" => (todayUtc.AddDays(-6), tomorrowUtc),
            "month" or "last30days" => (todayUtc.AddDays(-29), tomorrowUtc),
            "last90days" => (todayUtc.AddDays(-89), tomorrowUtc),
            _ => throw new ArgumentException($"Invalid analytics time range '{range}'. Allowed values are: today, yesterday, week, last7days, month, last30days, last90days.")
        };
    }

    public async Task<AnalyticsSummaryResponse> GetSummaryAsync(
        string range = "last7days",
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = ResolveRange(range);

        var query = _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc);

        var totalEvents = await query.LongCountAsync(cancellationToken);

        if (totalEvents == 0)
        {
            return new AnalyticsSummaryResponse
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                TotalEvents = 0,
                UniqueVisitors = 0,
                UniqueSessions = 0,
                AuthenticatedEvents = 0
            };
        }

        var uniqueVisitors = await query.Select(e => e.VisitorId).Distinct().LongCountAsync(cancellationToken);
        var uniqueSessions = await query.Select(e => e.SessionId).Distinct().LongCountAsync(cancellationToken);
        var authenticatedEvents = await query.Where(e => e.UserId != null).LongCountAsync(cancellationToken);

        var countsByEvent = await query
            .GroupBy(e => e.EventType)
            .Select(g => new { EventType = g.Key, Count = g.LongCount() })
            .ToDictionaryAsync(x => x.EventType, x => x.Count, cancellationToken);

        long GetCount(AnalyticsEventType type) => countsByEvent.TryGetValue(type, out var count) ? count : 0L;

        return new AnalyticsSummaryResponse
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            TotalEvents = totalEvents,
            UniqueVisitors = uniqueVisitors,
            UniqueSessions = uniqueSessions,
            AuthenticatedEvents = authenticatedEvents,
            PageViews = GetCount(AnalyticsEventType.PageView),
            WorkshopViews = GetCount(AnalyticsEventType.WorkshopView),
            CheckoutStarts = GetCount(AnalyticsEventType.WorkshopCheckoutStarted),
            CheckoutCompletions = GetCount(AnalyticsEventType.WorkshopCheckoutCompleted),
            FeedbackOpens = GetCount(AnalyticsEventType.FeedbackOpened),
            FeedbackSubmissions = GetCount(AnalyticsEventType.FeedbackSubmitted),
            LoginStarts = GetCount(AnalyticsEventType.LoginStarted),
            LoginCompletions = GetCount(AnalyticsEventType.LoginCompleted)
        };
    }

    public async Task<AnalyticsTrendsResponse> GetTrendsAsync(
        string range = "last30days",
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = ResolveRange(range, "last30days");

        var query = _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc);

        var points = await query
            .GroupBy(e => e.OccurredAtUtc.Date)
            .Select(g => new
            {
                DateUtc = g.Key,
                TotalEvents = g.LongCount(),
                UniqueVisitors = g.Select(e => e.VisitorId).Distinct().LongCount(),
                UniqueSessions = g.Select(e => e.SessionId).Distinct().LongCount()
            })
            .OrderBy(x => x.DateUtc)
            .ToListAsync(cancellationToken);

        var dtoList = points.Select(p => new AnalyticsTrendPointDto
        {
            DateUtc = p.DateUtc,
            TotalEvents = p.TotalEvents,
            UniqueVisitors = p.UniqueVisitors,
            UniqueSessions = p.UniqueSessions
        }).ToList();

        return new AnalyticsTrendsResponse
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Granularity = "day",
            Points = dtoList
        };
    }

    public async Task<AnalyticsEventBreakdownResponse> GetEventBreakdownAsync(
        string range = "last30days",
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = ResolveRange(range, "last30days");

        var query = _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc);

        var groups = await query
            .GroupBy(e => new { e.EventType, e.EventName })
            .Select(g => new AnalyticsEventBreakdownDto
            {
                EventType = (int)g.Key.EventType,
                EventName = g.Key.EventName,
                Count = g.LongCount(),
                UniqueVisitors = g.Select(e => e.VisitorId).Distinct().LongCount()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        return new AnalyticsEventBreakdownResponse
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Events = groups
        };
    }

    public async Task<AnalyticsWorkshopResponse> GetWorkshopAnalyticsAsync(
        string range = "last30days",
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = ResolveRange(range, "last30days");
        var boundedLimit = Math.Clamp(limit, 1, 50);

        var query = _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc && e.WorkshopId != null);

        var workshopAggregations = await query
            .GroupBy(e => new { e.WorkshopId, WorkshopTitle = e.Workshop != null ? e.Workshop.Title : "Unknown Workshop" })
            .Select(g => new AnalyticsWorkshopDto
            {
                WorkshopId = g.Key.WorkshopId!.Value,
                WorkshopName = g.Key.WorkshopTitle,
                Views = g.LongCount(e => e.EventType == AnalyticsEventType.WorkshopView),
                CheckoutStarts = g.LongCount(e => e.EventType == AnalyticsEventType.WorkshopCheckoutStarted),
                CheckoutCompletions = g.LongCount(e => e.EventType == AnalyticsEventType.WorkshopCheckoutCompleted),
                UniqueVisitors = g.Select(e => e.VisitorId).Distinct().LongCount()
            })
            .OrderByDescending(x => x.Views)
            .Take(boundedLimit)
            .ToListAsync(cancellationToken);

        return new AnalyticsWorkshopResponse
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Workshops = workshopAggregations
        };
    }

    public async Task<AnalyticsRecentEventsResponse> GetRecentEventsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, 100);

        var events = await _db.AnalyticsEvents
            .AsNoTracking()
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(boundedLimit)
            .Select(e => new AnalyticsRecentEventDto
            {
                Id = e.Id,
                EventName = e.EventName,
                OccurredAtUtc = e.OccurredAtUtc,
                VisitorId = e.VisitorId,
                SessionId = e.SessionId,
                WorkshopId = e.WorkshopId,
                Path = e.Path,
                IsAuthenticated = e.UserId != null
            })
            .ToListAsync(cancellationToken);

        return new AnalyticsRecentEventsResponse
        {
            Events = events
        };
    }
}
