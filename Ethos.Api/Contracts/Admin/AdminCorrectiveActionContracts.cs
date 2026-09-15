namespace Ethos.Api.Contracts.Admin;

public class ExecuteCorrectiveActionRequest
{
    public string ActionType { get; set; } = null!; // RESTORE_PACKAGE_QUOTA, RECONCILE_PAYMENT_GATEWAY, RECALCULATE_TRAINER_SNAPSHOT, TERMINATE_CORRUPTED_SESSION, RETRY_COMMUNICATION, OVERRIDE_BOOKING_STATUS
    public string TargetEntityType { get; set; } = null!; // PACKAGE, PAYMENT, COMMUNICATION, TRAINER, SESSION, BOOKING
    public Guid TargetEntityId { get; set; }
    public Guid? IncidentId { get; set; }
    public string Justification { get; set; } = null!;
    public string? PreconditionHash { get; set; }
    public string? ParametersJson { get; set; }
    public bool IsDryRun { get; set; } = false;
}

public class CorrectiveActionResponse
{
    public Guid Id { get; set; }
    public string ActionNumber { get; set; } = null!;
    public string ActionType { get; set; } = null!;
    public string TargetEntityType { get; set; } = null!;
    public Guid TargetEntityId { get; set; }
    public Guid? IncidentId { get; set; }
    public string? IncidentNumber { get; set; }
    public string TraceId { get; set; } = null!;
    public Guid AdminUserId { get; set; }
    public string AdminName { get; set; } = null!;
    public string Status { get; set; } = null!; // Proposed, Simulated, Executed, Failed
    public bool IsDryRun { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? PreconditionHash { get; set; }
    public string? ParametersJson { get; set; }
    public string? BeforeStateJson { get; set; }
    public string? AfterStateJson { get; set; }
    public string Justification { get; set; } = null!;
    public string? ExecutionLog { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CorrectiveActionDryRunResponse
{
    public bool IsDryRun => true;
    public bool CanExecute { get; set; }
    public string ActionType { get; set; } = null!;
    public string TargetEntityType { get; set; } = null!;
    public Guid TargetEntityId { get; set; }
    public string PreconditionHash { get; set; } = null!;
    public List<string> Warnings { get; set; } = new();
    public object? DiffBefore { get; set; }
    public object? DiffProjectedAfter { get; set; }
    public string Message { get; set; } = "Simulation completed. No live mutations occurred.";
}

public class CorrectiveActionMetricsResponse
{
    public int TotalActionsCount { get; set; }
    public int ExecutedCount { get; set; }
    public int SimulatedCount { get; set; }
    public int FailedCount { get; set; }
    public double SuccessRatePercentage { get; set; }

    // Friendly aliases
    public int TotalActions => TotalActionsCount;
    public int ExecutedActions => ExecutedCount;
    public int SimulatedActions => SimulatedCount;
    public int FailedActions => FailedCount;

    public Dictionary<string, int> ActionTypeBreakdown { get; set; } = new();
}
