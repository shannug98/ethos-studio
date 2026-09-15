using Ethos.Api.Application.Common;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Infrastructure.Telemetry;

public class TelemetryBackgroundWorker : BackgroundService
{
    private readonly ITelemetryQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelemetryBackgroundWorker> _logger;

    public TelemetryBackgroundWorker(
        ITelemetryQueue queue,
        IServiceProvider serviceProvider,
        ILogger<TelemetryBackgroundWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<ApiRequestLog>(64);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for at least one item
                var first = await _queue.DequeueAsync(stoppingToken);
                batch.Add(first);

                // Drain up to 63 more items available immediately
                while (batch.Count < 64 && _queue.TryDequeue(out var next) && next != null)
                {
                    batch.Add(next);
                }

                await PersistBatchAsync(batch, stoppingToken);
                batch.Clear();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist telemetry batch. Dropping batch to maintain system stability.");
                batch.Clear();
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task PersistBatchAsync(List<ApiRequestLog> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0) return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.ApiRequestLogs.AddRangeAsync(batch, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}