namespace Ethos.Api.Application.Notifications;

public interface IWhatsAppOutboxDispatcher
{
    Task<int> ProcessPendingBatchAsync(string workerId, CancellationToken cancellationToken = default);

    Task<int> RecoverAbandonedLeasesAsync(CancellationToken cancellationToken = default);
}
