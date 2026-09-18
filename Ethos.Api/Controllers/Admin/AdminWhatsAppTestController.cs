using Ethos.Api.Application.Common;
using Ethos.Api.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ethos.Api.Controllers.Admin;

public record TestBookingConfirmedRequest(
    string RecipientPhone,
    string? AttendeeName,
    string? WorkshopTitle,
    string? WorkshopDate,
    string? WorkshopTime,
    string? BookingId,
    bool ExecuteLiveSend = false
);

public record TestTicketPdfRequest(
    string RecipientPhone,
    string? AttendeeName,
    string? WorkshopTitle,
    string? WorkshopDate,
    string? WorkshopTime,
    string? BookingId,
    string? PdfHttpsUrl,
    string? FileName,
    bool ExecuteLiveSend = false
);

public record NormalizePhoneRequest(string Phone);

[ApiController]
[Route("api/admin/whatsapp-test")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - MSG91 WhatsApp Testing")]
public class AdminWhatsAppTestController : ControllerBase
{
    private readonly IMsg91WhatsAppService _msg91Service;
    private readonly Msg91Options _options;
    private readonly ILogger<AdminWhatsAppTestController> _logger;

    public AdminWhatsAppTestController(
        IMsg91WhatsAppService msg91Service,
        IOptions<Msg91Options> options,
        ILogger<AdminWhatsAppTestController> logger)
    {
        _msg91Service = msg91Service;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("normalize-phone")]
    public IActionResult NormalizePhone([FromBody] NormalizePhoneRequest request)
    {
        EnsureTestEndpointsEnabled();

        var isValid = _msg91Service.TryNormalizePhoneNumber(
            request.Phone,
            out var normalized,
            out var error,
            _options.DefaultCountryCode);

        return Ok(new
        {
            input = request.Phone,
            isValid,
            normalized = isValid ? normalized : null,
            error
        });
    }

    [HttpPost("booking-confirmed")]
    public async Task<IActionResult> TestBookingConfirmed(
        [FromBody] TestBookingConfirmedRequest request,
        CancellationToken cancellationToken)
    {
        EnsureTestEndpointsEnabled();

        var data = new BookingConfirmedData(
            AttendeeName: !string.IsNullOrWhiteSpace(request.AttendeeName) ? request.AttendeeName : "Rahul Sharma",
            WorkshopTitle: !string.IsNullOrWhiteSpace(request.WorkshopTitle) ? request.WorkshopTitle : "Bollywood Workshop",
            WorkshopDate: !string.IsNullOrWhiteSpace(request.WorkshopDate) ? request.WorkshopDate : "25 October 2026",
            WorkshopTime: !string.IsNullOrWhiteSpace(request.WorkshopTime) ? request.WorkshopTime : "6:00 PM – 8:00 PM",
            BookingId: !string.IsNullOrWhiteSpace(request.BookingId) ? request.BookingId : "ETHOS-WKS-8F31A2C4"
        );

        if (!_msg91Service.TryNormalizePhoneNumber(request.RecipientPhone, out var normalizedPhone, out var phoneError, _options.DefaultCountryCode))
        {
            return BadRequest(new { message = "Recipient phone is invalid.", details = phoneError });
        }

        var jsonPayload = _msg91Service.BuildBookingConfirmedJson(data, normalizedPhone);

        // Dry-run by default
        if (!request.ExecuteLiveSend)
        {
            return Ok(new
            {
                mode = "DRY_RUN",
                notice = "Message was NOT sent. Pass 'executeLiveSend: true' to dispatch live.",
                template = _options.BookingConfirmedTemplateName,
                namespaceId = _options.BookingConfirmedNamespace,
                recipient = MaskPhone(normalizedPhone),
                sanitizedPayloadJson = jsonPayload
            });
        }

        // Live send security guards
        EnsureLiveSendPermitted(normalizedPhone);

        var result = await _msg91Service.SendBookingConfirmedAsync(data, normalizedPhone, cancellationToken);
        return Ok(SanitizeResult(result));
    }

    [HttpPost("ticket-pdf")]
    public async Task<IActionResult> TestTicketPdf(
        [FromBody] TestTicketPdfRequest request,
        CancellationToken cancellationToken)
    {
        EnsureTestEndpointsEnabled();

        var testPdfUrl = !string.IsNullOrWhiteSpace(request.PdfHttpsUrl)
            ? request.PdfHttpsUrl
            : "https://media.ethosdancestudio.com/tickets/sample-test-ticket.pdf";

        var data = new TicketPdfData(
            AttendeeName: !string.IsNullOrWhiteSpace(request.AttendeeName) ? request.AttendeeName : "Rahul Sharma",
            WorkshopTitle: !string.IsNullOrWhiteSpace(request.WorkshopTitle) ? request.WorkshopTitle : "Bollywood Workshop",
            WorkshopDate: !string.IsNullOrWhiteSpace(request.WorkshopDate) ? request.WorkshopDate : "25 October 2026",
            WorkshopTime: !string.IsNullOrWhiteSpace(request.WorkshopTime) ? request.WorkshopTime : "6:00 PM – 8:00 PM",
            BookingId: !string.IsNullOrWhiteSpace(request.BookingId) ? request.BookingId : "ETHOS-WKS-8F31A2C4",
            PdfHttpsUrl: testPdfUrl,
            FileName: !string.IsNullOrWhiteSpace(request.FileName) ? request.FileName : "ETHOS-TKT-001.pdf"
        );

        if (!_msg91Service.TryNormalizePhoneNumber(request.RecipientPhone, out var normalizedPhone, out var phoneError, _options.DefaultCountryCode))
        {
            return BadRequest(new { message = "Recipient phone is invalid.", details = phoneError });
        }

        var jsonPayload = _msg91Service.BuildTicketPdfJson(data, normalizedPhone);

        // Dry-run by default
        if (!request.ExecuteLiveSend)
        {
            return Ok(new
            {
                mode = "DRY_RUN",
                notice = "Message was NOT sent. Pass 'executeLiveSend: true' to dispatch live.",
                template = _options.TicketPdfTemplateName,
                namespaceId = (string?)null,
                recipient = MaskPhone(normalizedPhone),
                documentUrl = testPdfUrl,
                sanitizedPayloadJson = jsonPayload
            });
        }

        // Live send security guards
        EnsureLiveSendPermitted(normalizedPhone);

        var result = await _msg91Service.SendTicketPdfAsync(data, normalizedPhone, cancellationToken);
        return Ok(SanitizeResult(result));
    }

    private void EnsureTestEndpointsEnabled()
    {
        if (!_options.AllowTestEndpoints)
        {
            throw new InvalidOperationException("MSG91 test endpoints are disabled by server configuration (Msg91:AllowTestEndpoints is false).");
        }
    }

    private void EnsureLiveSendPermitted(string normalizedPhone)
    {
        if (_options.ApprovedTestNumbers.Count > 0 && !_options.ApprovedTestNumbers.Contains(normalizedPhone))
        {
            throw new InvalidOperationException($"Live dispatch to {MaskPhone(normalizedPhone)} is restricted. Number is not in Msg91:ApprovedTestNumbers.");
        }
    }

    private static object SanitizeResult(Msg91DispatchResult result)
    {
        return new
        {
            success = result.Success,
            statusCode = result.StatusCode,
            providerMessageId = result.ProviderMessageId,
            providerRequestId = result.ProviderRequestId,
            isTimeout = result.IsTimeout,
            isTransientError = result.IsTransientError,
            isPermanentError = result.IsPermanentError,
            skipped = result.Skipped,
            skipReason = result.SkipReason,
            errorMessage = result.ErrorMessage,
            summary = result.SanitizedPayloadSummary
        };
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "[EMPTY]";
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4) return "******";
        return $"{new string('*', digits.Length - 4)}{digits[^4..]}";
    }
}
