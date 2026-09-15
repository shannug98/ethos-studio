namespace Ethos.Api.Domain.Entities;

public class CommunicationLog
{
    public Guid Id { get; set; }
    public string MessageReference { get; set; } = null!; // MSG-YYYYMM-XXXX
    public string Channel { get; set; } = null!;          // SMS, WHATSAPP, EMAIL, IN_APP
    public string Recipient { get; set; } = null!;        // Masked phone or email
    public Guid? RecipientUserId { get; set; }
    public string TemplateId { get; set; } = null!;
    public string? Subject { get; set; }
    public string BodyPreview { get; set; } = null!;      // Sanitized preview (zero secrets)
    public string Status { get; set; } = "QUEUED";        // QUEUED, SENT, DELIVERED, FAILED, SIMULATED
    public string Provider { get; set; } = "SIMULATED";   // MSG91, SIMULATED
    public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
    public string? IdempotencyKey { get; set; }
    public string? Justification { get; set; }
    public string TraceId { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
}
