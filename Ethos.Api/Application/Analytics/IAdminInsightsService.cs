using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Application.Analytics;

public interface IAdminInsightsService
{
    Task<BusinessOverviewResponse> GetOverviewAsync(
        string range = "last30days",
        Guid? workshopId = null,
        int windowMinutes = 15,
        CancellationToken cancellationToken = default);

    Task<BusinessTrendsResponse> GetTrendsAsync(
        string range = "last30days",
        Guid? workshopId = null,
        CancellationToken cancellationToken = default);

    Task<BusinessWorkshopMatrixResponse> GetWorkshopMatrixAsync(
        string range = "last30days",
        int limit = 10,
        CancellationToken cancellationToken = default);

    Task<PaymentOutcomesDto> GetPaymentOutcomesAsync(
        string range = "last30days",
        Guid? workshopId = null,
        CancellationToken cancellationToken = default);

    Task<BusinessLiveUsersResponse> GetLiveUsersAsync(
        int windowMinutes = 15,
        CancellationToken cancellationToken = default);

    Task<BusinessActivityFeedResponse> GetActivityFeedAsync(
        int limit = 20,
        Guid? workshopId = null,
        CancellationToken cancellationToken = default);

    Task<TrafficSourcesResponse> GetTrafficSourcesAsync(
        string range = "last30days",
        Guid? workshopId = null,
        CancellationToken cancellationToken = default);

    Task<DeviceBreakdownResponse> GetDeviceBreakdownAsync(
        string range = "last30days",
        Guid? workshopId = null,
        CancellationToken cancellationToken = default);

    Task<TopLocationsResponse> GetTopLocationsAsync(
        string range = "last30days",
        Guid? workshopId = null,
        CancellationToken cancellationToken = default);
}
