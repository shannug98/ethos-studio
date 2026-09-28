using Ethos.Api.Application.Common;
using Ethos.Api.Application.Feedback;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Infrastructure.BackgroundWorkers;

public class FeedbackEligibilityBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FeedbackEligibilityBackgroundWorker> _logger;
    private readonly IWorkerLivenessTracker _livenessTracker;
    private readonly SemaphoreSlim _executionLock = new(1, 1);
    private readonly string _workerId;

    public FeedbackEligibilityBackgroundWorker(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<FeedbackEligibilityBackgroundWorker> logger,
        IWorkerLivenessTracker livenessTracker)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _livenessTracker = livenessTracker;
        _workerId = $"worker-fb-{Guid.NewGuid():N}"[..15];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var isEnabled = _configuration.GetValue<bool?>("Feedback:WorkerEnabled") ?? true;
        if (!isEnabled)
        {
            _livenessTracker.RecordHeartbeat("worker_feedback_eligibility", "Standby / Disabled in configuration");
            _logger.LogInformation("[Feedback Eligibility Worker] Disabled via configuration.");
            return;
        }

        var pollingSeconds = _configuration.GetValue<int?>("Feedback:WorkerPollingIntervalSeconds") ?? 300; // 5 minutes default
        var pollInterval = TimeSpan.FromSeconds(Math.Clamp(pollingSeconds, 1, 3600));

        _logger.LogInformation(
            "[Feedback Eligibility Worker {WorkerId}] Initialized. Polling interval: {Interval}s ({Minutes:F1}m)",
            _workerId,
            pollInterval.TotalSeconds,
            pollInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            _livenessTracker.RecordHeartbeat("worker_feedback_eligibility", "Active / Polling");

            var lockAcquired = false;
            try
            {
                // Prevent overlapping execution cycles within the same application process
                lockAcquired = await _executionLock.WaitAsync(0, stoppingToken);
                if (lockAcquired)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var eligibilityService = scope.ServiceProvider.GetRequiredService<IFeedbackEligibilityService>();

                    var result = await eligibilityService.ProcessCompletedWorkshopsAsync(DateTime.UtcNow, stoppingToken);

                    if (result.NotificationsQueuedCount > 0 || result.TokensGeneratedCount > 0)
                    {
                        _logger.LogInformation(
                            "[Feedback Eligibility Worker {WorkerId}] Evaluated {Workshops} workshops ({Evaluated} bookings): Queued {Queued} WhatsApp outbox records ({Attended} attended, {NoShow} no-show). Generated {Tokens} feedback tokens.",
                            _workerId,
                            result.WorkshopsProcessed,
                            result.TotalBookingsEvaluated,
                            result.NotificationsQueuedCount,
                            result.EligibleAttendedCount,
                            result.EligibleNoShowCount,
                            result.TokensGeneratedCount);
                    }
                }
                else
                {
                    _logger.LogWarning("[Feedback Eligibility Worker {WorkerId}] Prior evaluation iteration is still active. Skipping overlapping run.", _workerId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("[Feedback Eligibility Worker {WorkerId}] Graceful shutdown requested.", _workerId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[Feedback Eligibility Worker {WorkerId}] Transient evaluation cycle failure. Backing off before next poll.",
                    _workerId);
            }
            finally
            {
                if (lockAcquired)
                {
                    _executionLock.Release();
                }
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("[Feedback Eligibility Worker {WorkerId}] Stopped.", _workerId);
    }
}
