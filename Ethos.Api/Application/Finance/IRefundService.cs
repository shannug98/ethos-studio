using System;
using System.Threading;
using System.Threading.Tasks;
using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Application.Finance;

public class RefundResult
{
    public bool Success { get; set; }
    public Guid RefundId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid BookingId { get; set; }
    public long AmountPaise { get; set; }
    public RefundStatus Status { get; set; }
    public string? RazorpayRefundId { get; set; }
    public string? Message { get; set; }
    public string? FailureReason { get; set; }
}

public interface IRefundService
{
    Task<RefundResult> RefundPaymentAsync(
        Guid paymentId,
        string reason,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    Task<RefundResult> ProcessRefundJobAsync(
        Guid refundJobId,
        CancellationToken cancellationToken = default);
}
