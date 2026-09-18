using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Workshops")]
public class AdminWorkshopsController : ControllerBase
{
    private readonly IAdminWorkshopService _workshopService;
    private readonly IAdminAuthorizationService _authService;
    private readonly Ethos.Api.Application.Feedback.IGuestWorkshopFeedbackService _guestFeedbackService;

    public AdminWorkshopsController(
        IAdminWorkshopService workshopService,
        IAdminAuthorizationService authService,
        Ethos.Api.Application.Feedback.IGuestWorkshopFeedbackService guestFeedbackService)
    {
        _workshopService = workshopService;
        _authService = authService;
        _guestFeedbackService = guestFeedbackService;
    }

    private Guid AdminUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("workshops")]
    public async Task<ActionResult<PagedResult<TrainerWorkshopResponse>>> GetWorkshops(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? phase = null,
        [FromQuery] WorkshopStatus? status = null,
        [FromQuery] Guid? trainerId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? search = null,
        [FromQuery] string? city = null,
        CancellationToken cancellationToken = default)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopView, "Workshop", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _workshopService.GetWorkshopsAsync(page, pageSize, phase, status, trainerId, startDate, endDate, search, city, cancellationToken);
        return Ok(result);
    }

    [HttpGet("workshops/counts")]
    public async Task<ActionResult<AdminWorkshopCountsDto>> GetWorkshopCounts(CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopView, "Workshop", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _workshopService.GetWorkshopCountsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("workshops")]
    public async Task<ActionResult<TrainerWorkshopResponse>> CreateWorkshop(
        [FromBody] AdminCreateWorkshopRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopCreate, "Workshop", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            var result = await _workshopService.CreateWorkshopAsync(AdminUserId, request, cancellationToken);
            return CreatedAtAction(nameof(GetWorkshopById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(new { message = msg });
        }
    }

    [HttpPut("workshops/{id:guid}")]
    public async Task<ActionResult<TrainerWorkshopResponse>> UpdateWorkshop(
        Guid id,
        [FromBody] AdminUpdateWorkshopRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopUpdate, "Workshop", id, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            var result = await _workshopService.UpdateWorkshopAsync(id, AdminUserId, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "WORKSHOP_INVALID_OPERATION", message = ex.Message });
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(new { message = msg });
        }
    }

    [HttpGet("workshops/pending")]
    public async Task<ActionResult<IReadOnlyList<TrainerWorkshopResponse>>> GetPendingWorkshops(CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopView, "Workshop", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _workshopService.GetPendingWorkshopsAsync(cancellationToken);
        return Ok(result);
    }

    
    [HttpGet("workshops/{id:guid}/pricing-tiers")]
    public async Task<ActionResult<AdminWorkshopPricingTiersResponse>> GetWorkshopPricingTiers(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopView, "Workshop", id, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            var result = await _workshopService.GetWorkshopPricingTiersAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("workshops/{id:guid}/pricing-tiers")]
    public async Task<IActionResult> UpdateWorkshopPricingTiers(
        Guid id,
        [FromBody] AdminUpdateWorkshopPricingTiersRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopUpdate, "Workshop", id, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _workshopService.UpdateWorkshopPricingTiersAsync(id, AdminUserId, request, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("workshops/{id:guid}")]
    public async Task<ActionResult<TrainerWorkshopResponse>> GetWorkshopById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopView, "Workshop", id, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _workshopService.GetWorkshopByIdAsync(id, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("workshops/{id:guid}/approve-price")]
    public async Task<IActionResult> ApproveWorkshopPrice(
        Guid id,
        [FromBody] AdminApproveWorkshopPriceRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopApprove, "Workshop", id, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _workshopService.ApproveWorkshopPriceAsync(id, AdminUserId, request, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("workshops/{id:guid}/approve")]
    public async Task<IActionResult> ApproveWorkshop(
        Guid id,
        [FromBody] AdminApproveWorkshopPriceRequest? request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopApprove, "Workshop", id, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _workshopService.ApproveWorkshopPriceAsync(id, AdminUserId, request ?? new AdminApproveWorkshopPriceRequest(), cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("workshops/{id:guid}/publish")]
    public async Task<IActionResult> PublishWorkshop(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopApprove, "Workshop", id, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _workshopService.PublishWorkshopAsync(id, AdminUserId, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("workshops/{id:guid}/unpublish")]
    public async Task<IActionResult> UnpublishWorkshop(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopApprove, "Workshop", id, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _workshopService.UnpublishWorkshopAsync(id, AdminUserId, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("workshops/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveWorkshop(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopCancel, "Workshop", id, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _workshopService.ArchiveWorkshopAsync(id, AdminUserId, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("workshops/{id:guid}/reject")]
    public async Task<IActionResult> RejectWorkshop(
        Guid id,
        [FromBody] AdminRejectWorkshopBody body,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopCancel, "Workshop", id, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        if (string.IsNullOrWhiteSpace(body?.Reason))
            return BadRequest(new { message = "Rejection reason is required." });

        try
        {
            await _workshopService.RejectWorkshopAsync(id, AdminUserId, body.Reason, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("workshops/{id:guid}/cancel")]
    public async Task<IActionResult> CancelWorkshop(
        Guid id,
        [FromBody] AdminCancelWorkshopBody body,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopCancel, "Workshop", id, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        if (string.IsNullOrWhiteSpace(body?.Reason))
            return BadRequest(new { message = "Cancellation reason is required." });

        try
        {
            await _workshopService.CancelWorkshopAsync(id, AdminUserId, body.Reason, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("workshops/{id:guid}/complete")]
    public async Task<IActionResult> CompleteWorkshop(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopUpdate, "Workshop", id, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _workshopService.CompleteWorkshopAsync(id, AdminUserId, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("workshops/{workshopId:guid}/registrations")]
    public async Task<ActionResult<PagedResult<AdminWorkshopRegistrationResponse>>> GetWorkshopRegistrations(
        Guid workshopId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopView, "Workshop", workshopId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _workshopService.GetWorkshopRegistrationsAsync(workshopId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpPost("workshops/bookings/{bookingId:guid}/feedback-token")]
    public async Task<ActionResult<Ethos.Api.Contracts.Feedback.GenerateFeedbackTokenResponse>> GenerateFeedbackToken(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopView, "Workshop", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            var result = await _guestFeedbackService.GenerateTokenForBookingAsync(bookingId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("workshops/{id:guid}/tickets")]
    public async Task<ActionResult<IReadOnlyList<AdminWorkshopTicketResponse>>> GetWorkshopTickets(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopView, "Workshop", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _workshopService.GetWorkshopTicketsAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("workshops/{id:guid}/tickets/{ticketId:guid}/override")]
    public async Task<IActionResult> OverrideTicketCheckIn(
        Guid id,
        Guid ticketId,
        [FromBody] AdminTicketOverrideRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopUpdate, "Workshop", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        if (string.IsNullOrWhiteSpace(request?.Reason))
        {
            return BadRequest(new { message = "A mandatory reason must be provided for manual check-in override." });
        }

        try
        {
            await _workshopService.AdminCheckInOverrideAsync(id, ticketId, AdminUserId, request.Reason, cancellationToken);
            return Ok(new { message = "Ticket manual check-in recorded successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("workshops/{id:guid}/tickets/{ticketId:guid}/undo-check-in")]
    public async Task<IActionResult> UndoTicketCheckIn(
        Guid id,
        Guid ticketId,
        [FromBody] AdminTicketOverrideRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopUpdate, "Workshop", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        if (string.IsNullOrWhiteSpace(request?.Reason))
        {
            return BadRequest(new { message = "A mandatory reason must be provided to reverse check-in." });
        }

        try
        {
            await _workshopService.AdminUndoCheckInAsync(id, ticketId, AdminUserId, request.Reason, cancellationToken);
            return Ok(new { message = "Ticket check-in successfully reversed." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("workshops/{id:guid}/attendance/export")]
    public async Task<IActionResult> ExportWorkshopAttendance(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopView, "Workshop", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var csv = await _workshopService.ExportWorkshopAttendanceCsvAsync(id, cancellationToken);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"workshop_attendance_{id}.csv");
    }

    [HttpGet("workshops/{workshopId:guid}/overview")]
    public async Task<ActionResult<AdminWorkshopOverviewResponse>> GetWorkshopOverview(
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopView, "Workshop", workshopId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            var result = await _workshopService.GetWorkshopOverviewAsync(workshopId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("workshops/{workshopId:guid}/tickets/check-in")]
    public async Task<ActionResult<AdminCheckInTicketResponse>> CheckInWorkshopTicket(
        Guid workshopId,
        [FromBody] AdminCheckInTicketRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopUpdate, "Workshop", workshopId, HttpContext, cancellationToken);
        if (!authCheck.Success)
        {
            return StatusCode(authCheck.StatusCode, new AdminCheckInTicketResponse
            {
                Success = false,
                Code = "UNAUTHORIZED",
                Message = authCheck.ErrorMessage ?? "You do not have permission to check in attendees."
            });
        }

        var result = await _workshopService.CheckInWorkshopTicketAsync(workshopId, AdminUserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("workshops/{workshopId:guid}/attendees")]
    public async Task<ActionResult<IReadOnlyList<AdminWorkshopAttendeeDto>>> GetWorkshopAttendees(
        Guid workshopId,
        [FromQuery] string? filter,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopView, "Workshop", workshopId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _workshopService.GetWorkshopAttendeesAsync(workshopId, filter, search, cancellationToken);
        return Ok(result);
    }

    [HttpGet("workshops/{workshopId:guid}/feedback")]
    public async Task<ActionResult<IReadOnlyList<AdminWorkshopFeedbackDto>>> GetWorkshopFeedback(
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.WorkshopView, "Workshop", workshopId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _workshopService.GetWorkshopFeedbackAsync(workshopId, cancellationToken);
        return Ok(result);
    }
}

public class AdminRejectWorkshopBody
{
    public string Reason { get; set; } = null!;
}

public class AdminCancelWorkshopBody
{
    public string Reason { get; set; } = null!;
}

public class AdminTicketOverrideRequest
{
    public string Reason { get; set; } = null!;
}
