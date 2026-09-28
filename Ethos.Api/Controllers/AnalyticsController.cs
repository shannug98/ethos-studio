using Ethos.Api.Application.Analytics;
using Ethos.Api.Contracts.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[AllowAnonymous]
[Tags("Product Analytics Ingestion")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsEventService _analyticsService;

    public AnalyticsController(IAnalyticsEventService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpPost("events")]
    public async Task<ActionResult<RecordAnalyticsEventResponse>> RecordEvent(
        [FromBody] RecordAnalyticsEventRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _analyticsService.RecordEventAsync(request, HttpContext, cancellationToken);
        return Ok(result);
    }
}
