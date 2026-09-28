using Ethos.Api.Application.Common;
using Ethos.Api.Application.Feedback;
using Ethos.Api.Infrastructure.BackgroundWorkers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ethos.Api.Tests.Feedback;

public class FeedbackEligibilityBackgroundWorkerTests
{
    private class MockWorkerLivenessTracker : IWorkerLivenessTracker
    {
        public Dictionary<string, (DateTime Timestamp, string? Status)> Heartbeats { get; } = new();

        public void RecordHeartbeat(string workerKey, string? statusInfo = null, long itemsProcessedDelta = 0, string? lastError = null)
        {
            Heartbeats[workerKey] = (DateTime.UtcNow, statusInfo);
        }

        public WorkerHeartbeat? GetHeartbeat(string workerKey) => null;

        public IReadOnlyDictionary<string, WorkerHeartbeat> GetAllHeartbeats() =>
            new Dictionary<string, WorkerHeartbeat>();
    }

    private class MockEligibilityService : IFeedbackEligibilityService
    {
        public int InvocationCount { get; private set; }
        public Func<Task<FeedbackEligibilityEvaluationResult>>? Handler { get; set; }

        public Task<FeedbackEligibilityEvaluationResult> EvaluateWorkshopBookingsAsync(
            Guid workshopId,
            DateTime? nowUtc = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FeedbackEligibilityEvaluationResult { WorkshopId = workshopId });

        public async Task<FeedbackEligibilityEvaluationResult> ProcessCompletedWorkshopsAsync(
            DateTime? nowUtc = null,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            if (Handler != null)
            {
                return await Handler();
            }

            return new FeedbackEligibilityEvaluationResult
            {
                WorkshopsProcessed = 1,
                NotificationsQueuedCount = 2,
                TokensGeneratedCount = 2
            };
        }
    }

    [Fact]
    public async Task ExecuteAsync_InvokesProcessCompletedWorkshopsAsync_AndRecordsHeartbeat()
    {
        var services = new ServiceCollection();
        var mockEligibility = new MockEligibilityService();
        services.AddScoped<IFeedbackEligibilityService>(_ => mockEligibility);
        var serviceProvider = services.BuildServiceProvider();

        var liveness = new MockWorkerLivenessTracker();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Feedback:WorkerEnabled"] = "true",
                ["Feedback:WorkerPollingIntervalSeconds"] = "1"
            })
            .Build();

        var worker = new FeedbackEligibilityBackgroundWorker(
            serviceProvider,
            config,
            NullLogger<FeedbackEligibilityBackgroundWorker>.Instance,
            liveness);

        using var cts = new CancellationTokenSource();
        var executeTask = worker.StartAsync(cts.Token);

        // Allow worker to run at least one iteration
        await Task.Delay(150);

        cts.Cancel();
        await executeTask;

        Assert.True(mockEligibility.InvocationCount >= 1);
        Assert.True(liveness.Heartbeats.ContainsKey("worker_feedback_eligibility"));
        Assert.Equal("Active / Polling", liveness.Heartbeats["worker_feedback_eligibility"].Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabledInConfiguration_StandsBy()
    {
        var services = new ServiceCollection();
        var mockEligibility = new MockEligibilityService();
        services.AddScoped<IFeedbackEligibilityService>(_ => mockEligibility);
        var serviceProvider = services.BuildServiceProvider();

        var liveness = new MockWorkerLivenessTracker();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Feedback:WorkerEnabled"] = "false"
            })
            .Build();

        var worker = new FeedbackEligibilityBackgroundWorker(
            serviceProvider,
            config,
            NullLogger<FeedbackEligibilityBackgroundWorker>.Instance,
            liveness);

        using var cts = new CancellationTokenSource();
        var task = worker.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await task;

        Assert.Equal(0, mockEligibility.InvocationCount);
        Assert.True(liveness.Heartbeats.ContainsKey("worker_feedback_eligibility"));
        Assert.Equal("Standby / Disabled in configuration", liveness.Heartbeats["worker_feedback_eligibility"].Status);
    }

    [Fact]
    public async Task ExecuteAsync_CatchesTransientException_AndContinuesPolling()
    {
        var services = new ServiceCollection();
        var callCount = 0;
        var mockEligibility = new MockEligibilityService
        {
            Handler = () =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Simulated transient database connection timeout");
                }
                return Task.FromResult(new FeedbackEligibilityEvaluationResult { WorkshopsProcessed = 1 });
            }
        };
        services.AddScoped<IFeedbackEligibilityService>(_ => mockEligibility);
        var serviceProvider = services.BuildServiceProvider();

        var liveness = new MockWorkerLivenessTracker();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Feedback:WorkerEnabled"] = "true",
                ["Feedback:WorkerPollingIntervalSeconds"] = "1"
            })
            .Build();

        var worker = new FeedbackEligibilityBackgroundWorker(
            serviceProvider,
            config,
            NullLogger<FeedbackEligibilityBackgroundWorker>.Instance,
            liveness);

        using var cts = new CancellationTokenSource();
        var executeTask = worker.StartAsync(cts.Token);

        // Allow worker to run two iterations (1 failing, 1 succeeding)
        await Task.Delay(1150);

        cts.Cancel();
        await executeTask;

        Assert.True(callCount >= 2);
    }
}
