using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ethos.Api.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ethos.Api.Application.Notifications;

public class Msg91WhatsAppService : IMsg91WhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly Msg91Options _options;
    private readonly ILogger<Msg91WhatsAppService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    public Msg91WhatsAppService(
        HttpClient httpClient,
        IOptions<Msg91Options> options,
        ILogger<Msg91WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Msg91DispatchResult> SendBookingConfirmedAsync(
        BookingConfirmedData data,
        string recipientPhone,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!TryNormalizePhoneNumber(recipientPhone, out var normalizedPhone, out var phoneError, _options.DefaultCountryCode))
        {
            return Msg91DispatchResult.Permanent(
                400,
                $"Invalid recipient phone number: {phoneError}",
                SanitizeSummary("ethos_booking_confirmed", recipientPhone, data.BookingId));
        }

        var jsonPayload = BuildBookingConfirmedJson(data, normalizedPhone);
        var summary = SanitizeSummary("ethos_booking_confirmed", normalizedPhone, data.BookingId);

        if (!_options.Enabled || !_options.IsConfigured)
        {
            _logger.LogInformation(
                "[MSG91 WhatsApp] Outbound messaging is disabled or credentials unconfigured. Skipping booking confirmation dispatch for {Recipient} (Booking: {BookingId})",
                MaskPhone(normalizedPhone),
                data.BookingId);

            return Msg91DispatchResult.Skip("MSG91 service is disabled or credentials unconfigured.", summary);
        }

        return await PostToMsg91Async(jsonPayload, summary, cancellationToken);
    }

    public async Task<Msg91DispatchResult> SendTicketPdfAsync(
        TicketPdfData data,
        string recipientPhone,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!TryNormalizePhoneNumber(recipientPhone, out var normalizedPhone, out var phoneError, _options.DefaultCountryCode))
        {
            return Msg91DispatchResult.Permanent(
                400,
                $"Invalid recipient phone number: {phoneError}",
                SanitizeSummary("ethos_ticket_pdf", recipientPhone, data.BookingId));
        }

        // Strict HTTPS document URL check
        if (!IsSecureHttpsUrl(data.PdfHttpsUrl))
        {
            _logger.LogWarning(
                "[MSG91 WhatsApp] Rejecting ticket PDF dispatch for {Recipient}: Document URL is not a valid HTTPS URL: {Url}",
                MaskPhone(normalizedPhone),
                SanitizeUrl(data.PdfHttpsUrl));

            return Msg91DispatchResult.Permanent(
                400,
                "Ticket PDF document URL must be a valid public HTTPS URL. Local paths or HTTP are rejected.",
                SanitizeSummary("ethos_ticket_pdf", normalizedPhone, data.BookingId));
        }

        var jsonPayload = BuildTicketPdfJson(data, normalizedPhone);
        var summary = SanitizeSummary("ethos_ticket_pdf", normalizedPhone, data.BookingId);

        if (!_options.Enabled || !_options.IsConfigured)
        {
            _logger.LogInformation(
                "[MSG91 WhatsApp] Outbound messaging is disabled or credentials unconfigured. Skipping ticket PDF dispatch for {Recipient} (Booking: {BookingId})",
                MaskPhone(normalizedPhone),
                data.BookingId);

            return Msg91DispatchResult.Skip("MSG91 service is disabled or credentials unconfigured.", summary);
        }

        return await PostToMsg91Async(jsonPayload, summary, cancellationToken);
    }

    public bool TryNormalizePhoneNumber(
        string? rawPhone,
        out string normalizedPhone,
        out string? failureReason,
        string defaultCountryCode = "91")
    {
        normalizedPhone = string.Empty;
        failureReason = null;

        if (string.IsNullOrWhiteSpace(rawPhone))
        {
            failureReason = "Phone number is null, empty, or whitespace.";
            return false;
        }

        var trimmed = rawPhone.Trim();

        // 1. Strict character validation: Only allow digits, leading '+', and spacing/hyphen separators.
        // If letters or unexpected punctuation exist, reject rather than silently changing input.
        if (Regex.IsMatch(trimmed, @"[^\d\+\s\-]"))
        {
            failureReason = "Phone number contains invalid characters (letters or unexpected symbols).";
            return false;
        }

        // 2. Extract digits only
        var digitsOnly = Regex.Replace(trimmed, @"[^\d]", "");

        if (digitsOnly.Length < 10)
        {
            failureReason = $"Phone number has only {digitsOnly.Length} digits; minimum 10 digits required.";
            return false;
        }

        // 3. Indian Mobile Number Normalization Rules
        // Standard Indian Mobile: 10 digits starting with 6, 7, 8, or 9
        if (digitsOnly.Length == 10)
        {
            if (digitsOnly[0] is '6' or '7' or '8' or '9')
            {
                normalizedPhone = $"{defaultCountryCode}{digitsOnly}";
                return true;
            }

            failureReason = "10-digit Indian mobile number must start with 6, 7, 8, or 9.";
            return false;
        }

        // 11 digits starting with 0 (e.g. 09876543210)
        if (digitsOnly.Length == 11 && digitsOnly.StartsWith('0'))
        {
            var tenDigit = digitsOnly[1..];
            if (tenDigit[0] is '6' or '7' or '8' or '9')
            {
                normalizedPhone = $"{defaultCountryCode}{tenDigit}";
                return true;
            }

            failureReason = "0-prefixed Indian mobile number must have a valid 10-digit subscriber number starting with 6, 7, 8, or 9.";
            return false;
        }

        // 12 digits starting with country code 91
        if (digitsOnly.Length == 12 && digitsOnly.StartsWith("91"))
        {
            var nationalPart = digitsOnly[2..];
            if (nationalPart[0] is '6' or '7' or '8' or '9')
            {
                normalizedPhone = digitsOnly;
                return true;
            }

            failureReason = "91-prefixed Indian mobile number must have a valid subscriber number starting with 6, 7, 8, or 9.";
            return false;
        }

        // General E.164-compatible numbers between 11 and 15 digits
        if (digitsOnly.Length <= 15)
        {
            normalizedPhone = digitsOnly;
            return true;
        }

        failureReason = $"Phone number contains {digitsOnly.Length} digits, exceeding the 15-digit E.164 maximum.";
        return false;
    }

    public string BuildBookingConfirmedJson(BookingConfirmedData data, string recipientPhone)
    {
        var envelope = new Msg91OutboundEnvelope<BookingConfirmedComponents>
        {
            IntegratedNumber = _options.IntegratedNumber ?? string.Empty,
            ContentType = "template",
            Payload = new Msg91Payload<BookingConfirmedComponents>
            {
                MessagingProduct = "whatsapp",
                Type = "template",
                Template = new Msg91Template<BookingConfirmedComponents>
                {
                    Name = _options.BookingConfirmedTemplateName,
                    Language = new Msg91Language { Code = "en", Policy = "deterministic" },
                    Namespace = _options.BookingConfirmedNamespace,
                    ToAndComponents = new List<Msg91ToAndComponents<BookingConfirmedComponents>>
                    {
                        new()
                        {
                            To = new List<string> { recipientPhone },
                            Components = new BookingConfirmedComponents
                            {
                                Body1 = new TextComponent { Value = data.AttendeeName },
                                Body2 = new TextComponent { Value = data.WorkshopTitle },
                                Body3 = new TextComponent { Value = data.WorkshopDate },
                                Body4 = new TextComponent { Value = data.WorkshopTime },
                                Body5 = new TextComponent { Value = data.BookingId }
                            }
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    public string BuildTicketPdfJson(TicketPdfData data, string recipientPhone)
    {
        var envelope = new Msg91OutboundEnvelope<TicketPdfComponents>
        {
            IntegratedNumber = _options.IntegratedNumber ?? string.Empty,
            ContentType = "template",
            Payload = new Msg91Payload<TicketPdfComponents>
            {
                MessagingProduct = "whatsapp",
                Type = "template",
                Template = new Msg91Template<TicketPdfComponents>
                {
                    Name = _options.TicketPdfTemplateName,
                    Language = new Msg91Language { Code = "en", Policy = "deterministic" },
                    Namespace = null, // Must serialize as "namespace": null
                    ToAndComponents = new List<Msg91ToAndComponents<TicketPdfComponents>>
                    {
                        new()
                        {
                            To = new List<string> { recipientPhone },
                            Components = new TicketPdfComponents
                            {
                                Header1 = new DocumentHeaderComponent
                                {
                                    Type = "document",
                                    Value = data.PdfHttpsUrl,
                                    Filename = data.FileName
                                },
                                Body1 = new TextComponent { Value = data.AttendeeName },
                                Body2 = new TextComponent { Value = data.WorkshopTitle },
                                Body3 = new TextComponent { Value = data.WorkshopDate },
                                Body4 = new TextComponent { Value = data.WorkshopTime },
                                Body5 = new TextComponent { Value = data.BookingId }
                            }
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    private async Task<Msg91DispatchResult> PostToMsg91Async(
        string jsonPayload,
        string summary,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl);
        request.Headers.Add("authkey", _options.AuthKey);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        string responseContent = string.Empty;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds)));

            response = await _httpClient.SendAsync(request, timeoutCts.Token);
            responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                ex,
                "[MSG91 WhatsApp] Outbound HTTP request timed out after {Timeout}s. Setting ambiguous outcome. Summary: {Summary}",
                _options.TimeoutSeconds,
                summary);

            return Msg91DispatchResult.Timeout(
                $"MSG91 HTTP request timed out after {_options.TimeoutSeconds} seconds. Provider acceptance is ambiguous.",
                summary);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[MSG91 WhatsApp] Network/HTTP transport exception. Summary: {Summary}", summary);
            return Msg91DispatchResult.Transient(0, $"Network error: {ex.Message}", summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MSG91 WhatsApp] Unexpected exception during outbound dispatch. Summary: {Summary}", summary);
            return Msg91DispatchResult.Permanent(500, $"Internal client exception: {ex.Message}", summary);
        }

        var statusCode = (int)response.StatusCode;

        // Parse Provider Response Fields (never assume fixed schema)
        string? parsedMessageId = null;
        string? parsedRequestId = null;
        string? parsedMessage = null;
        bool hasErrorStatus = false;

        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            if (root.TryGetProperty("request_id", out var reqIdProp) || root.TryGetProperty("requestId", out reqIdProp))
            {
                parsedRequestId = reqIdProp.GetString();
            }

            if (root.TryGetProperty("message_id", out var msgIdProp) || root.TryGetProperty("messageId", out msgIdProp))
            {
                parsedMessageId = msgIdProp.GetString();
            }

            if (root.TryGetProperty("message", out var msgProp))
            {
                parsedMessage = msgProp.GetString();
            }

            if (root.TryGetProperty("status", out var statusProp))
            {
                var statusStr = statusProp.GetString();
                if (statusStr?.Equals("error", StringComparison.OrdinalIgnoreCase) == true ||
                    statusStr?.Equals("fail", StringComparison.OrdinalIgnoreCase) == true)
                {
                    hasErrorStatus = true;
                }
            }
        }
        catch (JsonException)
        {
            _logger.LogWarning("[MSG91 WhatsApp] Provider response was not valid JSON: {Content}", responseContent.Length > 200 ? responseContent[..200] : responseContent);
        }

        if (response.IsSuccessStatusCode && !hasErrorStatus)
        {
            _logger.LogInformation(
                "[MSG91 WhatsApp] Message successfully accepted by provider. RequestId: {ReqId} | Summary: {Summary}",
                parsedRequestId ?? "N/A",
                summary);

            return Msg91DispatchResult.Accepted(parsedMessageId, parsedRequestId, summary);
        }

        if (statusCode >= 400 && statusCode < 500)
        {
            var err = parsedMessage ?? $"HTTP {statusCode} Bad Request";
            _logger.LogWarning(
                "[MSG91 WhatsApp] Client error from MSG91 (HTTP {Code}): {Error}. Summary: {Summary}",
                statusCode,
                err,
                summary);

            return Msg91DispatchResult.Permanent(statusCode, err, summary);
        }

        var serverErr = parsedMessage ?? $"HTTP {statusCode} Provider Server Error";
        _logger.LogError(
            "[MSG91 WhatsApp] Transient server error from MSG91 (HTTP {Code}): {Error}. Summary: {Summary}",
            statusCode,
            serverErr,
            summary);

        return Msg91DispatchResult.Transient(statusCode, serverErr, summary);
    }

    private static bool IsSecureHttpsUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";
        }
        return "[INVALID_URL]";
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "[EMPTY]";
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4) return "******";
        return $"{new string('*', digits.Length - 4)}{digits[^4..]}";
    }

    private static string SanitizeSummary(string template, string phone, string bookingId)
    {
        return $"Template: {template} | Recipient: {MaskPhone(phone)} | Booking: {bookingId}";
    }
}
