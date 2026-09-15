using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Application.Finance;

public interface IPaymentReconciliationService
{
    Task<AdminPaymentTransactionResponse> ReconcilePaymentAsync(
        Guid transactionId,
        Guid adminUserId,
        AdminReconcilePaymentRequest request,
        CancellationToken cancellationToken);
}