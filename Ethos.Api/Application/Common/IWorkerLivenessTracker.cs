using System.Collections.Concurrent;

namespace Ethos.Api.Application.Common;

public class WorkerHeartbeat
{
    public string WorkerKey { get; set; } = null!;
    public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;
    public string? StatusInfo { get; set; }
    public long CyclesCompleted { get; set; }
    public long ItemsProcessed { get; set; }
    public string? LastError { get; set; }
}

public interface IWorkerLivenessTracker
{
    void RecordHeartbeat(string workerKey, string? statusInfo = null, long itemsProcessedDelta = 0, string? lastError = null);
    WorkerHeartbeat? GetHeartbeat(string workerKey);
    IReadOnlyDictionary<string, WorkerHeartbeat> GetAllHeartbeats();
}

public class WorkerLivenessTracker : IWorkerLivenessTracker
{
    private readonly ConcurrentDictionary<string, WorkerHeartbeat> _heartbeats = new(StringComparer.OrdinalIgnoreCase);

    public void RecordHeartbeat(string workerKey, string? statusInfo = null, long itemsProcessedDelta = 0, string? lastError = null)
    {
        _heartbeats.AddOrUpdate(
            workerKey,
            key => new WorkerHeartbeat
            {
                WorkerKey = key,
                LastHeartbeatUtc = DateTime.UtcNow,
                StatusInfo = statusInfo,
                CyclesCompleted = 1,
                ItemsProcessed = Math.Max(0, itemsProcessedDelta),
                LastError = lastError
            },
            (key, existing) =>
            {
                existing.LastHeartbeatUtc = DateTime.UtcNow;
                if (statusInfo != null) existing.StatusInfo = statusInfo;
                existing.CyclesCompleted++;
                if (itemsProcessedDelta > 0) existing.ItemsProcessed += itemsProcessedDelta;
                if (lastError != null) existing.LastError = lastError;
                return existing;
            });
    }

    public WorkerHeartbeat? GetHeartbeat(string workerKey)
    {
        return _heartbeats.TryGetValue(workerKey, out var hb) ? hb : null;
    }

    public IReadOnlyDictionary<string, WorkerHeartbeat> GetAllHeartbeats()
    {
        return _heartbeats;
    }
}
