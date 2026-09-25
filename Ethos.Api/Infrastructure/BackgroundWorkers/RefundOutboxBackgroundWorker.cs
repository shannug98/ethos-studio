using System;
using System.Threading;
using System.Threading.Tasks;
using Ethos.Api.Application.Finance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Infrastructure.BackgroundWorkers;

public class RefundOutboxBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RefundOutboxBackgroundWorker> _logger;

    public RefundOutboxBackgroundWorker(
        IServiceProvider serviceProvider,
        ILogger<RefundOutboxBackgroundWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Refund Outbox Worker] Initialized.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IRefundOutboxDispatcher>();

                // 1. Recover abandoned leases / timeouts
                var recoveredCount = await dispatcher.RecoverAbandonedRefundsAsync(stoppingToken);
                if (recoveredCount > 0)
                {
                    _logger.LogInformation("[Refund Outbox Worker] Recovered {Count} abandoned refunds.", recoveredCount);
                }

                // 2. Process pending batches
                var processedCount = await dispatcher.ProcessPendingBatchAsync(stoppingToken);
                if (processedCount > 0)
                {
                    await Task.Delay(250, stoppingToken);
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Refund Outbox Worker] Unexpected error in polling cycle.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("[Refund Outbox Worker] Stopped.");
    }
}
