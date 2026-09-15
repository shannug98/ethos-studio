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
}
