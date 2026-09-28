using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Analytics;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/insights")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Business Insights")]
public class AdminInsightsController : ControllerBase
{
    private readonly IAdminInsightsService _insightsService;
    private readonly IAdminAuthorizationService _authService;

    public AdminInsightsController(
        IAdminInsightsService insightsService,
        IAdminAuthorizationService authService)
    {
        _insightsService = insightsService;
        _authService = authService;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<BusinessOverviewResponse>> GetOverview(
        [FromQuery] string range = "last30days",
        [FromQuery] Guid? workshopId = null,
        [FromQuery] int windowMinutes = 15,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AnalyticsView,
            "ANALYTICS",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        try
        {
            var result = await _insightsService.GetOverviewAsync(range, workshopId, windowMinutes, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("trends")]
    public async Task<ActionResult<BusinessTrendsResponse>> GetTrends(
        [FromQuery] string range = "last30days",
        [FromQuery] Guid? workshopId = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AnalyticsView,
            "ANALYTICS",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        try
        {
            var result = await _insightsService.GetTrendsAsync(range, workshopId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("workshops")]
    public async Task<ActionResult<BusinessWorkshopMatrixResponse>> GetWorkshopMatrix(
        [FromQuery] string range = "last30days",
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AnalyticsView,
            "ANALYTICS",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        try
        {
            var result = await _insightsService.GetWorkshopMatrixAsync(range, limit, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("payments")]
    public async Task<ActionResult<PaymentOutcomesDto>> GetPaymentOutcomes(
        [FromQuery] string range = "last30days",
        [FromQuery] Guid? workshopId = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AnalyticsView,
            "ANALYTICS",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        try
        {
            var result = await _insightsService.GetPaymentOutcomesAsync(range, workshopId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("live")]
    public async Task<ActionResult<BusinessLiveUsersResponse>> GetLiveUsers(
        [FromQuery] int windowMinutes = 15,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AnalyticsView,
            "ANALYTICS",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _insightsService.GetLiveUsersAsync(windowMinutes, cancellationToken);
        return Ok(result);
    }

    [HttpGet("activity")]
    public async Task<ActionResult<BusinessActivityFeedResponse>> GetActivityFeed(
        [FromQuery] int limit = 20,
        [FromQuery] Guid? workshopId = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AnalyticsView,
            "ANALYTICS",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        var result = await _insightsService.GetActivityFeedAsync(limit, workshopId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("traffic-sources")]
    public async Task<ActionResult<TrafficSourcesResponse>> GetTrafficSources(
        [FromQuery] string range = "last30days",
        [FromQuery] Guid? workshopId = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AnalyticsView,
            "ANALYTICS",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        try
        {
            var result = await _insightsService.GetTrafficSourcesAsync(range, workshopId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("devices")]
    public async Task<ActionResult<DeviceBreakdownResponse>> GetDeviceBreakdown(
        [FromQuery] string range = "last30days",
        [FromQuery] Guid? workshopId = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AnalyticsView,
            "ANALYTICS",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        try
        {
            var result = await _insightsService.GetDeviceBreakdownAsync(range, workshopId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("locations")]
    public async Task<ActionResult<TopLocationsResponse>> GetTopLocations(
        [FromQuery] string range = "last30days",
        [FromQuery] Guid? workshopId = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.AnalyticsView,
            "ANALYTICS",
            null,
            HttpContext,
            cancellationToken);

        if (!auth.Success)
        {
            return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });
        }

        try
        {
            var result = await _insightsService.GetTopLocationsAsync(range, workshopId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
