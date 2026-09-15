using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Application.Admin;

public interface IAdminPaymentService
{
    Task<PagedResult<AdminPaymentTransactionResponse>> GetPaymentsAsync(
        int page,
        int pageSize,
        DateTime? startDate,
        DateTime? endDate,
        Guid? userId,
        PaymentPurpose? purpose,
        PaymentStatus? status,
        string? search,
        CancellationToken cancellationToken);

    Task<AdminPaymentTransactionResponse?> GetPaymentByIdAsync(
        Guid transactionId,
        CancellationToken cancellationToken);

    Task<AdminRevenueResponse> GetRevenueAsync(CancellationToken cancellationToken);

    Task<AdminRefundResponse> RecordExternalRefundAsync(
        Guid transactionId,
        Guid adminUserId,
        AdminRecordRefundRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken);


    Task<AdminResolvePaymentIssueResponse> ResolvePaymentIssueAsync(
        Guid transactionId,
        Guid adminUserId,
        AdminResolvePaymentIssueRequest request,
        CancellationToken cancellationToken);

    Task<List<AdminPaymentTimelineEvent>> GetPaymentTimelineAsync(
        Guid transactionId,
        CancellationToken cancellationToken);

    Task<AdminPaymentReceiptResponse?> GetPaymentReceiptAsync(
        Guid transactionId,
        Guid adminUserId,
        CancellationToken cancellationToken);

    Task<AdminTrainerPayoutResponse> GetTrainerPayoutsAsync(CancellationToken cancellationToken);

    Task ProcessTrainerPayoutAsync(
        Guid trainerId,
        Guid adminUserId,
        AdminProcessTrainerPayoutRequest request,
        CancellationToken cancellationToken);
}
