using Ethos.Api.Contracts.Feedback;

namespace Ethos.Api.Application.Feedback;

public interface IGuestWorkshopFeedbackService
{
    Task<GuestWorkshopFeedbackDetailsResponse> GetFeedbackDetailsByTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<bool> SubmitGuestFeedbackAsync(
        SubmitGuestWorkshopFeedbackRequest request,
        CancellationToken cancellationToken = default);

    Task<GenerateFeedbackTokenResponse> GenerateTokenForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task<TrainerAggregateFeedbackDto> GetTrainerAggregateFeedbackAsync(
        Guid trainerProfileId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminTrainerFeedbackDto>> GetAdminTrainerFeedbackListAsync(
        Guid trainerProfileId,
        CancellationToken cancellationToken = default);
}
