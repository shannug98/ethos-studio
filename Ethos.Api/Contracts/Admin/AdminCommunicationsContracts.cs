namespace Ethos.Api.Contracts.Admin;

public class SendCommunicationRequest
{
    public string Channel { get; set; } = null!; // SMS, WHATSAPP, EMAIL, IN_APP
    public string Recipient { get; set; } = null!; // Target phone or email
    public string TemplateId { get; set; } = null!; // Must be in approved template catalog
    public Dictionary<string, string> Parameters { get; set; } = new();
    public Dictionary<string, string>? TemplateParameters { get; set; }
    public string Justification { get; set; } = null!; // Mandatory operational justification
    public Guid? RecipientUserId { get; set; }
}

public class RetryCommunicationRequest
{
    public Guid? CommunicationId { get; set; }
    public string Justification { get; set; } = null!; // Mandatory operational justification
}

public class CommunicationLogResponse
{
    public Guid Id { get; set; }
    public string MessageReference { get; set; } = null!; // MSG-YYYYMM-XXXX
    public string Channel { get; set; } = null!;
    public string RecipientMasked { get; set; } = null!;
    public string Recipient => RecipientMasked;
    public Guid? RecipientUserId { get; set; }
    public string TemplateId { get; set; } = null!;
    public string? Subject { get; set; }
    public string BodyPreview { get; set; } = null!;
    public string Status { get; set; } = null!; // QUEUED, SENT, DELIVERED, FAILED, SIMULATED
    public string Provider { get; set; } = null!; // MSG91, SIMULATED
    public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public string? Justification { get; set; }
    public string TraceId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
}

public class CommunicationTemplateDto
{
    public string TemplateId { get; set; } = null!;
    public string Id => TemplateId;
    public string Name { get; set; } = null!;
    public string Channel { get; set; } = null!;
    public string DltTemplateId { get; set; } = null!;
    public string Description { get; set; } = null!;
    public List<string> RequiredParameters { get; set; } = new();
    public string SampleBody { get; set; } = null!;
}

public class CommunicationsMetricsResponse
{
    public int TotalSentCount { get; set; }
    public int DeliveredCount { get; set; }
    public int SimulatedCount { get; set; }
    public int FailedCount { get; set; }
    public double DeliveryRatePercentage { get; set; }
    public int TotalSent => TotalSentCount;
    public double DeliveryRatePercent => DeliveryRatePercentage;
    public Dictionary<string, int> ChannelBreakdown { get; set; } = new();
    public Dictionary<string, int> StatusBreakdown { get; set; } = new();
    public string SubsystemHealthStatus { get; set; } = "Simulated"; // Healthy, Degraded, Simulated, NotConfigured
}
