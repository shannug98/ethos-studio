using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Application.Admin;

public interface IAdminBookingService
{
    Task<PagedResult<AdminClassEnrollmentResponse>> GetClassEnrollmentsAsync(
        int page,
        int pageSize,
        Guid? classId,
        Guid? studentId,
        int? status,
        string? search,
        CancellationToken cancellationToken);

    Task<PagedResult<AdminWorkshopRegistrationResponse>> GetWorkshopBookingsAsync(
        int page,
        int pageSize,
        Guid? workshopId,
        Guid? studentId,
        WorkshopBookingStatus? status,
        string? search,
        CancellationToken cancellationToken);

    Task CancelClassEnrollmentAsync(
        Guid enrollmentId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken);

    Task CancelWorkshopBookingAsync(
        Guid bookingId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken);

    Task<AdminClassEnrollmentResponse> ManualEnrollmentAsync(
        Guid adminUserId,
        AdminManualEnrollmentRequest request,
        CancellationToken cancellationToken);

    Task UpdateWorkshopBookingContactAsync(
        Guid bookingId,
        Guid adminUserId,
        string phone,
        string? email,
        string? name,
        CancellationToken cancellationToken);

    Task<string> ResendWhatsAppTicketAsync(
        Guid bookingId,
        Guid adminUserId,
        string? overridePhone,
        CancellationToken cancellationToken);
}