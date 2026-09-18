using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/bookings")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Bookings")]
public class AdminBookingsController : ControllerBase
{
    private readonly IAdminBookingService _bookingService;
    private readonly IAdminAuthorizationService _authService;

    public AdminBookingsController(
        IAdminBookingService bookingService,
        IAdminAuthorizationService authService)
    {
        _bookingService = bookingService;
        _authService = authService;
    }

    private Guid AdminUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("classes")]
    public async Task<ActionResult<PagedResult<AdminClassEnrollmentResponse>>> GetClassEnrollments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? classId = null,
        [FromQuery] Guid? studentId = null,
        [FromQuery] int? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.BookingView, "ClassEnrollment", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _bookingService.GetClassEnrollmentsAsync(page, pageSize, classId, studentId, status, search, cancellationToken);
        return Ok(result);
    }

    [HttpGet("workshops")]
    public async Task<ActionResult<PagedResult<AdminWorkshopRegistrationResponse>>> GetWorkshopBookings(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? workshopId = null,
        [FromQuery] Guid? studentId = null,
        [FromQuery] WorkshopBookingStatus? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.BookingView, "WorkshopBooking", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _bookingService.GetWorkshopBookingsAsync(page, pageSize, workshopId, studentId, status, search, cancellationToken);
        return Ok(result);
    }

    [HttpPost("classes/{enrollmentId:guid}/cancel")]
    public async Task<IActionResult> CancelClassEnrollment(
        Guid enrollmentId,
        [FromBody] AdminCancelBookingRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.BookingCorrect, "ClassEnrollment", enrollmentId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        if (string.IsNullOrWhiteSpace(request?.Reason))
            return BadRequest(new { message = "Cancellation reason is required." });

        try
        {
            await _bookingService.CancelClassEnrollmentAsync(enrollmentId, AdminUserId, request.Reason, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("workshops/{bookingId:guid}/cancel")]
    public async Task<IActionResult> CancelWorkshopBooking(
        Guid bookingId,
        [FromBody] AdminCancelBookingRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.BookingCorrect, "WorkshopBooking", bookingId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        if (string.IsNullOrWhiteSpace(request?.Reason))
            return BadRequest(new { message = "Cancellation reason is required." });

        try
        {
            await _bookingService.CancelWorkshopBookingAsync(bookingId, AdminUserId, request.Reason, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("classes/manual")]
    public async Task<ActionResult<AdminClassEnrollmentResponse>> ManualEnrollment(
        [FromBody] AdminManualEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.BookingCorrect, "ClassEnrollment", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            var result = await _bookingService.ManualEnrollmentAsync(AdminUserId, request, cancellationToken);
            return Ok(result);
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

    [HttpPut("workshops/{bookingId:guid}/contact")]
    public async Task<IActionResult> UpdateWorkshopBookingContact(
        Guid bookingId,
        [FromBody] AdminUpdateBookingContactRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.BookingCorrect, "WorkshopBooking", bookingId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _bookingService.UpdateWorkshopBookingContactAsync(bookingId, AdminUserId, request.Phone, request.Email, request.FullName, cancellationToken);
            return Ok(new { message = "Contact details updated successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("workshops/{bookingId:guid}/resend-whatsapp")]
    public async Task<IActionResult> ResendWhatsAppTicket(
        Guid bookingId,
        [FromBody] AdminResendWhatsAppRequest? request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.BookingCorrect, "WorkshopBooking", bookingId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            var recipientPhone = await _bookingService.ResendWhatsAppTicketAsync(bookingId, AdminUserId, request?.Phone, cancellationToken);
            return Ok(new { message = $"Ticket PDF queued and resent to {recipientPhone} via WhatsApp outbox." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}