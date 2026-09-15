using Ethos.Api.Contracts.Workshops;

namespace Ethos.Api.Application.Workshops;

public interface IWorkshopService
{
    Task<IReadOnlyList<WorkshopResponse>> GetApprovedWorkshopsAsync();

    Task<WorkshopResponse?> GetWorkshopByIdAsync(Guid id);

    Task<WorkshopPricingResponse?> GetWorkshopPricingAsync(Guid id);

    Task<WorkshopPriceQuoteResponse> GetWorkshopQuoteAsync(
        Guid id,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<CreateWorkshopOrderResponse> CreateWorkshopOrderAsync(Guid workshopId, CreateWorkshopOrderRequest request, CancellationToken cancellationToken = default);

    Task<WorkshopBookingResponse> VerifyWorkshopPaymentAsync(Guid workshopId, VerifyWorkshopPaymentRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkshopBookingResponse>> GetMyBookingsAsync();

    Task<WorkshopBookingResponse> BookWorkshopAsync(Guid workshopId);

    Task<bool> CancelBookingAsync(Guid workshopId);

    Task<IReadOnlyList<WorkshopFeedbackResponse>> GetMyFeedbackAsync();

    Task<WorkshopFeedbackResponse> SubmitFeedbackAsync(SubmitFeedbackRequest request);

    Task<WorkshopFeedbackResponse?> UpdateFeedbackAsync(Guid feedbackId, SubmitFeedbackRequest request);
}
