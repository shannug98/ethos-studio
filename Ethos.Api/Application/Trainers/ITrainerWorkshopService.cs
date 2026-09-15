using Ethos.Api.Contracts.Trainers;

namespace Ethos.Api.Application.Trainers;

public interface ITrainerWorkshopService
{
    Task<IReadOnlyList<TrainerWorkshopResponse>> GetMyWorkshopsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<TrainerWorkshopResponse?> GetMyWorkshopByIdAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken cancellationToken);

    Task<TrainerWorkshopResponse> CreateWorkshopAsync(
        Guid userId,
        TrainerWorkshopRequest request,
        CancellationToken cancellationToken);

    Task<TrainerWorkshopResponse?> UpdateWorkshopAsync(
        Guid userId,
        Guid workshopId,
        TrainerWorkshopRequest request,
        CancellationToken cancellationToken);

    Task SubmitWorkshopAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken cancellationToken);

    Task CancelWorkshopAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerWorkshopStudentResponse>> GetWorkshopStudentsAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerWorkshopFeedbackResponse>> GetWorkshopFeedbackAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken cancellationToken);

    Task UpdateWorkshopStudentStatusAsync(
        Guid userId,
        Guid workshopId,
        Guid studentId,
        Domain.Enums.WorkshopBookingStatus status,
        CancellationToken cancellationToken);

    Task<TicketValidationResponse> ValidateTicketAsync(
        Guid userId,
        Guid workshopId,
        TicketValidationRequest request,
        CancellationToken cancellationToken);

    Task<TicketValidationResponse> CheckInTicketAsync(
        Guid userId,
        Guid workshopId,
        Guid ticketId,
        CheckInTicketRequest request,
        CancellationToken cancellationToken);

    Task<GroupCheckInResponse> GroupCheckInAsync(
        Guid userId,
        Guid workshopId,
        Guid bookingId,
        CancellationToken cancellationToken);

    Task<WorkshopAttendanceSummaryResponse> GetWorkshopAttendanceSummaryAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken cancellationToken);

    Task<bool> RecordReEntryAsync(
        Guid userId,
        Guid workshopId,
        Guid ticketId,
        Domain.Enums.AttendanceEventType eventType,
        CancellationToken cancellationToken);
}
