namespace Ethos.Api.Domain.Entities;

public class CorrectiveAction
{
    public Guid Id { get; set; }
    public string ActionNumber { get; set; } = null!; // ACT-YYYYMM-XXXX
    public string ActionType { get; set; } = null!;   // RESTORE_PACKAGE_QUOTA, RECONCILE_PAYMENT_GATEWAY, RECALCULATE_TRAINER_SNAPSHOT, TERMINATE_CORRUPTED_SESSION, RETRY_COMMUNICATION, OVERRIDE_BOOKING_STATUS
    public string TargetEntityType { get; set; } = null!; // PACKAGE, PAYMENT, COMMUNICATION, TRAINER, SESSION, BOOKING
    public Guid TargetEntityId { get; set; }
    public Guid? IncidentId { get; set; }
    public string TraceId { get; set; } = null!;
    public Guid AdminUserId { get; set; }
    public string Status { get; set; } = "Executed"; // Proposed, Simulated, Executed, Failed
    public string? IdempotencyKey { get; set; }
    public string? PreconditionHash { get; set; }
    public string? ParametersJson { get; set; }
    public string? BeforeStateJson { get; set; }
    public string? AfterStateJson { get; set; }
    public string Justification { get; set; } = null!;
    public string? ExecutionLog { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual Incident? Incident { get; set; }
    public virtual User? AdminUser { get; set; }
}
