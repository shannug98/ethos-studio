using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Command Dashboard")]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _dashboardService;
    private readonly IAdminAuthorizationService _authService;

    public AdminDashboardController(
        IAdminDashboardService dashboardService,
        IAdminAuthorizationService authService)
    {
        _dashboardService = dashboardService;
        _authService = authService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminCommandDashboardResponse>> GetCommandDashboard(
        [FromQuery] string range = "week",
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AdminDashboardView,
            "DASHBOARD",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _dashboardService.GetCommandDashboardAsync(range, cancellationToken);
        return Ok(result);
    }

    [HttpGet("legacy-metrics")]
    public async Task<ActionResult<AdminDashboardResponse>> GetLegacyDashboardMetrics(
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AdminDashboardView,
            "DASHBOARD",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _dashboardService.GetDashboardMetricsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("dashboard/day/{date}")]
    [HttpGet("dashboard/daily/{date}")]
    [HttpGet("dashboard/daily-activity")]
    [HttpGet("dashboard/daily")]
    public async Task<ActionResult<AdminDailyActivityResponse>> GetDailyActivity(
        [FromQuery] DateOnly? date,
        [FromRoute] DateOnly? routeDate,
        CancellationToken cancellationToken = default)
    {
        var targetDate = date ?? routeDate;
        if (!targetDate.HasValue)
        {
            targetDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AdminDashboardView,
            "DASHBOARD",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _dashboardService.GetDailyActivityAsync(targetDate.Value, cancellationToken);
        return Ok(result);
    }

    // =========================================================================
    // VISUAL REFERENCE BOUNDED ENDPOINTS
    // =========================================================================

    [HttpGet("dashboard/summary")]
    public async Task<ActionResult<AdminDashboardSummaryDto>> GetDashboardSummary(
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AdminDashboardView,
            "DASHBOARD_SUMMARY",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _dashboardService.GetSummaryAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("dashboard/trends")]
    public async Task<ActionResult<AdminDashboardTrendsDto>> GetDashboardTrends(
        [FromQuery] string range = "last6months",
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AdminDashboardView,
            "DASHBOARD_TRENDS",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _dashboardService.GetTrendsAsync(range, cancellationToken);
        return Ok(result);
    }

    [HttpGet("dashboard/workshop-status")]
    public async Task<ActionResult<AdminWorkshopStatusDonutDto>> GetWorkshopStatusDistribution(
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AdminDashboardView,
            "DASHBOARD_WORKSHOP_STATUS",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _dashboardService.GetWorkshopStatusDistributionAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("dashboard/priorities")]
    public async Task<ActionResult<AdminDashboardPrioritiesDto>> GetDashboardPriorities(
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AdminDashboardView,
            "DASHBOARD_PRIORITIES",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _dashboardService.GetPrioritiesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("dashboard/recent-bookings")]
    public async Task<ActionResult<List<AdminRecentBookingDto>>> GetRecentBookings(
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, 20);
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.BookingView,
            "BOOKING",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _dashboardService.GetRecentBookingsAsync(boundedLimit, cancellationToken);
        return Ok(result);
    }

    [HttpGet("dashboard/upcoming-workshops")]
    public async Task<ActionResult<List<AdminUpcomingWorkshopDto>>> GetUpcomingWorkshops(
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, 20);
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.WorkshopView,
            "WORKSHOP",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _dashboardService.GetUpcomingWorkshopsAsync(boundedLimit, cancellationToken);
        return Ok(result);
    }

    [HttpGet("dashboard/activity")]
    public async Task<ActionResult<List<AdminAuditActivityDto>>> GetRecentActivity(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, 50);
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AdminAuditView,
            "AUDIT",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _dashboardService.GetRecentActivityFeedAsync(boundedLimit, cancellationToken);
        return Ok(result);
    }

    [HttpGet("dashboard/system-health")]
    public async Task<ActionResult<AdminSystemHealthDto>> GetSystemHealth(
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.ObservabilityView,
            "OBSERVABILITY",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _dashboardService.GetSystemHealthAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("dashboard/revenue-overview")]
    public async Task<ActionResult<AdminRevenueOverviewDto>> GetRevenueOverview(
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.PaymentView,
            "PAYMENT",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _dashboardService.GetRevenueOverviewAsync(cancellationToken);
        return Ok(result);
    }
}
