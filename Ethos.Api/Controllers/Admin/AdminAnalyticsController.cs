using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Analytics;
using Ethos.Api.Contracts.Analytics;
using Ethos.Api.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/analytics")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Analytics")]
public class AdminAnalyticsController : ControllerBase
{
    private readonly IAnalyticsQueryService _queryService;
    private readonly IAdminAuthorizationService _authService;

    public AdminAnalyticsController(
        IAnalyticsQueryService queryService,
        IAdminAuthorizationService authService)
    {
        _queryService = queryService;
        _authService = authService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<AnalyticsSummaryResponse>> GetSummary(
        [FromQuery] string range = "last7days",
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
            var result = await _queryService.GetSummaryAsync(range, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("trends")]
    public async Task<ActionResult<AnalyticsTrendsResponse>> GetTrends(
        [FromQuery] string range = "last30days",
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
            var result = await _queryService.GetTrendsAsync(range, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("events")]
    public async Task<ActionResult<AnalyticsEventBreakdownResponse>> GetEventBreakdown(
        [FromQuery] string range = "last30days",
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
            var result = await _queryService.GetEventBreakdownAsync(range, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("workshops")]
    public async Task<ActionResult<AnalyticsWorkshopResponse>> GetWorkshopAnalytics(
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
            var result = await _queryService.GetWorkshopAnalyticsAsync(range, limit, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("recent")]
    public async Task<ActionResult<AnalyticsRecentEventsResponse>> GetRecentEvents(
        [FromQuery] int limit = 20,
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

        var result = await _queryService.GetRecentEventsAsync(limit, cancellationToken);
        return Ok(result);
    }
}
