using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;

namespace Ethos.Api.Application.Payments;

public record WebhookFulfillmentResult(
    bool Success,
    string Status,
    string Message,
    Guid? TransactionId = null);

public interface IPaymentFulfillmentService
{
    Task<WorkshopBookingResponse> FulfillWorkshopPaymentAsync(
        PaymentTransaction transaction,
        string razorpayPaymentId,
        string? razorpaySignature,
        string source,
        string? eventId = null,
        CancellationToken cancellationToken = default);

    Task FulfillNonWorkshopPaymentAsync(
        PaymentTransaction transaction,
        string razorpayPaymentId,
        string source,
        string? eventId = null,
        CancellationToken cancellationToken = default);

    Task<WebhookFulfillmentResult> ProcessWebhookEventAsync(
        string rawBody,
        string? signatureHeader,
        CancellationToken cancellationToken = default);
}
