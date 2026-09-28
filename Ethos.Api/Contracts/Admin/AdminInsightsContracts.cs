namespace Ethos.Api.Contracts.Admin;

public class BusinessFunnelDto
{
    public long Visitors { get; set; }
    public long WorkshopViews { get; set; }
    public long UniqueWorkshopVisitors { get; set; }
    public long BookingStarts { get; set; }
    public long PaymentAttempts { get; set; }
    public long CompletedBookings { get; set; }

    public double WorkshopViewRate { get; set; }
    public double BookingStartRate { get; set; }
    public double PaymentAttemptRate { get; set; }
    public double CompletedBookingRate { get; set; }
    public double OverallConversionRate { get; set; }
}

public class PaymentOutcomesDto
{
    public long TotalAttempts { get; set; }
    public long Successful { get; set; }
    public double SuccessfulPercent { get; set; }
    public long Failed { get; set; }
    public double FailedPercent { get; set; }
    public long Unresolved { get; set; }
    public double UnresolvedPercent { get; set; }
    public long Cancelled { get; set; }
    public double CancelledPercent { get; set; }
    public long Refunded { get; set; }
    public double RefundedPercent { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class BusinessOverviewResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public string Range { get; set; } = string.Empty;
    public Guid? WorkshopId { get; set; }
    public string? WorkshopTitle { get; set; }

    public long Visitors { get; set; }
    public long WorkshopViews { get; set; }
    public long UniqueWorkshopVisitors { get; set; }
    public long BookingStarts { get; set; }
    public long PaymentAttempts { get; set; }
    public long CompletedBookings { get; set; }
    public decimal TotalRevenue { get; set; }

    public double VisitorChangePercent { get; set; }
    public double WorkshopViewChangePercent { get; set; }
    public double BookingStartChangePercent { get; set; }
    public double PaymentAttemptChangePercent { get; set; }
    public double CompletedBookingChangePercent { get; set; }
    public double RevenueChangePercent { get; set; }

    public BusinessFunnelDto Funnel { get; set; } = new();
    public PaymentOutcomesDto PaymentOutcomes { get; set; } = new();
    public long LiveUsers { get; set; }
    public double? AverageTimeToPurchaseSeconds { get; set; }
}

public class BusinessTrendPointDto
{
    public DateTime DateUtc { get; set; }
    public long Visitors { get; set; }
    public long WorkshopViews { get; set; }
    public long BookingStarts { get; set; }
    public long PaymentAttempts { get; set; }
    public long CompletedBookings { get; set; }
    public decimal Revenue { get; set; }
}

public class BusinessTrendsResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public List<BusinessTrendPointDto> Points { get; set; } = new();
}

public class BusinessWorkshopItemDto
{
    public Guid WorkshopId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public long Views { get; set; }
    public long UniqueVisitors { get; set; }
    public long BookingStarts { get; set; }
    public long PaymentAttempts { get; set; }
    public long CompletedBookings { get; set; }
    public decimal TotalRevenue { get; set; }
    public double ConversionRate { get; set; }
}

public class BusinessWorkshopMatrixResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public List<BusinessWorkshopItemDto> Workshops { get; set; } = new();
}

public class BusinessLiveUsersResponse
{
    public int WindowMinutes { get; set; }
    public long LiveUserCount { get; set; }
    public DateTime LastRefreshedAtUtc { get; set; }
}

public class BusinessActivityItemDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? WorkshopId { get; set; }
    public string? WorkshopTitle { get; set; }
    public decimal? Amount { get; set; }
}

public class BusinessActivityFeedResponse
{
    public List<BusinessActivityItemDto> Activities { get; set; } = new();
}

public class TrafficSourceDto
{
    public string Name { get; set; } = string.Empty;
    public long Count { get; set; }
    public double Percentage { get; set; }
    public string Color { get; set; } = string.Empty;
}

public class TrafficSourcesResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public long TotalVisitors { get; set; }
    public List<TrafficSourceDto> Sources { get; set; } = new();
}

public class DeviceBreakdownDto
{
    public string Device { get; set; } = string.Empty;
    public long Count { get; set; }
    public double Percentage { get; set; }
}

public class DeviceBreakdownResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public long TotalVisitors { get; set; }
    public List<DeviceBreakdownDto> Devices { get; set; } = new();
}

public class LocationItemDto
{
    public string Country { get; set; } = "India";
    public string Region { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public long VisitorCount { get; set; }
    public long Count { get; set; } // Backward compatibility
    public double Percentage { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Color { get; set; } = string.Empty;
}

public class TopLocationsResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public long TotalVisitors { get; set; }
    public List<LocationItemDto> Locations { get; set; } = new();
}

