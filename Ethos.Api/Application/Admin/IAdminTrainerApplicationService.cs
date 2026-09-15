using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Application.Admin;

public interface IAdminTrainerApplicationService
{
    Task<IReadOnlyList<TrainerApplicationResponse>> GetAllApplicationsAsync(
        CancellationToken cancellationToken);

    Task<PagedResult<TrainerApplicationResponse>> GetApplicationsAsync(
        int page,
        int pageSize,
        TrainerApplicationStatus? status,
        CancellationToken cancellationToken);

    Task<TrainerApplicationResponse?> GetApplicationByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task ApproveApplicationAsync(
        Guid applicationId,
        Guid adminUserId,
        AdminApproveApplicationRequest request,
        CancellationToken cancellationToken);

    Task RejectApplicationAsync(
        Guid applicationId,
        Guid adminUserId,
        AdminRejectApplicationRequest request,
        CancellationToken cancellationToken);

    Task RequestChangesAsync(
        Guid applicationId,
        Guid adminUserId,
        AdminRequestChangesRequest request,
        CancellationToken cancellationToken);

    Task<(string PhysicalPath, string ContentType)?> GetApplicationVideoStreamAsync(
        Guid applicationId,
        CancellationToken cancellationToken);
}
