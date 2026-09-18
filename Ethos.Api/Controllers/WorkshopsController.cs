using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/workshops")]
public class WorkshopsController : ControllerBase
{
    private readonly IWorkshopService _workshopService;

    public WorkshopsController(IWorkshopService workshopService)
    {
        _workshopService = workshopService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkshopResponse>>> GetApprovedWorkshops()
    {
        var response = await _workshopService.GetApprovedWorkshopsAsync();
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkshopResponse>> GetWorkshopById(Guid id)
    {
        var response = await _workshopService.GetWorkshopByIdAsync(id);

        if (response == null)
        {
            return NotFound(new { message = "Workshop not found." });
        }

        return Ok(response);
    }

    [HttpGet("{id:guid}/pricing")]
    public async Task<ActionResult<WorkshopPricingResponse>> GetWorkshopPricing(Guid id)
    {
        var response = await _workshopService.GetWorkshopPricingAsync(id);

        if (response == null)
        {
            return NotFound(new { message = "Workshop not found." });
        }

        return Ok(response);
    }

    [HttpGet("{id:guid}/quote")]
    public async Task<ActionResult<WorkshopPriceQuoteResponse>> GetWorkshopQuote(
        Guid id,
        [FromQuery] int quantity = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _workshopService.GetWorkshopQuoteAsync(id, quantity, cancellationToken);
            return Ok(response);
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

    [AllowAnonymous]
    [HttpPost("{id:guid}/order")]
    public async Task<ActionResult<CreateWorkshopOrderResponse>> CreateWorkshopOrder(
        Guid id,
        [FromBody] CreateWorkshopOrderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _workshopService.CreateWorkshopOrderAsync(id, request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already have a confirmed booking", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("full", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { message = ex.Message });
            }

            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            var raw = ex.InnerException?.Message ?? ex.Message;
            if (raw.Contains("IX_workshop_bookings_WorkshopId_StudentProfileId", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("23505", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { message = "You already have a booking or pending checkout session for this workshop." });
            }

            return BadRequest(new { message = "Order creation failed due to a database constraint. Please try again." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("{id:guid}/verify-payment")]
    public async Task<ActionResult<WorkshopBookingResponse>> VerifyWorkshopPayment(
        Guid id,
        [FromBody] VerifyWorkshopPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _workshopService.VerifyWorkshopPaymentAsync(id, request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = ex.Message });
            }
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("bookings/{bookingId:guid}/tickets")]
    [HttpGet("/api/students/bookings/{bookingId:guid}/tickets")]
    public async Task<ActionResult<IReadOnlyList<WorkshopTicketResponse>>> GetBookingTickets(
        Guid bookingId,
        [FromServices] IWorkshopTicketService ticketService,
        CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var uid)) return Unauthorized();

        try
        {
            var result = await ticketService.GetTicketsForBookingAsync(bookingId, uid, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("bookings/{bookingId:guid}/tickets/{ticketId:guid}/attendee")]
    [HttpPut("/api/students/bookings/{bookingId:guid}/tickets/{ticketId:guid}/attendee")]
    public async Task<ActionResult<WorkshopTicketResponse>> UpdateTicketAttendee(
        Guid bookingId,
        Guid ticketId,
        [FromBody] UpdateAttendeeDetailsRequest request,
        [FromServices] IWorkshopTicketService ticketService,
        CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var uid)) return Unauthorized();

        try
        {
            var result = await ticketService.UpdateAttendeeDetailsAsync(bookingId, ticketId, uid, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("bookings/{bookingId:guid}/tickets/{ticketId:guid}/resend")]
    [HttpPost("/api/students/bookings/{bookingId:guid}/tickets/{ticketId:guid}/resend")]
    public async Task<IActionResult> ResendTicketPass(
        Guid bookingId,
        Guid ticketId,
        [FromServices] IWorkshopTicketService ticketService,
        CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var uid)) return Unauthorized();

        try
        {
            await ticketService.ResendTicketPassAsync(bookingId, ticketId, uid, cancellationToken);
            return Ok(new { message = "Pass resent successfully." });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("tickets/{ticketId:guid}/pass")]
    [HttpGet("/api/students/tickets/{ticketId:guid}/pass")]
    public async Task<ActionResult<WorkshopTicketResponse>> GetTicketPass(
        Guid ticketId,
        [FromServices] IWorkshopTicketService ticketService,
        CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var uid)) return Unauthorized();

        try
        {
            var result = await ticketService.GetTicketPassAsync(ticketId, uid, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpGet("tickets/{ticketId:guid}/pdf")]
    [HttpGet("/api/students/tickets/{ticketId:guid}/pdf")]
    public async Task<IActionResult> GetTicketPdf(
        Guid ticketId,
        [FromQuery] string? token,
        [FromServices] AppDbContext dbContext,
        [FromServices] IWorkshopTicketService ticketService,
        CancellationToken cancellationToken)
    {
        var ticket = await dbContext.WorkshopTickets
            .Include(t => t.WorkshopBooking)
                .ThenInclude(b => b!.Workshop)
            .Include(t => t.WorkshopBooking)
                .ThenInclude(b => b!.StudentProfile)
            .FirstOrDefaultAsync(t => t.Id == ticketId || t.WorkshopBookingId == ticketId, cancellationToken);

        WorkshopBooking? booking = null;
        Workshop? workshop = null;
        string attendeeName;
        string ticketNumber;
        string qrToken;
        Guid effectiveTicketId;

        if (ticket != null && ticket.WorkshopBooking != null && ticket.WorkshopBooking.Workshop != null)
        {
            booking = ticket.WorkshopBooking;
            workshop = ticket.WorkshopBooking.Workshop;
            attendeeName = ticket.AttendeeName;
            ticketNumber = ticket.TicketNumber;
            qrToken = ticketService.DeriveQrToken(ticket);
            effectiveTicketId = ticket.Id;
        }
        else
        {
            booking = await dbContext.WorkshopBookings
                .Include(b => b.Workshop)
                .Include(b => b.StudentProfile)
                .FirstOrDefaultAsync(b => b.Id == ticketId, cancellationToken);

            if (booking == null || booking.Workshop == null)
            {
                return NotFound(new { message = "Ticket pass not found." });
            }

            workshop = booking.Workshop;
            attendeeName = booking.GuestName ?? "Ethos Student";
            ticketNumber = "ETH-WS-" + booking.Id.ToString()[..8].ToUpperInvariant() + "-01";
            qrToken = "ETHOS-TKT-" + booking.Id;
            effectiveTicketId = booking.Id;
        }

        if (booking == null || workshop == null)
        {
            return NotFound(new { message = "Ticket pass not found." });
        }

        // Authoritative Authorization Validation
        bool isAuthorized = false;

        // 1. Check authenticated user claims (owner or admin)
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Admin"))
            {
                isAuthorized = true;
            }
            else
            {
                var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdStr, out var currentUserId))
                {
                    if (booking?.StudentProfile?.UserId == currentUserId)
                    {
                        isAuthorized = true;
                    }
                }
            }
        }

        // 2. Controlled guest / external link access via separate scoped PDF download token
        if (!isAuthorized && !string.IsNullOrWhiteSpace(token))
        {
            if (ticketService.ValidatePdfDownloadToken(effectiveTicketId, token) ||
                (ticket != null && ticketService.ValidatePdfDownloadToken(ticket.Id, token)) ||
                (booking != null && ticketService.ValidatePdfDownloadToken(booking.Id, token)))
            {
                isAuthorized = true;
            }
        }

        if (!isAuthorized)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Unauthorized access to ticket pass. Valid authentication or active PDF download token required."
            });
        }

        var bookingRef = "BK-" + booking!.Id.ToString()[..8].ToUpperInvariant();

        var startTimeStr = DateTime.Today.Add(workshop.StartTime).ToString("h:mm tt");
        var endTimeStr = DateTime.Today.Add(workshop.EndTime).ToString("h:mm tt");
        var timeDisplay = $"{startTimeStr} - {endTimeStr}";
        var dateDisplay = workshop.WorkshopDate.ToString("dd MMMM yyyy");

        var model = new TicketPdfModel(
            WorkshopTitle: workshop.Title,
            WorkshopDate: dateDisplay,
            WorkshopTime: timeDisplay,
            Venue: workshop.Venue ?? "Ethos Dance Studio",
            AttendeeName: attendeeName,
            BookingReference: bookingRef,
            TicketNumber: ticketNumber,
            QrToken: qrToken);

        var pdfBytes = TicketPdfGenerator.Generate(model);
        return File(pdfBytes, "application/pdf", $"{ticketNumber}.pdf");
    }
}
