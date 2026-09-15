using Ethos.Api.Contracts.Payments;

namespace Ethos.Api.Application.Payments;

public interface IPaymentService
{
    Task<PaymentTransactionResponse> CreateOrderAsync(CreatePaymentOrderRequest request);

    Task<PaymentTransactionResponse> VerifyPaymentAsync(VerifyPaymentRequest request);

    Task<PaymentTransactionResponse> CreatePublicPackageOrderAsync(
        CreatePublicPackageOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentTransactionResponse> VerifyPublicPackagePaymentAsync(
        VerifyPublicPackagePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentTransactionResponse>> GetMyPaymentsAsync();

    Task<PaymentTransactionResponse?> GetPaymentByIdAsync(Guid id);
}
