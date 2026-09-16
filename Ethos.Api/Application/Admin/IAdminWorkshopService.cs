using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Application.Admin;

public interface IAdminWorkshopService
{
    Task<IReadOnlyList<TrainerWorkshopResponse>> GetPendingWorkshopsAsync(
        CancellationToken cancellationToken);

    Task<PagedResult<TrainerWorkshopResponse>> GetWorkshopsAsync(
        int page,
        int pageSize,
        string? phase,
        WorkshopStatus? status,
        Guid? trainerId,
        DateTime? startDate,
        DateTime? endDate,
        string? search,
        string? city,
        CancellationToken cancellationToken);

    Task<AdminWorkshopCountsDto> GetWorkshopCountsAsync(
        CancellationToken cancellationToken);

    Task<TrainerWorkshopResponse?> GetWorkshopByIdAsync(
        Guid workshopId,
        CancellationToken cancellationToken);

    Task<TrainerWorkshopResponse> CreateWorkshopAsync(
        Guid adminUserId,
        AdminCreateWorkshopRequest request,
        CancellationToken cancellationToken);

    Task<TrainerWorkshopResponse> UpdateWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        AdminUpdateWorkshopRequest request,
        CancellationToken cancellationToken);

    Task ApproveWorkshopPriceAsync(
        Guid workshopId,
        Guid adminUserId,
        AdminApproveWorkshopPriceRequest request,
        CancellationToken cancellationToken);

    Task RejectWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken);

    Task CancelWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken);

    Task CompleteWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        CancellationToken cancellationToken);

    Task<AdminWorkshopPricingTiersResponse> GetWorkshopPricingTiersAsync(
        Guid workshopId,
        CancellationToken cancellationToken);

    Task UpdateWorkshopPricingTiersAsync(
        Guid workshopId,
        Guid adminUserId,
        AdminUpdateWorkshopPricingTiersRequest request,
        CancellationToken cancellationToken);

    Task<PagedResult<AdminWorkshopRegistrationResponse>> GetWorkshopRegistrationsAsync(
        Guid workshopId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminWorkshopTicketResponse>> GetWorkshopTicketsAsync(
        Guid workshopId,
        CancellationToken cancellationToken);

    Task<bool> AdminCheckInOverrideAsync(
        Guid workshopId,
        Guid ticketId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken);

    Task<bool> AdminUndoCheckInAsync(
        Guid workshopId,
        Guid ticketId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken);

    Task<string> ExportWorkshopAttendanceCsvAsync(
        Guid workshopId,
        CancellationToken cancellationToken);

    Task PublishWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        CancellationToken cancellationToken);

    Task UnpublishWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        CancellationToken cancellationToken);

    Task ArchiveWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        CancellationToken cancellationToken);

    Task<AdminWorkshopOverviewResponse> GetWorkshopOverviewAsync(
        Guid workshopId,
        CancellationToken cancellationToken);

    Task<AdminCheckInTicketResponse> CheckInWorkshopTicketAsync(
        Guid workshopId,
        Guid adminUserId,
        AdminCheckInTicketRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminWorkshopAttendeeDto>> GetWorkshopAttendeesAsync(
        Guid workshopId,
        string? filter,
        string? search,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminWorkshopFeedbackDto>> GetWorkshopFeedbackAsync(
        Guid workshopId,
        CancellationToken cancellationToken);
}
