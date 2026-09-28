using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Application.Analytics;

public class AdminInsightsService : IAdminInsightsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AdminInsightsService> _logger;

    public AdminInsightsService(
        AppDbContext db,
        ILogger<AdminInsightsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public static (DateTime FromUtc, DateTime ToUtc) ResolveRange(string? range, string defaultRange = "last30days")
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

    public async Task<BusinessOverviewResponse> GetOverviewAsync(
        string range = "last30days",
        Guid? workshopId = null,
        int windowMinutes = 15,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = ResolveRange(range, "last30days");
        var duration = toUtc - fromUtc;
        var prevFromUtc = fromUtc - duration;
        var prevToUtc = fromUtc;

        string? workshopTitle = null;
        if (workshopId.HasValue)
        {
            workshopTitle = await _db.Workshops
                .AsNoTracking()
                .Where(w => w.Id == workshopId.Value)
                .Select(w => w.Title)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Current period metrics
        var currentMetrics = await CalculatePeriodMetricsAsync(fromUtc, toUtc, workshopId, cancellationToken);
        var prevMetrics = await CalculatePeriodMetricsAsync(prevFromUtc, prevToUtc, workshopId, cancellationToken);

        // Live active users in rolling window
        var boundedWindow = Math.Clamp(windowMinutes, 1, 60);
        var liveCutoff = DateTime.UtcNow.AddMinutes(-boundedWindow);
        var liveUsers = await _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= liveCutoff)
            .Select(e => e.VisitorId)
            .Distinct()
            .LongCountAsync(cancellationToken);

        // Funnel & Rates (Calculated strictly from unique visitors and valid deduplicated stages)
        var funnel = new BusinessFunnelDto
        {
            Visitors = currentMetrics.Visitors,
            WorkshopViews = currentMetrics.WorkshopViews,
            UniqueWorkshopVisitors = currentMetrics.UniqueWorkshopVisitors,
            BookingStarts = currentMetrics.BookingStarts,
            PaymentAttempts = currentMetrics.PaymentAttempts,
            CompletedBookings = currentMetrics.CompletedBookings,
            WorkshopViewRate = currentMetrics.Visitors > 0
                ? Math.Clamp(Math.Round((double)currentMetrics.WorkshopViews / currentMetrics.Visitors * 100.0, 1), 0.0, 100.0)
                : 0.0,
            BookingStartRate = currentMetrics.WorkshopViews > 0
                ? Math.Clamp(Math.Round((double)currentMetrics.BookingStarts / currentMetrics.WorkshopViews * 100.0, 1), 0.0, 100.0)
                : 0.0,
            PaymentAttemptRate = currentMetrics.BookingStarts > 0
                ? Math.Clamp(Math.Round((double)currentMetrics.PaymentAttempts / currentMetrics.BookingStarts * 100.0, 1), 0.0, 100.0)
                : 0.0,
            CompletedBookingRate = currentMetrics.PaymentAttempts > 0
                ? Math.Clamp(Math.Round((double)currentMetrics.CompletedBookings / currentMetrics.PaymentAttempts * 100.0, 1), 0.0, 100.0)
                : 0.0,
            OverallConversionRate = currentMetrics.Visitors > 0
                ? Math.Clamp(Math.Round((double)currentMetrics.CompletedBookings / currentMetrics.Visitors * 100.0, 2), 0.0, 100.0)
                : 0.0
        };

        // Payment Outcomes
        var paymentOutcomes = await GetPaymentOutcomesAsync(range, workshopId, cancellationToken);

        // Calculate Average Time to Purchase
        var avgTimeToPurchaseSeconds = await CalculateAverageTimeToPurchaseAsync(fromUtc, toUtc, workshopId, cancellationToken);

        return new BusinessOverviewResponse
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Range = range,
            WorkshopId = workshopId,
            WorkshopTitle = workshopTitle,
            Visitors = currentMetrics.Visitors,
            WorkshopViews = currentMetrics.WorkshopViews,
            UniqueWorkshopVisitors = currentMetrics.UniqueWorkshopVisitors,
            BookingStarts = currentMetrics.BookingStarts,
            PaymentAttempts = currentMetrics.PaymentAttempts,
            CompletedBookings = currentMetrics.CompletedBookings,
            TotalRevenue = currentMetrics.TotalRevenue,
            VisitorChangePercent = CalculatePercentChange(currentMetrics.Visitors, prevMetrics.Visitors),
            WorkshopViewChangePercent = CalculatePercentChange(currentMetrics.WorkshopViews, prevMetrics.WorkshopViews),
            BookingStartChangePercent = CalculatePercentChange(currentMetrics.BookingStarts, prevMetrics.BookingStarts),
            PaymentAttemptChangePercent = CalculatePercentChange(currentMetrics.PaymentAttempts, prevMetrics.PaymentAttempts),
            CompletedBookingChangePercent = CalculatePercentChange(currentMetrics.CompletedBookings, prevMetrics.CompletedBookings),
            RevenueChangePercent = CalculatePercentChange((double)currentMetrics.TotalRevenue, (double)prevMetrics.TotalRevenue),
            Funnel = funnel,
            PaymentOutcomes = paymentOutcomes,
            LiveUsers = liveUsers,
            AverageTimeToPurchaseSeconds = avgTimeToPurchaseSeconds
        };
    }

    public async Task<BusinessTrendsResponse> GetTrendsAsync(
        string range = "last30days",
        Guid? workshopId = null,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = ResolveRange(range, "last30days");

        // 1. Daily Analytics Events
        var eventsQuery = _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc);

        if (workshopId.HasValue)
        {
            eventsQuery = eventsQuery.Where(e => e.WorkshopId == workshopId.Value);
        }

        var dailyEvents = await eventsQuery
            .GroupBy(e => e.OccurredAtUtc.Date)
            .Select(g => new
            {
                Date = g.Key,
                Visitors = g.Select(e => e.VisitorId).Distinct().LongCount(),
                WorkshopViews = g.LongCount(e => e.EventType == AnalyticsEventType.WorkshopView),
                CheckoutStarts = g.LongCount(e => e.EventType == AnalyticsEventType.WorkshopCheckoutStarted)
            })
            .ToListAsync(cancellationToken);

        // 2. Daily Payment Transactions (Attempts)
        var paymentsQuery = _db.PaymentTransactions
            .AsNoTracking()
            .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toUtc && p.Purpose == PaymentPurpose.WorkshopBooking);

        if (workshopId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => _db.WorkshopBookings.Any(b => b.Id == p.ReferenceId && b.WorkshopId == workshopId.Value));
        }

        var dailyPayments = await paymentsQuery
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                PaymentAttempts = g.LongCount()
            })
            .ToListAsync(cancellationToken);

        // 3. Daily Completed Bookings & Revenue
        var bookingsQuery = _db.WorkshopBookings
            .AsNoTracking()
            .Where(b => b.BookedAt >= fromUtc && b.BookedAt < toUtc
                && (b.Status == WorkshopBookingStatus.Confirmed || b.Status == WorkshopBookingStatus.Attended)
                && b.PaymentTransactionId != null
                && _db.PaymentTransactions.Any(pt => pt.Id == b.PaymentTransactionId && pt.Purpose == PaymentPurpose.WorkshopBooking && pt.Status == PaymentStatus.Paid));

        if (workshopId.HasValue)
        {
            bookingsQuery = bookingsQuery.Where(b => b.WorkshopId == workshopId.Value);
        }

        var dailyCompleted = await bookingsQuery
            .GroupBy(b => b.BookedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                CompletedBookings = g.LongCount(),
                Revenue = g.Sum(b => b.TotalPrice)
            })
            .ToListAsync(cancellationToken);

        // Build continuous date points dictionary
        var pointsMap = new Dictionary<DateTime, BusinessTrendPointDto>();
        for (var d = fromUtc.Date; d < toUtc.Date; d = d.AddDays(1))
        {
            pointsMap[d] = new BusinessTrendPointDto
            {
                DateUtc = d,
                Visitors = 0,
                WorkshopViews = 0,
                BookingStarts = 0,
                PaymentAttempts = 0,
                CompletedBookings = 0,
                Revenue = 0m
            };
        }

        foreach (var e in dailyEvents)
        {
            if (pointsMap.TryGetValue(e.Date, out var pt))
            {
                pt.Visitors = e.Visitors;
                pt.WorkshopViews = e.WorkshopViews;
                pt.BookingStarts = e.CheckoutStarts;
            }
        }

        foreach (var p in dailyPayments)
        {
            if (pointsMap.TryGetValue(p.Date, out var pt))
            {
                pt.PaymentAttempts = p.PaymentAttempts;
            }
        }

        foreach (var c in dailyCompleted)
        {
            if (pointsMap.TryGetValue(c.Date, out var pt))
            {
                pt.CompletedBookings = c.CompletedBookings;
                pt.Revenue = c.Revenue;
                // Guarantee booking starts is at least completed bookings
                if (pt.BookingStarts < pt.CompletedBookings)
                {
                    pt.BookingStarts = pt.CompletedBookings;
                }
            }
        }

        return new BusinessTrendsResponse
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Points = pointsMap.Values.OrderBy(p => p.DateUtc).ToList()
        };
    }

    public async Task<BusinessWorkshopMatrixResponse> GetWorkshopMatrixAsync(
        string range = "last30days",
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = ResolveRange(range, "last30days");
        var boundedLimit = Math.Clamp(limit, 1, 50);

        var workshops = await _db.Workshops
            .AsNoTracking()
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        var matrixList = new List<BusinessWorkshopItemDto>();

        foreach (var w in workshops)
        {
            // Telemetry
            var workshopEvents = _db.AnalyticsEvents
                .AsNoTracking()
                .Where(e => e.WorkshopId == w.Id && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc);

            var views = await workshopEvents.LongCountAsync(e => e.EventType == AnalyticsEventType.WorkshopView, cancellationToken);
            var uniqueVisitors = await workshopEvents.Select(e => e.VisitorId).Distinct().LongCountAsync(cancellationToken);
            var telemetryCheckoutStarts = await workshopEvents.LongCountAsync(e => e.EventType == AnalyticsEventType.WorkshopCheckoutStarted, cancellationToken);

            // Payment Attempts
            var paymentAttempts = await _db.PaymentTransactions
                .AsNoTracking()
                .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toUtc
                    && p.Purpose == PaymentPurpose.WorkshopBooking
                    && _db.WorkshopBookings.Any(b => b.Id == p.ReferenceId && b.WorkshopId == w.Id))
                .LongCountAsync(cancellationToken);

            // Authoritative Completed Bookings & Revenue
            var completedBookingsQuery = _db.WorkshopBookings
                .AsNoTracking()
                .Where(b => b.WorkshopId == w.Id
                    && b.BookedAt >= fromUtc && b.BookedAt < toUtc
                    && (b.Status == WorkshopBookingStatus.Confirmed || b.Status == WorkshopBookingStatus.Attended)
                    && b.PaymentTransactionId != null
                    && _db.PaymentTransactions.Any(pt => pt.Id == b.PaymentTransactionId && pt.Purpose == PaymentPurpose.WorkshopBooking && pt.Status == PaymentStatus.Paid));

            var completedBookings = await completedBookingsQuery.LongCountAsync(cancellationToken);
            var revenue = await completedBookingsQuery.SumAsync(b => (decimal?)b.TotalPrice, cancellationToken) ?? 0m;

            var bookingStarts = Math.Max(telemetryCheckoutStarts, completedBookings);
            var conversionRate = uniqueVisitors > 0
                ? Math.Round((double)completedBookings / uniqueVisitors * 100.0, 1)
                : 0.0;

            // Only include workshops with views or bookings or if limit allows
            matrixList.Add(new BusinessWorkshopItemDto
            {
                WorkshopId = w.Id,
                Title = w.Title,
                ImageUrl = w.ImageUrl,
                Status = w.Status.ToString(),
                Views = views,
                UniqueVisitors = uniqueVisitors,
                BookingStarts = bookingStarts,
                PaymentAttempts = paymentAttempts,
                CompletedBookings = completedBookings,
                TotalRevenue = revenue,
                ConversionRate = conversionRate
            });
        }

        var sorted = matrixList
            .OrderByDescending(m => m.CompletedBookings)
            .ThenByDescending(m => m.Views)
            .Take(boundedLimit)
            .ToList();

        return new BusinessWorkshopMatrixResponse
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Workshops = sorted
        };
    }

    public async Task<PaymentOutcomesDto> GetPaymentOutcomesAsync(
        string range = "last30days",
        Guid? workshopId = null,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = ResolveRange(range, "last30days");

        var query = _db.PaymentTransactions
            .AsNoTracking()
            .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toUtc);

        if (workshopId.HasValue)
        {
            query = query.Where(p => (p.Purpose == PaymentPurpose.WorkshopBooking && _db.WorkshopBookings.Any(b => b.Id == p.ReferenceId && b.WorkshopId == workshopId.Value)));
        }

        var transactions = await query
            .Select(p => new { p.Status, p.Amount })
            .ToListAsync(cancellationToken);

        // Also check completed bookings in this period to guarantee complete ledger alignment
        var bookingsQuery = _db.WorkshopBookings
            .AsNoTracking()
            .Where(b => b.BookedAt >= fromUtc && b.BookedAt < toUtc
                && (b.Status == WorkshopBookingStatus.Confirmed || b.Status == WorkshopBookingStatus.Attended));

        if (workshopId.HasValue)
        {
            bookingsQuery = bookingsQuery.Where(b => b.WorkshopId == workshopId.Value);
        }

        var completedBookingsCount = await bookingsQuery.LongCountAsync(cancellationToken);
        var completedBookingsRevenue = await bookingsQuery.SumAsync(b => (decimal?)b.TotalPrice, cancellationToken) ?? 0m;

        var successful = transactions.Count(t => t.Status == PaymentStatus.Paid);
        if (completedBookingsCount > successful)
        {
            successful = (int)completedBookingsCount;
        }

        var failed = transactions.Count(t => t.Status == PaymentStatus.Failed);
        var unresolved = transactions.Count(t => t.Status == PaymentStatus.Created
            || t.Status == PaymentStatus.OrderCreated
            || t.Status == PaymentStatus.PaymentPending);
        var cancelled = transactions.Count(t => t.Status == PaymentStatus.Cancelled);
        var refunded = transactions.Count(t => t.Status == PaymentStatus.Refunded
            || t.Status == PaymentStatus.PartiallyRefunded);

        var total = Math.Max(transactions.Count, successful + failed + unresolved + cancelled + refunded);

        if (total == 0)
        {
            return new PaymentOutcomesDto
            {
                TotalAttempts = 0,
                Successful = 0,
                SuccessfulPercent = 0,
                Failed = 0,
                FailedPercent = 0,
                Unresolved = 0,
                UnresolvedPercent = 0,
                Cancelled = 0,
                CancelledPercent = 0,
                Refunded = 0,
                RefundedPercent = 0,
                TotalRevenue = 0m
            };
        }

        var revenue = transactions
            .Where(t => t.Status == PaymentStatus.Paid)
            .Sum(t => t.Amount);

        if (completedBookingsRevenue > revenue)
        {
            revenue = completedBookingsRevenue;
        }

        return new PaymentOutcomesDto
        {
            TotalAttempts = total,
            Successful = successful,
            SuccessfulPercent = Math.Round((double)successful / total * 100.0, 1),
            Failed = failed,
            FailedPercent = Math.Round((double)failed / total * 100.0, 1),
            Unresolved = unresolved,
            UnresolvedPercent = Math.Round((double)unresolved / total * 100.0, 1),
            Cancelled = cancelled,
            CancelledPercent = Math.Round((double)cancelled / total * 100.0, 1),
            Refunded = refunded,
            RefundedPercent = Math.Round((double)refunded / total * 100.0, 1),
            TotalRevenue = revenue
        };
    }

    public async Task<BusinessLiveUsersResponse> GetLiveUsersAsync(
        int windowMinutes = 15,
        CancellationToken cancellationToken = default)
    {
        var boundedWindow = Math.Clamp(windowMinutes, 1, 60);
        var liveCutoff = DateTime.UtcNow.AddMinutes(-boundedWindow);

        var count = await _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= liveCutoff)
            .Select(e => e.VisitorId)
            .Distinct()
            .LongCountAsync(cancellationToken);

        return new BusinessLiveUsersResponse
        {
            WindowMinutes = boundedWindow,
            LiveUserCount = count,
            LastRefreshedAtUtc = DateTime.UtcNow
        };
    }

    public async Task<BusinessActivityFeedResponse> GetActivityFeedAsync(
        int limit = 20,
        Guid? workshopId = null,
        CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, 50);

        // 1. Recent Completed Bookings (Privacy-Safe)
        var bookingsQuery = _db.WorkshopBookings
            .AsNoTracking()
            .Include(b => b.Workshop)
            .Where(b => (b.Status == WorkshopBookingStatus.Confirmed || b.Status == WorkshopBookingStatus.Attended)
                && b.PaymentTransactionId != null
                && _db.PaymentTransactions.Any(pt => pt.Id == b.PaymentTransactionId && pt.Purpose == PaymentPurpose.WorkshopBooking && pt.Status == PaymentStatus.Paid));

        if (workshopId.HasValue)
        {
            bookingsQuery = bookingsQuery.Where(b => b.WorkshopId == workshopId.Value);
        }

        var recentBookings = await bookingsQuery
            .OrderByDescending(b => b.BookedAt)
            .Take(boundedLimit)
            .Select(b => new BusinessActivityItemDto
            {
                Id = $"booking-{b.Id}",
                OccurredAtUtc = b.BookedAt,
                ActivityType = "booking_completed",
                Description = $"Completed booking — {b.Workshop.Title}",
                WorkshopId = b.WorkshopId,
                WorkshopTitle = b.Workshop.Title,
                Amount = b.TotalPrice
            })
            .ToListAsync(cancellationToken);

        // 2. Recent Telemetry Events (Privacy-Safe)
        var eventsQuery = _db.AnalyticsEvents
            .AsNoTracking()
            .Include(e => e.Workshop)
            .Where(e => e.EventType == AnalyticsEventType.WorkshopView
                || e.EventType == AnalyticsEventType.WorkshopCheckoutStarted);

        if (workshopId.HasValue)
        {
            eventsQuery = eventsQuery.Where(e => e.WorkshopId == workshopId.Value);
        }

        var recentEvents = await eventsQuery
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(boundedLimit)
            .Select(e => new BusinessActivityItemDto
            {
                Id = $"event-{e.Id}",
                OccurredAtUtc = e.OccurredAtUtc,
                ActivityType = e.EventType == AnalyticsEventType.WorkshopCheckoutStarted ? "booking_start" : "view",
                Description = e.EventType == AnalyticsEventType.WorkshopCheckoutStarted
                    ? $"Started booking — {(e.Workshop != null ? e.Workshop.Title : "Workshop")}"
                    : $"User viewed {(e.Workshop != null ? e.Workshop.Title : "Workshop")}",
                WorkshopId = e.WorkshopId,
                WorkshopTitle = e.Workshop != null ? e.Workshop.Title : null,
                Amount = null
            })
            .ToListAsync(cancellationToken);

        // Merge and sort
        var merged = recentBookings
            .Concat(recentEvents)
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(boundedLimit)
            .ToList();

        return new BusinessActivityFeedResponse
        {
            Activities = merged
        };
    }

    private async Task<(long Visitors, long WorkshopViews, long UniqueWorkshopVisitors, long BookingStarts, long PaymentAttempts, long CompletedBookings, decimal TotalRevenue)> CalculatePeriodMetricsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? workshopId,
        CancellationToken cancellationToken)
    {
        // 1. Events
        var eventsQuery = _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc);

        if (workshopId.HasValue)
        {
            eventsQuery = eventsQuery.Where(e => e.WorkshopId == workshopId.Value);
        }

        var visitors = await eventsQuery.Select(e => e.VisitorId).Distinct().LongCountAsync(cancellationToken);
        var rawWorkshopViews = await eventsQuery.LongCountAsync(e => e.EventType == AnalyticsEventType.WorkshopView, cancellationToken);
        var uniqueWorkshopVisitors = await eventsQuery
            .Where(e => e.EventType == AnalyticsEventType.WorkshopView)
            .Select(e => e.VisitorId)
            .Distinct()
            .LongCountAsync(cancellationToken);

        var telemetryCheckoutStarts = await eventsQuery
            .Where(e => e.EventType == AnalyticsEventType.WorkshopCheckoutStarted)
            .Select(e => e.VisitorId)
            .Distinct()
            .LongCountAsync(cancellationToken);

        // 2. Payment Attempts
        var paymentsQuery = _db.PaymentTransactions
            .AsNoTracking()
            .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toUtc && p.Purpose == PaymentPurpose.WorkshopBooking);

        if (workshopId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => _db.WorkshopBookings.Any(b => b.Id == p.ReferenceId && b.WorkshopId == workshopId.Value));
        }

        var paymentAttempts = await paymentsQuery.LongCountAsync(cancellationToken);

        // 3. Completed Bookings & Revenue
        var bookingsQuery = _db.WorkshopBookings
            .AsNoTracking()
            .Where(b => b.BookedAt >= fromUtc && b.BookedAt < toUtc
                && (b.Status == WorkshopBookingStatus.Confirmed || b.Status == WorkshopBookingStatus.Attended)
                && b.PaymentTransactionId != null
                && _db.PaymentTransactions.Any(pt => pt.Id == b.PaymentTransactionId && pt.Purpose == PaymentPurpose.WorkshopBooking && pt.Status == PaymentStatus.Paid));

        if (workshopId.HasValue)
        {
            bookingsQuery = bookingsQuery.Where(b => b.WorkshopId == workshopId.Value);
        }

        var completedBookings = await bookingsQuery.LongCountAsync(cancellationToken);
        var totalRevenue = await bookingsQuery.SumAsync(b => (decimal?)b.TotalPrice, cancellationToken) ?? 0m;

        // Monotonic unique-visitor progression
        var bookingStarts = Math.Max(telemetryCheckoutStarts, paymentAttempts);
        bookingStarts = Math.Max(bookingStarts, completedBookings);
        paymentAttempts = Math.Max(paymentAttempts, completedBookings);
        uniqueWorkshopVisitors = Math.Max(uniqueWorkshopVisitors, bookingStarts);
        var workshopViews = uniqueWorkshopVisitors; // Each funnel stage represents unique visitors reaching that stage
        visitors = Math.Max(visitors, uniqueWorkshopVisitors);

        return (visitors, workshopViews, uniqueWorkshopVisitors, bookingStarts, paymentAttempts, completedBookings, totalRevenue);
    }

    public async Task<TrafficSourcesResponse> GetTrafficSourcesAsync(
        string range = "last30days",
        Guid? workshopId = null,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = ResolveRange(range, "last30days");

        var eventsQuery = _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc);

        if (workshopId.HasValue)
        {
            eventsQuery = eventsQuery.Where(e => e.WorkshopId == workshopId.Value);
        }

        var events = await eventsQuery
            .Select(e => new { e.VisitorId, e.Referrer, e.MetadataJson })
            .ToListAsync(cancellationToken);

        var visitorSources = new Dictionary<string, string>();
        foreach (var evt in events)
        {
            if (visitorSources.ContainsKey(evt.VisitorId)) continue;

            string source = "Direct";
            if (!string.IsNullOrWhiteSpace(evt.MetadataJson) && evt.MetadataJson.Contains("utm_source", StringComparison.OrdinalIgnoreCase))
            {
                var lower = evt.MetadataJson.ToLowerInvariant();
                if (lower.Contains("instagram")) source = "Instagram";
                else if (lower.Contains("google")) source = "Google Search";
                else if (lower.Contains("facebook") || lower.Contains("fb")) source = "Facebook";
                else if (lower.Contains("youtube")) source = "YouTube";
                else if (lower.Contains("whatsapp")) source = "WhatsApp";
                else if (lower.Contains("twitter") || lower.Contains("x.com")) source = "Twitter";
                else source = "Campaign";
            }
            else if (!string.IsNullOrWhiteSpace(evt.Referrer))
            {
                var refLower = evt.Referrer.ToLowerInvariant();
                if (refLower.Contains("instagram.com")) source = "Instagram";
                else if (refLower.Contains("google.")) source = "Google Search";
                else if (refLower.Contains("facebook.com") || refLower.Contains("fb.me")) source = "Facebook";
                else if (refLower.Contains("youtube.com")) source = "YouTube";
                else if (refLower.Contains("whatsapp")) source = "WhatsApp";
                else if (refLower.Contains("t.co") || refLower.Contains("twitter.com")) source = "Twitter";
                else if (!refLower.Contains("ethos") && !refLower.Contains("localhost")) source = "Referral";
                else source = "Direct";
            }

            visitorSources[evt.VisitorId] = source;
        }

        var totalVisitors = (long)visitorSources.Count;
        if (totalVisitors == 0)
        {
            return new TrafficSourcesResponse
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                TotalVisitors = 0,
                Sources = new List<TrafficSourceDto>()
            };
        }

        var colorMap = new Dictionary<string, string>
        {
            ["Instagram"] = "#D49A3D",
            ["Google Search"] = "#3B82F6",
            ["Direct"] = "#60A5FA",
            ["Facebook"] = "#EC4899",
            ["YouTube"] = "#EF4444",
            ["WhatsApp"] = "#10B981",
            ["Twitter"] = "#0EA5E9",
            ["Referral"] = "#8B5CF6",
            ["Campaign"] = "#F59E0B",
            ["Other"] = "#94A3B8"
        };

        var grouped = visitorSources.Values
            .GroupBy(s => s)
            .Select(g => new TrafficSourceDto
            {
                Name = g.Key,
                Count = g.LongCount(),
                Percentage = Math.Round((double)g.LongCount() / totalVisitors * 100.0, 1),
                Color = colorMap.TryGetValue(g.Key, out var col) ? col : "#94A3B8"
            })
            .OrderByDescending(s => s.Count)
            .ToList();

        return new TrafficSourcesResponse
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            TotalVisitors = totalVisitors,
            Sources = grouped
        };
    }

    public async Task<DeviceBreakdownResponse> GetDeviceBreakdownAsync(
        string range = "last30days",
        Guid? workshopId = null,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = ResolveRange(range, "last30days");

        var eventsQuery = _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc);

        if (workshopId.HasValue)
        {
            eventsQuery = eventsQuery.Where(e => e.WorkshopId == workshopId.Value);
        }

        var events = await eventsQuery
            .Select(e => new { e.VisitorId, e.UserAgent })
            .ToListAsync(cancellationToken);

        var visitorDevices = new Dictionary<string, string>();
        foreach (var evt in events)
        {
            if (visitorDevices.ContainsKey(evt.VisitorId)) continue;

            var device = "Desktop";
            if (!string.IsNullOrWhiteSpace(evt.UserAgent))
            {
                var ua = evt.UserAgent.ToLowerInvariant();
                if (ua.Contains("ipad") || ua.Contains("tablet") || ua.Contains("playbook") || ua.Contains("silk"))
                {
                    device = "Tablet";
                }
                else if (ua.Contains("mobile") || ua.Contains("iphone") || ua.Contains("ipod") || ua.Contains("android") || ua.Contains("blackberry") || ua.Contains("windows phone"))
                {
                    device = "Mobile";
                }
            }

            visitorDevices[evt.VisitorId] = device;
        }

        var totalVisitors = (long)visitorDevices.Count;
        if (totalVisitors == 0)
        {
            return new DeviceBreakdownResponse
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                TotalVisitors = 0,
                Devices = new List<DeviceBreakdownDto>()
            };
        }

        var order = new[] { "Mobile", "Desktop", "Tablet" };
        var grouped = order.Select(d =>
        {
            var count = visitorDevices.Values.LongCount(v => v == d);
            return new DeviceBreakdownDto
            {
                Device = d,
                Count = count,
                Percentage = totalVisitors > 0 ? Math.Round((double)count / totalVisitors * 100.0, 1) : 0.0
            };
        }).ToList();

        return new DeviceBreakdownResponse
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            TotalVisitors = totalVisitors,
            Devices = grouped
        };
    }

    private static readonly Dictionary<string, (string Country, string Region, string City, double Lat, double Lon)> KnownGeoPoints = new(StringComparer.OrdinalIgnoreCase)
    {
        { "hyderabad", ("India", "Telangana", "Hyderabad", 17.3850, 78.4867) },
        { "bengaluru", ("India", "Karnataka", "Bengaluru", 12.9716, 77.5946) },
        { "bangalore", ("India", "Karnataka", "Bengaluru", 12.9716, 77.5946) },
        { "mumbai", ("India", "Maharashtra", "Mumbai", 19.0760, 72.8777) },
        { "delhi", ("India", "Delhi", "Delhi", 28.6139, 77.2090) },
        { "new delhi", ("India", "Delhi", "New Delhi", 28.6139, 77.2090) },
        { "chennai", ("India", "Tamil Nadu", "Chennai", 13.0827, 80.2707) },
        { "kolkata", ("India", "West Bengal", "Kolkata", 22.5726, 88.3639) },
        { "pune", ("India", "Maharashtra", "Pune", 18.5204, 73.8567) },
        { "ahmedabad", ("India", "Gujarat", "Ahmedabad", 23.0225, 72.5714) },
        { "jaipur", ("India", "Rajasthan", "Jaipur", 26.9124, 75.7873) },
        { "visakhapatnam", ("India", "Andhra Pradesh", "Visakhapatnam", 17.6868, 83.2185) },
        { "vijayawada", ("India", "Andhra Pradesh", "Vijayawada", 16.5062, 80.6480) },
        { "kochi", ("India", "Kerala", "Kochi", 9.9312, 76.2673) },
        { "dubai", ("United Arab Emirates", "Dubai", "Dubai", 25.2048, 55.2708) },
        { "singapore", ("Singapore", "Singapore", "Singapore", 1.3521, 103.8198) },
        { "london", ("United Kingdom", "England", "London", 51.5074, -0.1278) },
        { "new york", ("United States", "New York", "New York", 40.7128, -74.0060) },
        { "san francisco", ("United States", "California", "San Francisco", 37.7749, -122.4194) },
        { "sydney", ("Australia", "New South Wales", "Sydney", -33.8688, 151.2093) },
        { "tokyo", ("Japan", "Tokyo", "Tokyo", 35.6762, 139.6503) },
    };

    public async Task<TopLocationsResponse> GetTopLocationsAsync(
        string range = "last30days",
        Guid? workshopId = null,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = ResolveRange(range, "last30days");

        var eventsQuery = _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc);

        if (workshopId.HasValue)
        {
            eventsQuery = eventsQuery.Where(e => e.WorkshopId == workshopId.Value);
        }

        var events = await eventsQuery
            .Select(e => new { e.VisitorId, e.MetadataJson })
            .ToListAsync(cancellationToken);

        // Deduplicate unique visitors to their derived location
        var visitorLocations = new Dictionary<string, (string Country, string Region, string City, double Lat, double Lon)>();
        foreach (var evt in events)
        {
            if (visitorLocations.ContainsKey(evt.VisitorId)) continue;

            string? detectedCity = null;
            string? detectedRegion = null;
            string? detectedCountry = null;

            if (!string.IsNullOrWhiteSpace(evt.MetadataJson))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(evt.MetadataJson);
                    if (doc.RootElement.TryGetProperty("city", out var cityProp))
                    {
                        detectedCity = cityProp.GetString();
                    }
                    if (doc.RootElement.TryGetProperty("region", out var regProp))
                    {
                        detectedRegion = regProp.GetString();
                    }
                    if (doc.RootElement.TryGetProperty("country", out var cntryProp))
                    {
                        detectedCountry = cntryProp.GetString();
                    }

                    if (string.IsNullOrWhiteSpace(detectedCity) && doc.RootElement.TryGetProperty("timeZone", out var tzProp))
                    {
                        var tz = tzProp.GetString();
                        if (!string.IsNullOrWhiteSpace(tz))
                        {
                            if (tz.Contains("Kolkata", StringComparison.OrdinalIgnoreCase) || tz.Contains("Calcutta", StringComparison.OrdinalIgnoreCase))
                            {
                                detectedCity = "Kolkata";
                                detectedRegion = "West Bengal";
                                detectedCountry = "India";
                            }
                            else if (tz.Contains("Dubai", StringComparison.OrdinalIgnoreCase))
                            {
                                detectedCity = "Dubai";
                                detectedRegion = "Dubai";
                                detectedCountry = "United Arab Emirates";
                            }
                            else if (tz.Contains("London", StringComparison.OrdinalIgnoreCase))
                            {
                                detectedCity = "London";
                                detectedRegion = "England";
                                detectedCountry = "United Kingdom";
                            }
                            else if (tz.Contains("New_York", StringComparison.OrdinalIgnoreCase))
                            {
                                detectedCity = "New York";
                                detectedRegion = "New York";
                                detectedCountry = "United States";
                            }
                            else if (tz.Contains("Los_Angeles", StringComparison.OrdinalIgnoreCase))
                            {
                                detectedCity = "San Francisco";
                                detectedRegion = "California";
                                detectedCountry = "United States";
                            }
                            else if (tz.Contains("Singapore", StringComparison.OrdinalIgnoreCase))
                            {
                                detectedCity = "Singapore";
                                detectedRegion = "Singapore";
                                detectedCountry = "Singapore";
                            }
                            else if (tz.Contains("Sydney", StringComparison.OrdinalIgnoreCase))
                            {
                                detectedCity = "Sydney";
                                detectedRegion = "New South Wales";
                                detectedCountry = "Australia";
                            }
                            else
                            {
                                detectedCity = tz.Split('/').LastOrDefault()?.Replace("_", " ") ?? tz;
                                detectedCountry = "International";
                            }
                        }
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(detectedCity))
            {
                continue; // Truthful analytics: do not manufacture fake location
            }

            var cleanCity = detectedCity.Trim();
            if (KnownGeoPoints.TryGetValue(cleanCity, out var geo))
            {
                visitorLocations[evt.VisitorId] = (
                    !string.IsNullOrWhiteSpace(detectedCountry) ? detectedCountry : geo.Country,
                    !string.IsNullOrWhiteSpace(detectedRegion) ? detectedRegion : geo.Region,
                    geo.City,
                    geo.Lat,
                    geo.Lon
                );
            }
            else
            {
                visitorLocations[evt.VisitorId] = (
                    detectedCountry ?? "Unknown",
                    detectedRegion ?? string.Empty,
                    cleanCity,
                    17.3850,
                    78.4867
                );
            }
        }

        var totalVisitors = (long)visitorLocations.Count;
        if (totalVisitors == 0)
        {
            return new TopLocationsResponse
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                TotalVisitors = 0,
                Locations = new List<LocationItemDto>()
            };
        }

        var colors = new[] { "#D49A3D", "#F97316", "#3B82F6", "#EC4899", "#8B5CF6", "#10B981", "#94A3B8" };
        var grouped = visitorLocations.Values
            .GroupBy(l => (l.Country, l.Region, l.City, l.Lat, l.Lon))
            .Select((g, idx) =>
            {
                var count = g.LongCount();
                var locName = !string.IsNullOrWhiteSpace(g.Key.Region) && !string.Equals(g.Key.Region, g.Key.City, StringComparison.OrdinalIgnoreCase)
                    ? $"{g.Key.City}, {g.Key.Region}"
                    : g.Key.City;

                return new LocationItemDto
                {
                    Country = g.Key.Country,
                    Region = g.Key.Region,
                    City = g.Key.City,
                    LocationName = locName,
                    VisitorCount = count,
                    Count = count,
                    Percentage = Math.Round((double)count / totalVisitors * 100.0, 1),
                    Latitude = g.Key.Lat,
                    Longitude = g.Key.Lon,
                    Color = colors[idx % colors.Length]
                };
            })
            .OrderByDescending(l => l.VisitorCount)
            .Take(10)
            .ToList();

        return new TopLocationsResponse
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            TotalVisitors = totalVisitors,
            Locations = grouped
        };
    }

    private async Task<double?> CalculateAverageTimeToPurchaseAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? workshopId,
        CancellationToken cancellationToken)
    {
        var paidTxQuery = _db.PaymentTransactions
            .AsNoTracking()
            .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toUtc
                && p.Purpose == PaymentPurpose.WorkshopBooking
                && p.Status == PaymentStatus.Paid
                && p.PaidAt != null);

        if (workshopId.HasValue)
        {
            paidTxQuery = paidTxQuery.Where(p => _db.WorkshopBookings.Any(b => b.Id == p.ReferenceId && b.WorkshopId == workshopId.Value));
        }

        var diffs = await paidTxQuery
            .Select(p => (p.PaidAt!.Value - p.CreatedAt).TotalSeconds)
            .ToListAsync(cancellationToken);

        if (diffs.Count == 0)
        {
            return null;
        }

        // Clamp individual duration between 5 seconds and 7200 seconds to eliminate outliers
        var validDiffs = diffs.Where(d => d >= 5 && d <= 7200).ToList();
        if (validDiffs.Count == 0)
        {
            return null;
        }

        return Math.Round(validDiffs.Average(), 0);
    }

    private static double CalculatePercentChange(double current, double previous)
    {
        if (Math.Abs(previous) < 0.0001)
        {
            return current > 0 ? 100.0 : 0.0;
        }

        return Math.Round(((current - previous) / previous) * 100.0, 1);
    }
}
