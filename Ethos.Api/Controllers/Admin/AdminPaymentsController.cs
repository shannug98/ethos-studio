using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Payments")]
public class AdminPaymentsController : ControllerBase
{
    private readonly IAdminPaymentService _paymentService;
    private readonly IAdminAuthorizationService _authService;

    public AdminPaymentsController(
        IAdminPaymentService paymentService,
        IAdminAuthorizationService authService)
    {
        _paymentService = paymentService;
        _authService = authService;
    }

    private Guid AdminUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("payments")]
    public async Task<ActionResult<PagedResult<AdminPaymentTransactionResponse>>> GetPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] PaymentPurpose? purpose = null,
        [FromQuery] PaymentStatus? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.PaymentView, "PaymentTransaction", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _paymentService.GetPaymentsAsync(page, pageSize, startDate, endDate, userId, purpose, status, search, cancellationToken);
        return Ok(result);
    }

    [HttpGet("payments/{transactionId:guid}")]
    public async Task<ActionResult<AdminPaymentTransactionResponse>> GetPaymentById(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.PaymentView, "PaymentTransaction", transactionId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _paymentService.GetPaymentByIdAsync(transactionId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<AdminRevenueResponse>> GetRevenue(CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.PaymentView, "PaymentTransaction", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _paymentService.GetRevenueAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("payments/{transactionId:guid}/refund")]
    public async Task<ActionResult<AdminRefundResponse>> RecordRefund(
        Guid transactionId,
        [FromBody] AdminRecordRefundRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.PaymentReconcile, "PaymentTransaction", transactionId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            var result = await _paymentService.RecordExternalRefundAsync(transactionId, AdminUserId, request, idempotencyKey, cancellationToken);
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


    [HttpPost("payments/{transactionId:guid}/resolve")]
    public async Task<ActionResult<AdminResolvePaymentIssueResponse>> ResolvePaymentIssue(
        Guid transactionId,
        [FromBody] AdminResolvePaymentIssueRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.PaymentReconcile, "PaymentTransaction", transactionId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            var result = await _paymentService.ResolvePaymentIssueAsync(transactionId, AdminUserId, request, cancellationToken);
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

    [HttpGet("payments/{transactionId:guid}/timeline")]
    public async Task<ActionResult<List<AdminPaymentTimelineEvent>>> GetPaymentTimeline(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.PaymentView, "PaymentTransaction", transactionId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _paymentService.GetPaymentTimelineAsync(transactionId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("payments/{transactionId:guid}/receipt")]
    public async Task<ActionResult<AdminPaymentReceiptResponse>> GetPaymentReceipt(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.PaymentView, "PaymentTransaction", transactionId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _paymentService.GetPaymentReceiptAsync(transactionId, AdminUserId, cancellationToken);
        if (result == null) return NotFound(new { message = "Payment transaction not found." });
        return Ok(result);
    }

    [HttpGet("finance/trainer-payouts")]
    public async Task<ActionResult<AdminTrainerPayoutResponse>> GetTrainerPayouts(CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.PaymentView, "TrainerProfile", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _paymentService.GetTrainerPayoutsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("finance/trainer-payouts/{trainerId:guid}/process")]
    public async Task<IActionResult> ProcessTrainerPayout(
        Guid trainerId,
        [FromBody] AdminProcessTrainerPayoutRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.PaymentReconcile, "TrainerProfile", trainerId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _paymentService.ProcessTrainerPayoutAsync(trainerId, AdminUserId, request, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
