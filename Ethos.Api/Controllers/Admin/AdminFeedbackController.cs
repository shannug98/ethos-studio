using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Feedback")]
public class AdminFeedbackController : ControllerBase
{
    private readonly IAdminFeedbackService _feedbackService;
    private readonly IAdminAuthorizationService _authService;

    public AdminFeedbackController(IAdminFeedbackService feedbackService, IAdminAuthorizationService authService)
    {
        _feedbackService = feedbackService;
        _authService = authService;
    }

    [HttpGet("feedback")]
    public async Task<ActionResult<PagedResult<AdminFeedbackResponse>>> GetFeedback(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? trainerId = null,
        [FromQuery] Guid? studentId = null,
        [FromQuery] Guid? workshopId = null,
        [FromQuery] int? minRating = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "Feedback", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _feedbackService.GetFeedbackAsync(page, pageSize, trainerId, studentId, workshopId, minRating, startDate, endDate, cancellationToken);
        return Ok(result);
    }

    [HttpGet("feedback/{feedbackId:guid}")]
    public async Task<ActionResult<AdminFeedbackResponse>> GetFeedbackById(
        Guid feedbackId,
        CancellationToken cancellationToken)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "Feedback", feedbackId, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _feedbackService.GetFeedbackByIdAsync(feedbackId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }
}
