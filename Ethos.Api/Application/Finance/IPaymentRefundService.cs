using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Application.Finance;

public interface IPaymentRefundService
{
    Task<AdminRefundResponse> RecordExternalRefundAsync(
        Guid transactionId,
        Guid adminUserId,
        AdminRecordRefundRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken);
}