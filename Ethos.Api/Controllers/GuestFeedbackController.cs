using Ethos.Api.Application.Feedback;
using Ethos.Api.Contracts.Feedback;
using Ethos.Api.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/feedback/workshop")]
[Tags("Public - Workshop Feedback")]
public class GuestFeedbackController : ControllerBase
{
    private readonly IGuestWorkshopFeedbackService _feedbackService;

    public GuestFeedbackController(IGuestWorkshopFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    [HttpGet("token/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<GuestWorkshopFeedbackDetailsResponse>> GetFeedbackDetails(
        string token,
        CancellationToken cancellationToken)
    {
        var details = await _feedbackService.GetFeedbackDetailsByTokenAsync(token, cancellationToken);
        return Ok(details);
    }

    [HttpPost("submit")]
    [AllowAnonymous]
    public async Task<ActionResult> SubmitFeedback(
        [FromBody] SubmitGuestWorkshopFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var success = await _feedbackService.SubmitGuestFeedbackAsync(request, cancellationToken);
            return Ok(new { success, message = "Thank you for your feedback! Your review has been recorded." });
        }
        catch (BusinessRuleException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "VALIDATION_FAILED", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "OPERATION_FAILED", message = ex.Message });
        }
    }
}
