namespace Ethos.Api.Contracts.Analytics;

public class AnalyticsSummaryResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }

    public long TotalEvents { get; set; }
    public long UniqueVisitors { get; set; }
    public long UniqueSessions { get; set; }
    public long AuthenticatedEvents { get; set; }

    public long PageViews { get; set; }
    public long WorkshopViews { get; set; }
    public long CheckoutStarts { get; set; }
    public long CheckoutCompletions { get; set; }
    public long FeedbackOpens { get; set; }
    public long FeedbackSubmissions { get; set; }
    public long LoginStarts { get; set; }
    public long LoginCompletions { get; set; }
}

public class AnalyticsTrendPointDto
{
    public DateTime DateUtc { get; set; }
    public long TotalEvents { get; set; }
    public long UniqueVisitors { get; set; }
    public long UniqueSessions { get; set; }
}

public class AnalyticsTrendsResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public string Granularity { get; set; } = "day";

    public List<AnalyticsTrendPointDto> Points { get; set; } = new();
}

public class AnalyticsEventBreakdownDto
{
    public int EventType { get; set; }
    public string EventName { get; set; } = string.Empty;
    public long Count { get; set; }
    public long UniqueVisitors { get; set; }
}

public class AnalyticsEventBreakdownResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }

    public List<AnalyticsEventBreakdownDto> Events { get; set; } = new();
}

public class AnalyticsWorkshopDto
{
    public Guid WorkshopId { get; set; }
    public string WorkshopName { get; set; } = string.Empty;

    public long Views { get; set; }
    public long CheckoutStarts { get; set; }
    public long CheckoutCompletions { get; set; }

    public long UniqueVisitors { get; set; }
}

public class AnalyticsWorkshopResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }

    public List<AnalyticsWorkshopDto> Workshops { get; set; } = new();
}

public class AnalyticsRecentEventDto
{
    public Guid Id { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }

    public string VisitorId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;

    public Guid? WorkshopId { get; set; }
    public string? Path { get; set; }

    public bool IsAuthenticated { get; set; }
}

public class AnalyticsRecentEventsResponse
{
    public List<AnalyticsRecentEventDto> Events { get; set; } = new();
}
