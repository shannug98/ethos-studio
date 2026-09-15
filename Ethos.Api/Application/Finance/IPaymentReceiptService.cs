using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Application.Finance;

public interface IPaymentReceiptService
{
    Task<AdminPaymentReceiptResponse?> GetPaymentReceiptAsync(
        Guid transactionId,
        Guid adminUserId,
        CancellationToken cancellationToken);
}