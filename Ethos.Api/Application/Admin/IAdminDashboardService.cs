using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Application.Admin;

public interface IAdminDashboardService
{
    Task<AdminDashboardResponse> GetDashboardMetricsAsync(CancellationToken cancellationToken = default);
    Task<AdminCommandDashboardResponse> GetCommandDashboardAsync(string range = "week", CancellationToken cancellationToken = default);
    Task<AdminDailyActivityResponse> GetDailyActivityAsync(DateOnly date, CancellationToken cancellationToken = default);
}
