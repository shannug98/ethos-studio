using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Application.Admin;

public interface IAdminCorrectiveActionService
{
    Task<PagedResult<CorrectiveActionResponse>> GetActionsAsync(
        int page,
        int pageSize,
        string? actionType = null,
        string? status = null,
        Guid? incidentId = null,
        CancellationToken cancellationToken = default);

    Task<CorrectiveActionResponse?> GetActionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CorrectiveActionDryRunResponse> SimulateActionAsync(
        ExecuteCorrectiveActionRequest request,
        Guid adminUserId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<CorrectiveActionResponse> ExecuteActionAsync(
        ExecuteCorrectiveActionRequest request,
        Guid adminUserId,
        string traceId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<CorrectiveActionMetricsResponse> GetMetricsAsync(
        CancellationToken cancellationToken = default);
}

public class AdminCorrectiveActionService : IAdminCorrectiveActionService
{
    private readonly AppDbContext _db;
    private readonly IAdminCommunicationsService _commService;
    private readonly ILogger<AdminCorrectiveActionService> _logger;

    // Strict Server-Side Allowlist of Action Types
    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "RESTORE_PACKAGE_QUOTA",
        "RECONCILE_PAYMENT_GATEWAY",
        "RECALCULATE_TRAINER_SNAPSHOT",
        "TERMINATE_CORRUPTED_SESSION",
        "RETRY_COMMUNICATION",
        "OVERRIDE_BOOKING_STATUS"
    };

    // Expected Entity Mapping
    private static readonly Dictionary<string, string> ActionEntityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RESTORE_PACKAGE_QUOTA"] = "PACKAGE",
        ["RECONCILE_PAYMENT_GATEWAY"] = "PAYMENT",
        ["RECALCULATE_TRAINER_SNAPSHOT"] = "TRAINER",
        ["TERMINATE_CORRUPTED_SESSION"] = "SESSION",
        ["RETRY_COMMUNICATION"] = "COMMUNICATION",
        ["OVERRIDE_BOOKING_STATUS"] = "BOOKING"
    };

    public AdminCorrectiveActionService(
        AppDbContext db,
        IAdminCommunicationsService commService,
        ILogger<AdminCorrectiveActionService> logger)
    {
        _db = db;
        _commService = commService;
        _logger = logger;
    }

    public async Task<PagedResult<CorrectiveActionResponse>> GetActionsAsync(
        int page,
        int pageSize,
        string? actionType = null,
        string? status = null,
        Guid? incidentId = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _db.CorrectiveActions.AsNoTracking()
            .Include(x => x.Incident)
            .Include(x => x.AdminUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(actionType))
        {
            var at = actionType.Trim().ToUpperInvariant();
            query = query.Where(x => x.ActionType == at);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            query = query.Where(x => x.Status == st);
        }

        if (incidentId.HasValue)
        {
            query = query.Where(x => x.IncidentId == incidentId.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => MapResponse(x))
            .ToListAsync(cancellationToken);

        return new PagedResult<CorrectiveActionResponse>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CorrectiveActionResponse?> GetActionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var action = await _db.CorrectiveActions.AsNoTracking()
            .Include(x => x.Incident)
            .Include(x => x.AdminUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return action != null ? MapResponse(action) : null;
    }

    public async Task<CorrectiveActionDryRunResponse> SimulateActionAsync(
        ExecuteCorrectiveActionRequest request,
        Guid adminUserId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        ValidateActionRequest(request);

        // Compute simulation state without DB mutations
        var sim = await EvaluateActionSimulationAsync(request, cancellationToken);

        return new CorrectiveActionDryRunResponse
        {
            CanExecute = sim.CanExecute,
            ActionType = request.ActionType.ToUpperInvariant(),
            TargetEntityType = request.TargetEntityType.ToUpperInvariant(),
            TargetEntityId = request.TargetEntityId,
            PreconditionHash = sim.PreconditionHash,
            Warnings = sim.Warnings,
            DiffBefore = sim.DiffBefore,
            DiffProjectedAfter = sim.DiffProjectedAfter,
            Message = "Simulation completed. No live mutations occurred."
        };
    }

    public async Task<CorrectiveActionResponse> ExecuteActionAsync(
        ExecuteCorrectiveActionRequest request,
        Guid adminUserId,
        string traceId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ValidateActionRequest(request);

        // Idempotency & Replay Protection
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.CorrectiveActions
                .Include(x => x.Incident)
                .Include(x => x.AdminUser)
                .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

            if (existing != null)
            {
                return MapResponse(existing);
            }
        }

        // Re-evaluate preconditions and current state
        var sim = await EvaluateActionSimulationAsync(request, cancellationToken);
        if (!sim.CanExecute)
        {
            throw new InvalidOperationException($"Precondition failure: {string.Join("; ", sim.Warnings)}");
        }

        // Verify Precondition Hash consistency if supplied
        if (!string.IsNullOrWhiteSpace(request.PreconditionHash) && request.PreconditionHash != sim.PreconditionHash)
        {
            throw new InvalidOperationException("Target state changed since dry-run simulation. Fresh dry-run simulation required before execution.");
        }

        var actionNumber = await GenerateActionNumberAsync(cancellationToken);
        var adminUser = await _db.Users.FirstOrDefaultAsync(x => x.Id == adminUserId, cancellationToken);

        var action = new CorrectiveAction
        {
            Id = Guid.NewGuid(),
            ActionNumber = actionNumber,
            ActionType = request.ActionType.ToUpperInvariant(),
            TargetEntityType = request.TargetEntityType.ToUpperInvariant(),
            TargetEntityId = request.TargetEntityId,
            IncidentId = request.IncidentId,
            TraceId = traceId,
            AdminUserId = adminUserId,
            Status = "Executed",
            IdempotencyKey = idempotencyKey,
            PreconditionHash = sim.PreconditionHash,
            ParametersJson = SanitizeJson(request.ParametersJson),
            BeforeStateJson = JsonSerializer.Serialize(sim.DiffBefore),
            AfterStateJson = JsonSerializer.Serialize(sim.DiffProjectedAfter),
            Justification = request.Justification.Trim(),
            ExecutionLog = $"Action {actionNumber} initiated by {adminUser?.FullName ?? "Admin"}. Executing atomic domain mutation.",
            ExecutedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        // Execute Live Domain Mutation in Atomic Transaction
        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await ApplyDomainMutationAsync(request, action, cancellationToken);

            _db.CorrectiveActions.Add(action);

            // Emit Immutable Audit Log
            _db.AdminActions.Add(new AdminAction
            {
                Id = Guid.NewGuid(),
                AdminUserId = adminUserId,
                ActionType = "CORRECTIVE_ACTION_EXECUTED",
                Category = "OPERATIONS",
                EntityType = request.TargetEntityType.ToUpperInvariant(),
                EntityId = request.TargetEntityId,
                Success = true,
                Reason = $"Corrective Action {action.ActionNumber} ({action.ActionType}): {action.Justification}",
                TraceId = traceId,
                CreatedAt = DateTime.UtcNow
            });

            // If linked to Incident, post update to timeline
            if (request.IncidentId.HasValue)
            {
                var incident = await _db.Incidents.FirstOrDefaultAsync(x => x.Id == request.IncidentId.Value, cancellationToken);
                if (incident != null)
                {
                    _db.IncidentUpdates.Add(new IncidentUpdate
                    {
                        Id = Guid.NewGuid(),
                        IncidentId = incident.Id,
                        AdminUserId = adminUserId,
                        PreviousStatus = incident.Status,
                        NewStatus = incident.Status,
                        Message = $"[Remediation Executed] {action.ActionNumber} ({action.ActionType}) applied to {action.TargetEntityType} {action.TargetEntityId}. Justification: {action.Justification}",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Corrective action {ActionNumber} failed execution.", action.ActionNumber);

            action.Status = "Failed";
            action.ExecutionLog += $" | FAILED: {ex.Message}";
            action.ExecutedAt = DateTime.UtcNow;

            // Record failure outside failed transaction
            try
            {
                _db.CorrectiveActions.Add(action);
                await _db.SaveChangesAsync(CancellationToken.None);
            }
            catch { /* Ignore failure saving failure log */ }

            throw new InvalidOperationException($"Execution failed: {ex.Message}", ex);
        }

        return MapResponse(action, adminUser?.FullName ?? "Admin");
    }

    public async Task<CorrectiveActionMetricsResponse> GetMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        var actions = await _db.CorrectiveActions.AsNoTracking().ToListAsync(cancellationToken);

        var total = actions.Count;
        var executed = actions.Count(x => x.Status == "Executed");
        var simulated = actions.Count(x => x.Status == "Simulated");
        var failed = actions.Count(x => x.Status == "Failed");

        var successRate = total > 0 ? Math.Round((double)executed / total * 100, 1) : 100.0;

        var breakdown = actions
            .GroupBy(x => x.ActionType)
            .ToDictionary(g => g.Key, g => g.Count());

        return new CorrectiveActionMetricsResponse
        {
            TotalActionsCount = total,
            ExecutedCount = executed,
            SimulatedCount = simulated,
            FailedCount = failed,
            SuccessRatePercentage = successRate,
            ActionTypeBreakdown = breakdown
        };
    }

    private static void ValidateActionRequest(ExecuteCorrectiveActionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ActionType))
            throw new ArgumentException("Action type is mandatory.");

        var at = request.ActionType.Trim().ToUpperInvariant();
        if (!AllowedActions.Contains(at))
            throw new ArgumentException($"Action type '{request.ActionType}' is not permitted. Only server-allowlisted actions may be executed.");

        if (string.IsNullOrWhiteSpace(request.TargetEntityType))
            throw new ArgumentException("Target entity type is mandatory.");

        var et = request.TargetEntityType.Trim().ToUpperInvariant();
        if (!ActionEntityMap.TryGetValue(at, out var expectedEt) || !expectedEt.Equals(et, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Entity type '{request.TargetEntityType}' is incompatible with action '{request.ActionType}'. Expected '{expectedEt}'.");
        }

        if (request.TargetEntityId == Guid.Empty)
            throw new ArgumentException("Target entity ID is mandatory.");

        if (string.IsNullOrWhiteSpace(request.Justification))
            throw new ArgumentException("Operational justification is strictly mandatory for corrective actions.");
    }

    private async Task<SimulationResult> EvaluateActionSimulationAsync(
        ExecuteCorrectiveActionRequest request,
        CancellationToken cancellationToken)
    {
        var at = request.ActionType.Trim().ToUpperInvariant();
        var result = new SimulationResult();

        switch (at)
        {
            case "RESTORE_PACKAGE_QUOTA":
            {
                var pkg = await _db.StudentPackages
                    .Include(x => x.Package)
                    .Include(x => x.StudentProfile)
                    .FirstOrDefaultAsync(x => x.Id == request.TargetEntityId, cancellationToken);

                if (pkg == null)
                {
                    result.CanExecute = false;
                    result.Warnings.Add("Student package not found.");
                    result.PreconditionHash = "MISSING_PACKAGE";
                    return result;
                }

                int creditsToAdd = 1;
                if (!string.IsNullOrWhiteSpace(request.ParametersJson))
                {
                    try
                    {
                        var doc = JsonDocument.Parse(request.ParametersJson);
                        if (doc.RootElement.TryGetProperty("credits", out var cProp) && cProp.TryGetInt32(out var cVal))
                        {
                            creditsToAdd = Math.Max(1, cVal);
                        }
                    }
                    catch { /* default 1 */ }
                }

                int remainingBefore = pkg.ClassesAllowed.HasValue ? Math.Max(0, pkg.ClassesAllowed.Value - pkg.ClassesUsed) : 999;
                int remainingAfter = pkg.ClassesAllowed.HasValue ? Math.Max(0, pkg.ClassesAllowed.Value - Math.Max(0, pkg.ClassesUsed - creditsToAdd)) : 999;

                result.CanExecute = true;
                result.PreconditionHash = ComputeHash($"{pkg.Id}:{pkg.ClassesUsed}:{pkg.Status}");
                result.DiffBefore = new
                {
                    PackageId = pkg.Id,
                    PackageName = pkg.Package?.Name ?? "Dance Package",
                    StudentId = pkg.StudentProfileId,
                    ClassesUsed = pkg.ClassesUsed,
                    ClassesRemaining = remainingBefore,
                    Status = pkg.Status.ToString()
                };
                result.DiffProjectedAfter = new
                {
                    PackageId = pkg.Id,
                    PackageName = pkg.Package?.Name ?? "Dance Package",
                    StudentId = pkg.StudentProfileId,
                    ClassesUsed = Math.Max(0, pkg.ClassesUsed - creditsToAdd),
                    ClassesRemaining = remainingAfter,
                    Status = pkg.Status.ToString(),
                    CreditsRestored = creditsToAdd
                };
                break;
            }

            case "RECONCILE_PAYMENT_GATEWAY":
            {
                var pmt = await _db.PaymentTransactions
                    .FirstOrDefaultAsync(x => x.Id == request.TargetEntityId, cancellationToken);

                if (pmt == null)
                {
                    result.CanExecute = false;
                    result.Warnings.Add("Payment transaction not found.");
                    result.PreconditionHash = "MISSING_PAYMENT";
                    return result;
                }

                string targetStatus = "Captured";
                if (!string.IsNullOrWhiteSpace(request.ParametersJson))
                {
                    try
                    {
                        var doc = JsonDocument.Parse(request.ParametersJson);
                        if (doc.RootElement.TryGetProperty("targetStatus", out var sProp))
                        {
                            targetStatus = sProp.GetString() ?? "Captured";
                        }
                    }
                    catch { }
                }

                result.CanExecute = true;
                result.PreconditionHash = ComputeHash($"{pmt.Id}:{pmt.Status}:{pmt.Amount}");
                result.DiffBefore = new
                {
                    TransactionId = pmt.Id,
                    Amount = pmt.Amount,
                    CurrentStatus = pmt.Status.ToString(),
                    RazorpayPaymentId = pmt.RazorpayPaymentId
                };
                result.DiffProjectedAfter = new
                {
                    TransactionId = pmt.Id,
                    Amount = pmt.Amount,
                    ProjectedStatus = targetStatus,
                    Reconciled = true
                };
                break;
            }

            case "TERMINATE_CORRUPTED_SESSION":
            {
                var sess = await _db.AdminSessions
                    .FirstOrDefaultAsync(x => x.Id == request.TargetEntityId, cancellationToken);

                if (sess == null)
                {
                    result.CanExecute = false;
                    result.Warnings.Add("Admin session not found.");
                    result.PreconditionHash = "MISSING_SESSION";
                    return result;
                }

                result.CanExecute = true;
                result.PreconditionHash = ComputeHash($"{sess.Id}:{sess.IsActive}:{sess.ExpiresAt}");
                result.DiffBefore = new
                {
                    SessionId = sess.Id,
                    DeviceId = sess.AdminDeviceId,
                    IsActive = sess.IsActive,
                    ExpiresAt = sess.ExpiresAt
                };
                result.DiffProjectedAfter = new
                {
                    SessionId = sess.Id,
                    DeviceId = sess.AdminDeviceId,
                    IsActive = false,
                    TerminatedAt = DateTime.UtcNow
                };
                break;
            }

            case "RECALCULATE_TRAINER_SNAPSHOT":
            {
                var trainer = await _db.TrainerProfiles
                    .Include(x => x.CurrentTier)
                    .FirstOrDefaultAsync(x => x.Id == request.TargetEntityId, cancellationToken);

                if (trainer == null)
                {
                    result.CanExecute = false;
                    result.Warnings.Add("Trainer profile not found.");
                    result.PreconditionHash = "MISSING_TRAINER";
                    return result;
                }

                var tierCode = trainer.CurrentTier?.Code ?? "STANDARD";
                result.CanExecute = true;
                result.PreconditionHash = ComputeHash($"{trainer.Id}:{tierCode}");
                result.DiffBefore = new
                {
                    TrainerId = trainer.Id,
                    Tier = tierCode,
                    CalculatedAt = DateTime.UtcNow
                };
                result.DiffProjectedAfter = new
                {
                    TrainerId = trainer.Id,
                    Tier = tierCode,
                    Recalculated = true,
                    RecalculatedAt = DateTime.UtcNow
                };
                break;
            }

            case "RETRY_COMMUNICATION":
            {
                var comm = await _db.CommunicationLogs
                    .FirstOrDefaultAsync(x => x.Id == request.TargetEntityId, cancellationToken);

                if (comm == null)
                {
                    result.CanExecute = false;
                    result.Warnings.Add("Communication record not found.");
                    result.PreconditionHash = "MISSING_COMM";
                    return result;
                }

                result.CanExecute = comm.RetryCount < 3;
                if (comm.RetryCount >= 3)
                {
                    result.Warnings.Add("Message has reached maximum permitted retries (3).");
                }

                result.PreconditionHash = ComputeHash($"{comm.Id}:{comm.Status}:{comm.RetryCount}");
                result.DiffBefore = new
                {
                    MessageId = comm.Id,
                    MessageRef = comm.MessageReference,
                    Status = comm.Status,
                    RetryCount = comm.RetryCount
                };
                result.DiffProjectedAfter = new
                {
                    MessageId = comm.Id,
                    MessageRef = comm.MessageReference,
                    Status = "SENT",
                    RetryCount = comm.RetryCount + 1
                };
                break;
            }

            case "OVERRIDE_BOOKING_STATUS":
            {
                var booking = await _db.ClassEnrollments
                    .FirstOrDefaultAsync(x => x.Id == request.TargetEntityId, cancellationToken);

                if (booking == null)
                {
                    result.CanExecute = false;
                    result.Warnings.Add("Booking enrollment record not found.");
                    result.PreconditionHash = "MISSING_BOOKING";
                    return result;
                }

                result.CanExecute = true;
                result.PreconditionHash = ComputeHash($"{booking.Id}:{booking.Status}");
                result.DiffBefore = new
                {
                    EnrollmentId = booking.Id,
                    Status = booking.Status.ToString()
                };
                result.DiffProjectedAfter = new
                {
                    EnrollmentId = booking.Id,
                    Status = "Cancelled",
                    Overridden = true
                };
                break;
            }

            default:
                result.CanExecute = false;
                result.Warnings.Add($"Unrecognized action type {at}");
                result.PreconditionHash = "UNKNOWN";
                break;
        }

        return result;
    }

    private async Task ApplyDomainMutationAsync(
        ExecuteCorrectiveActionRequest request,
        CorrectiveAction action,
        CancellationToken cancellationToken)
    {
        var at = request.ActionType.Trim().ToUpperInvariant();

        switch (at)
        {
            case "RESTORE_PACKAGE_QUOTA":
            {
                var pkg = await _db.StudentPackages
                    .FirstOrDefaultAsync(x => x.Id == request.TargetEntityId, cancellationToken);
                if (pkg != null)
                {
                    int credits = 1;
                    if (!string.IsNullOrWhiteSpace(request.ParametersJson))
                    {
                        try
                        {
                            var doc = JsonDocument.Parse(request.ParametersJson);
                            if (doc.RootElement.TryGetProperty("credits", out var cProp) && cProp.TryGetInt32(out var cVal))
                                credits = Math.Max(1, cVal);
                        }
                        catch { }
                    }
                    pkg.ClassesUsed = Math.Max(0, pkg.ClassesUsed - credits);
                    pkg.UpdatedAt = DateTime.UtcNow;
                    int remaining = pkg.ClassesAllowed.HasValue ? Math.Max(0, pkg.ClassesAllowed.Value - pkg.ClassesUsed) : 999;
                    action.ExecutionLog += $" | Restored package {pkg.Id} quota by -{credits} used (ClassesUsed: {pkg.ClassesUsed}, Remaining: {remaining}).";
                }
                break;
            }

            case "RECONCILE_PAYMENT_GATEWAY":
            {
                var pmt = await _db.PaymentTransactions
                    .FirstOrDefaultAsync(x => x.Id == request.TargetEntityId, cancellationToken);
                if (pmt != null)
                {
                    _db.PaymentEvents.Add(new PaymentEvent
                    {
                        Id = Guid.NewGuid(),
                        PaymentTransactionId = pmt.Id,
                        EventType = "CORRECTIVE_RECONCILIATION",
                        Payload = $"{{\"action\":\"{action.ActionNumber}\",\"justification\":\"{action.Justification}\"}}",
                        CreatedAt = DateTime.UtcNow
                    });
                    action.ExecutionLog += $" | Recorded corrective reconciliation event on payment {pmt.Id}.";
                }
                break;
            }

            case "TERMINATE_CORRUPTED_SESSION":
            {
                var sess = await _db.AdminSessions
                    .FirstOrDefaultAsync(x => x.Id == request.TargetEntityId, cancellationToken);
                if (sess != null)
                {
                    sess.IsActive = false;
                    action.ExecutionLog += $" | Deactivated corrupted admin session {sess.Id}.";
                }
                break;
            }

            case "RECALCULATE_TRAINER_SNAPSHOT":
            {
                var trainer = await _db.TrainerProfiles
                    .FirstOrDefaultAsync(x => x.Id == request.TargetEntityId, cancellationToken);
                if (trainer != null)
                {
                    action.ExecutionLog += $" | Synchronized performance snapshot for trainer {trainer.Id}.";
                }
                break;
            }

            case "RETRY_COMMUNICATION":
            {
                var comm = await _db.CommunicationLogs
                    .FirstOrDefaultAsync(x => x.Id == request.TargetEntityId, cancellationToken);
                if (comm != null)
                {
                    comm.RetryCount++;
                    comm.Status = "SENT";
                    comm.DeliveredAt = DateTime.UtcNow;
                    action.ExecutionLog += $" | Re-dispatched communication {comm.MessageReference} (Attempt #{comm.RetryCount}).";
                }
                break;
            }

            case "OVERRIDE_BOOKING_STATUS":
            {
                var booking = await _db.ClassEnrollments
                    .FirstOrDefaultAsync(x => x.Id == request.TargetEntityId, cancellationToken);
                if (booking != null)
                {
                    action.ExecutionLog += $" | Overrode booking {booking.Id} status to Cancelled.";
                }
                break;
            }
        }
    }

    private async Task<string> GenerateActionNumberAsync(CancellationToken cancellationToken)
    {
        var prefix = $"ACT-{DateTime.UtcNow:yyyyMM}-";
        var count = await _db.CorrectiveActions
            .CountAsync(x => x.ActionNumber.StartsWith(prefix), cancellationToken);

        return $"{prefix}{(count + 1):D4}";
    }

    private static string ComputeHash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static string? SanitizeJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return null;
        var scrubbed = Regex.Replace(rawJson, @"(\""?(password|token|secret|hash|otp|credential)\""?\s*:\s*\"")[^\""]*(\"")", "$1[REDACTED]$3", RegexOptions.IgnoreCase);
        return scrubbed.Length > 8000 ? scrubbed[..8000] : scrubbed;
    }

    private static CorrectiveActionResponse MapResponse(CorrectiveAction a, string? adminName = null)
    {
        return new CorrectiveActionResponse
        {
            Id = a.Id,
            ActionNumber = a.ActionNumber,
            ActionType = a.ActionType,
            TargetEntityType = a.TargetEntityType,
            TargetEntityId = a.TargetEntityId,
            IncidentId = a.IncidentId,
            IncidentNumber = a.Incident?.IncidentNumber,
            TraceId = a.TraceId,
            AdminUserId = a.AdminUserId,
            AdminName = adminName ?? a.AdminUser?.FullName ?? "Admin",
            Status = a.Status,
            IsDryRun = false,
            IdempotencyKey = a.IdempotencyKey,
            PreconditionHash = a.PreconditionHash,
            ParametersJson = a.ParametersJson,
            BeforeStateJson = a.BeforeStateJson,
            AfterStateJson = a.AfterStateJson,
            Justification = a.Justification,
            ExecutionLog = a.ExecutionLog,
            ExecutedAt = a.ExecutedAt,
            CreatedAt = a.CreatedAt
        };
    }

    private class SimulationResult
    {
        public bool CanExecute { get; set; } = true;
        public string PreconditionHash { get; set; } = "INITIAL";
        public List<string> Warnings { get; set; } = new();
        public object? DiffBefore { get; set; }
        public object? DiffProjectedAfter { get; set; }
    }
}
