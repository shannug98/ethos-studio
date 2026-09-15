using Ethos.Api.Application.Feedback;
using Ethos.Api.Contracts.Feedback;
using Ethos.Api.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/students/me/feedback")]
[Authorize(Roles = "STUDENT")]
public class StudentFeedbackController : ControllerBase
{
    private readonly IStudentFeedbackService _feedbackService;

    public StudentFeedbackController(IStudentFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<LearningActivitySummaryResponse>> GetLearningActivitySummary(
        CancellationToken cancellationToken)
    {
        var response = await _feedbackService.GetLearningActivitySummaryAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StudentFeedbackResponse>>> GetMyFeedback(
        CancellationToken cancellationToken)
    {
        var response = await _feedbackService.GetMyFeedbackAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<PendingFeedbackItemResponse>>> GetPendingFeedback(
        CancellationToken cancellationToken)
    {
        var response = await _feedbackService.GetPendingFeedbackAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPost("classes/{classId:guid}")]
    public async Task<ActionResult<StudentFeedbackResponse>> SubmitClassFeedback(
        Guid classId,
        [FromBody] SubmitClassFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _feedbackService.SubmitClassFeedbackAsync(classId, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (BusinessRuleException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { code = "RESOURCE_NOT_FOUND", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already submitted", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { code = "FEEDBACK_ALREADY_SUBMITTED", message = ex.Message });
            }
            return BadRequest(new { code = "OPERATION_FAILED", message = ex.Message });
        }
    }

    [HttpPost("workshops/{workshopId:guid}")]
    public async Task<ActionResult<StudentFeedbackResponse>> SubmitWorkshopFeedback(
        Guid workshopId,
        [FromBody] SubmitWorkshopFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _feedbackService.SubmitWorkshopFeedbackAsync(workshopId, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (BusinessRuleException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { code = "RESOURCE_NOT_FOUND", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already submitted", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { code = "FEEDBACK_ALREADY_SUBMITTED", message = ex.Message });
            }
            return BadRequest(new { code = "OPERATION_FAILED", message = ex.Message });
        }
    }
}
