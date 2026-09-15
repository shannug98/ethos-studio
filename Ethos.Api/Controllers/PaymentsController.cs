using Ethos.Api.Application.Payments;
using Ethos.Api.Contracts.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("orders")]
    public async Task<ActionResult<PaymentTransactionResponse>> CreateOrder([FromBody] CreatePaymentOrderRequest request)
    {
        try
        {
            var response = await _paymentService.CreateOrderAsync(request);
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

    [HttpPost("verify")]
    public async Task<ActionResult<PaymentTransactionResponse>> VerifyPayment([FromBody] VerifyPaymentRequest request)
    {
        try
        {
            var response = await _paymentService.VerifyPaymentAsync(request);
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

    [AllowAnonymous]
    [HttpPost("public/package-order")]
    public async Task<IActionResult> CreatePublicPackageOrder(
        [FromBody] CreatePublicPackageOrderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response =
                await _paymentService.CreatePublicPackageOrderAsync(
                    request,
                    cancellationToken);

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

    [AllowAnonymous]
    [HttpPost("public/verify-package-payment")]
    public async Task<IActionResult> VerifyPublicPackagePayment(
        [FromBody] VerifyPublicPackagePaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response =
                await _paymentService.VerifyPublicPackagePaymentAsync(
                    request,
                    cancellationToken);

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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentTransactionResponse>> GetPaymentById(Guid id)
    {
        var response = await _paymentService.GetPaymentByIdAsync(id);

        if (response == null)
        {
            return NotFound(new { message = "Payment transaction not found." });
        }

        return Ok(response);
    }
}
