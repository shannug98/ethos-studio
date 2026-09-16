using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Application.Admin;

public interface IAdminDashboardService
{
    Task<AdminDashboardResponse> GetDashboardMetricsAsync(CancellationToken cancellationToken = default);
    Task<AdminCommandDashboardResponse> GetCommandDashboardAsync(string range = "week", CancellationToken cancellationToken = default);
    Task<AdminDailyActivityResponse> GetDailyActivityAsync(DateOnly date, CancellationToken cancellationToken = default);

    // Visual Reference Bounded Methods
    Task<AdminDashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<AdminDashboardTrendsDto> GetTrendsAsync(string range = "last6months", CancellationToken cancellationToken = default);
    Task<AdminWorkshopStatusDonutDto> GetWorkshopStatusDistributionAsync(CancellationToken cancellationToken = default);
    Task<AdminDashboardPrioritiesDto> GetPrioritiesAsync(CancellationToken cancellationToken = default);
    Task<List<AdminRecentBookingDto>> GetRecentBookingsAsync(int limit = 5, CancellationToken cancellationToken = default);
    Task<List<AdminUpcomingWorkshopDto>> GetUpcomingWorkshopsAsync(int limit = 5, CancellationToken cancellationToken = default);
    Task<List<AdminAuditActivityDto>> GetRecentActivityFeedAsync(int limit = 10, CancellationToken cancellationToken = default);
    Task<AdminSystemHealthDto> GetSystemHealthAsync(CancellationToken cancellationToken = default);
    Task<AdminRevenueOverviewDto> GetRevenueOverviewAsync(CancellationToken cancellationToken = default);
}
