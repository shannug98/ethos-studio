using System.Threading;
using System.Threading.Tasks;

namespace Ethos.Api.Application.Finance;

public interface IRefundOutboxDispatcher
{
    Task<int> ProcessPendingBatchAsync(CancellationToken cancellationToken = default);
    Task<int> RecoverAbandonedRefundsAsync(CancellationToken cancellationToken = default);
}
