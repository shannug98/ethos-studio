using Ethos.Api.Application.Common;
using Ethos.Api.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ethos.Api.Infrastructure.BackgroundWorkers;

public class WhatsAppOutboxBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<Msg91Options> _options;
    private readonly ILogger<WhatsAppOutboxBackgroundWorker> _logger;
    private readonly string _workerId;

    public WhatsAppOutboxBackgroundWorker(
        IServiceProvider serviceProvider,
        IOptions<Msg91Options> options,
        ILogger<WhatsAppOutboxBackgroundWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
        _workerId = $"worker-{Guid.NewGuid():N}"[..15];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[WhatsApp Outbox Worker {WorkerId}] Initialized. Polling interval: {Interval}s | Batch: {BatchSize}",
            _workerId,
            _options.Value.WorkerPollingIntervalSeconds,
            _options.Value.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IWhatsAppOutboxDispatcher>();

                // 1. Recover abandoned leases (e.g. from crashed or timed-out worker executions)
                var recoveredCount = await dispatcher.RecoverAbandonedLeasesAsync(stoppingToken);
                if (recoveredCount > 0)
                {
                    _logger.LogInformation(
                        "[WhatsApp Outbox Worker {WorkerId}] Recovered {Count} abandoned notification leases.",
                        _workerId,
                        recoveredCount);
                }

                // 2. Process pending batches with bounded size
                var processedCount = await dispatcher.ProcessPendingBatchAsync(_workerId, stoppingToken);

                // If work was processed, loop immediately (with small yield) to drain queue without delaying notifications
                if (processedCount > 0)
                {
                    await Task.Delay(250, stoppingToken);
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("[WhatsApp Outbox Worker {WorkerId}] Graceful shutdown requested.", _workerId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[WhatsApp Outbox Worker {WorkerId}] Transient iteration failure. Backing off before next poll.",
                    _workerId);
            }

            var pollInterval = TimeSpan.FromSeconds(Math.Clamp(_options.Value.WorkerPollingIntervalSeconds, 2, 60));
            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("[WhatsApp Outbox Worker {WorkerId}] Stopped.", _workerId);
    }
}
