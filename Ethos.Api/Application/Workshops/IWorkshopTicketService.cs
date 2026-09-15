using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;

namespace Ethos.Api.Application.Workshops;

public interface IWorkshopTicketService
{
    Task<IReadOnlyList<WorkshopTicketResponse>> GetTicketsForBookingAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<WorkshopTicketResponse> GetTicketPassAsync(
        Guid ticketId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<WorkshopTicketResponse> UpdateAttendeeDetailsAsync(
        Guid bookingId,
        Guid ticketId,
        Guid userId,
        UpdateAttendeeDetailsRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> ResendTicketPassAsync(
        Guid bookingId,
        Guid ticketId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<List<WorkshopTicketResponse>> IssueTicketsForBookingAsync(
        WorkshopBooking booking,
        PaymentTransaction transaction,
        User? user,
        CancellationToken cancellationToken = default);

    string DeriveQrToken(WorkshopTicket ticket);

    string ComputeTokenHash(string rawToken);
}
