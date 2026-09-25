using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Application.Finance;

public class RefundOutboxDispatcher : IRefundOutboxDispatcher
{
    private readonly AppDbContext _dbContext;
    private readonly IRefundService _refundService;
    private readonly ILogger<RefundOutboxDispatcher> _logger;

    public RefundOutboxDispatcher(
        AppDbContext dbContext,
        IRefundService refundService,
        ILogger<RefundOutboxDispatcher> logger)
    {
        _dbContext = dbContext;
        _refundService = refundService;
        _logger = logger;
    }

    public async Task<int> RecoverAbandonedRefundsAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        int recovered = 0;

        // Recover abandoned PaymentRefunds
        var abandonedRefunds = await _dbContext.PaymentRefunds
            .Where(r => r.Status == RefundStatus.Processing && r.ProcessingStartedAtUtc != null && r.ProcessingStartedAtUtc < cutoff)
            .OrderBy(r => r.ProcessingStartedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var r in abandonedRefunds)
        {
            r.Status = RefundStatus.ReconciliationRequired;
            r.FailureReason = "Operation timed out in Processing state (>15 min). Requires gateway reconciliation.";
            recovered++;
            _logger.LogWarning("[RefundOutbox] Refund {RefundId} marked ReconciliationRequired due to abandoned lease.", r.Id);
        }

        // Recover abandoned RefundJobs
        var abandonedJobs = await _dbContext.RefundJobs
            .Where(j => j.Status == RefundStatus.Processing && j.CreatedAtUtc < cutoff)
            .OrderBy(j => j.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var j in abandonedJobs)
        {
            j.Status = RefundStatus.ReconciliationRequired;
            j.LastError = "Refund job remained in Processing state (>15 min). Requires reconciliation.";
            recovered++;
            _logger.LogWarning("[RefundOutbox] RefundJob {JobId} marked ReconciliationRequired due to timeout.", j.Id);
        }

        if (recovered > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return recovered;
    }

    public async Task<int> ProcessPendingBatchAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        const int batchSize = 10;

        List<Guid> candidateJobIds;

        if (_dbContext.Database.IsNpgsql())
        {
            using var claimTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            candidateJobIds = await _dbContext.RefundJobs
                .FromSqlInterpolated($@"
                    SELECT * FROM refund_jobs
                    WHERE (status = {(int)RefundStatus.Requested} OR
                          (status = {(int)RefundStatus.Failed} AND retry_count < 3 AND (next_retry_utc IS NULL OR next_retry_utc <= {now})))
                    ORDER BY created_at_utc
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED")
                .Select(j => j.Id)
                .ToListAsync(cancellationToken);

            if (candidateJobIds.Count == 0)
            {
                await claimTx.RollbackAsync(cancellationToken);
                return 0;
            }

            var jobsToClaim = await _dbContext.RefundJobs
                .Where(j => candidateJobIds.Contains(j.Id))
                .ToListAsync(cancellationToken);

            foreach (var job in jobsToClaim)
            {
                job.Status = RefundStatus.Processing;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await claimTx.CommitAsync(cancellationToken);
        }
        else
        {
            candidateJobIds = await _dbContext.RefundJobs
                .Where(j =>
                    j.Status == RefundStatus.Requested ||
                    (j.Status == RefundStatus.Failed && j.RetryCount < 3 && (j.NextRetryUtc == null || j.NextRetryUtc <= now)))
                .OrderBy(j => j.CreatedAtUtc)
                .Select(j => j.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (candidateJobIds.Count == 0) return 0;

            var jobsToClaim = await _dbContext.RefundJobs
                .Where(j => candidateJobIds.Contains(j.Id))
                .ToListAsync(cancellationToken);

            foreach (var job in jobsToClaim)
            {
                job.Status = RefundStatus.Processing;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        int processed = 0;
        foreach (var jobId in candidateJobIds)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var result = await _refundService.ProcessRefundJobAsync(jobId, cancellationToken);
                processed++;
                _logger.LogInformation("[RefundOutbox] Processed job {JobId}: Success={Success}, Status={Status}", jobId, result.Success, result.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RefundOutbox] Error executing refund job {JobId}", jobId);
            }
        }

        return processed;
    }
}
