using Ethos.Api.Contracts.Analytics;

namespace Ethos.Api.Application.Analytics;

public interface IAnalyticsQueryService
{
    Task<AnalyticsSummaryResponse> GetSummaryAsync(
        string range = "last7days",
        CancellationToken cancellationToken = default);

    Task<AnalyticsTrendsResponse> GetTrendsAsync(
        string range = "last30days",
        CancellationToken cancellationToken = default);

    Task<AnalyticsEventBreakdownResponse> GetEventBreakdownAsync(
        string range = "last30days",
        CancellationToken cancellationToken = default);

    Task<AnalyticsWorkshopResponse> GetWorkshopAnalyticsAsync(
        string range = "last30days",
        int limit = 10,
        CancellationToken cancellationToken = default);

    Task<AnalyticsRecentEventsResponse> GetRecentEventsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default);
}
