using System.Text.Json.Serialization;

namespace Ethos.Api.Application.Notifications;

public record BookingConfirmedData(
    string AttendeeName,
    string WorkshopTitle,
    string WorkshopDate,
    string WorkshopTime,
    string BookingId
);

public record TicketPdfData(
    string AttendeeName,
    string WorkshopTitle,
    string WorkshopDate,
    string WorkshopTime,
    string BookingId,
    string PdfHttpsUrl,
    string FileName
);

public record Msg91DispatchResult(
    bool Success,
    bool IsTimeout = false,
    bool IsTransientError = false,
    bool IsPermanentError = false,
    bool Skipped = false,
    string? SkipReason = null,
    int StatusCode = 0,
    string? ProviderMessageId = null,
    string? ProviderRequestId = null,
    string? ErrorMessage = null,
    string? SanitizedPayloadSummary = null
)
{
    public static Msg91DispatchResult Accepted(string? messageId, string? requestId, string? summary = null) =>
        new(Success: true, StatusCode: 200, ProviderMessageId: messageId, ProviderRequestId: requestId, SanitizedPayloadSummary: summary);

    public static Msg91DispatchResult Timeout(string errorMessage, string? summary = null) =>
        new(Success: false, IsTimeout: true, ErrorMessage: errorMessage, SanitizedPayloadSummary: summary);

    public static Msg91DispatchResult Transient(int statusCode, string errorMessage, string? summary = null) =>
        new(Success: false, IsTransientError: true, StatusCode: statusCode, ErrorMessage: errorMessage, SanitizedPayloadSummary: summary);

    public static Msg91DispatchResult Permanent(int statusCode, string errorMessage, string? summary = null) =>
        new(Success: false, IsPermanentError: true, StatusCode: statusCode, ErrorMessage: errorMessage, SanitizedPayloadSummary: summary);

    public static Msg91DispatchResult Skip(string reason, string? summary = null) =>
        new(Success: false, Skipped: true, SkipReason: reason, SanitizedPayloadSummary: summary);
}

// Request Models for MSG91 Outbound API Serialization
public class Msg91OutboundEnvelope<TComponent>
{
    [JsonPropertyName("integrated_number")]
    public string IntegratedNumber { get; set; } = string.Empty;

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = "template";

    [JsonPropertyName("payload")]
    public Msg91Payload<TComponent> Payload { get; set; } = new();
}

public class Msg91Payload<TComponent>
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; set; } = "whatsapp";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "template";

    [JsonPropertyName("template")]
    public Msg91Template<TComponent> Template { get; set; } = new();
}

public class Msg91Template<TComponent>
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public Msg91Language Language { get; set; } = new();

    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }

    [JsonPropertyName("to_and_components")]
    public List<Msg91ToAndComponents<TComponent>> ToAndComponents { get; set; } = new();
}

public class Msg91Language
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "en";

    [JsonPropertyName("policy")]
    public string Policy { get; set; } = "deterministic";
}

public class Msg91ToAndComponents<TComponent>
{
    [JsonPropertyName("to")]
    public List<string> To { get; set; } = new();

    [JsonPropertyName("components")]
    public TComponent Components { get; set; } = default!;
}

public class BookingConfirmedComponents
{
    [JsonPropertyName("body_1")]
    public TextComponent Body1 { get; set; } = new();

    [JsonPropertyName("body_2")]
    public TextComponent Body2 { get; set; } = new();

    [JsonPropertyName("body_3")]
    public TextComponent Body3 { get; set; } = new();

    [JsonPropertyName("body_4")]
    public TextComponent Body4 { get; set; } = new();

    [JsonPropertyName("body_5")]
    public TextComponent Body5 { get; set; } = new();
}

public class TicketPdfComponents
{
    [JsonPropertyName("header_1")]
    public DocumentHeaderComponent Header1 { get; set; } = new();

    [JsonPropertyName("body_1")]
    public TextComponent Body1 { get; set; } = new();

    [JsonPropertyName("body_2")]
    public TextComponent Body2 { get; set; } = new();

    [JsonPropertyName("body_3")]
    public TextComponent Body3 { get; set; } = new();

    [JsonPropertyName("body_4")]
    public TextComponent Body4 { get; set; } = new();

    [JsonPropertyName("body_5")]
    public TextComponent Body5 { get; set; } = new();
}

public class TextComponent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class DocumentHeaderComponent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "document";

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;
}
