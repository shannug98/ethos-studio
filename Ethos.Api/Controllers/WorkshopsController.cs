using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Workshops;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
}
