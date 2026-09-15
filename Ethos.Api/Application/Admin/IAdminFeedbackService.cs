using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Application.Admin;

public interface IAdminFeedbackService
{
    Task<PagedResult<AdminFeedbackResponse>> GetFeedbackAsync(
        int page,
        int pageSize,
        Guid? trainerId,
        Guid? studentId,
        Guid? workshopId,
        int? minRating,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken);

    Task<AdminFeedbackResponse?> GetFeedbackByIdAsync(
        Guid feedbackId,
        CancellationToken cancellationToken);
}
