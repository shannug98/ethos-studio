namespace Ethos.Api.Application.Common;

public class Msg91Options
{
    public const string SectionName = "Msg91";

    public bool Enabled { get; set; } = false;

    public string? AuthKey { get; set; }

    public string? IntegratedNumber { get; set; }

    public string BaseUrl { get; set; } = "https://api.msg91.com/api/v5/whatsapp/whatsapp-outbound-message/bulk/";

    public string BookingConfirmedTemplateName { get; set; } = "ethos_booking_confirmed";

    public string BookingConfirmedNamespace { get; set; } = "df6202a5_c621_4e2b_90a3_9d5ef2e14f8a";

    public string TicketPdfTemplateName { get; set; } = "ethos_ticket_pdf";

    public string? TicketPdfNamespace { get; set; } = null;

    public string DefaultCountryCode { get; set; } = "91";

    public int TimeoutSeconds { get; set; } = 15;

    public int MaxRetryAttempts { get; set; } = 3;

    public int LeaseDurationSeconds { get; set; } = 60;

    public int WorkerPollingIntervalSeconds { get; set; } = 5;

    public int BatchSize { get; set; } = 10;

    public int PdfUrlExpiryHours { get; set; } = 24;

    public bool AllowTestEndpoints { get; set; } = false;

    public List<string> ApprovedTestNumbers { get; set; } = new();

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AuthKey) &&
        !string.IsNullOrWhiteSpace(IntegratedNumber);
}
