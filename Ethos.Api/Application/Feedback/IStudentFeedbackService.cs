using Ethos.Api.Contracts.Feedback;

namespace Ethos.Api.Application.Feedback;

public interface IStudentFeedbackService
{
    Task<LearningActivitySummaryResponse> GetLearningActivitySummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentFeedbackResponse>> GetMyFeedbackAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingFeedbackItemResponse>> GetPendingFeedbackAsync(CancellationToken cancellationToken = default);

    Task<StudentFeedbackResponse> SubmitClassFeedbackAsync(
        Guid classId,
        SubmitClassFeedbackRequest request,
        CancellationToken cancellationToken = default);

    Task<StudentFeedbackResponse> SubmitWorkshopFeedbackAsync(
        Guid workshopId,
        SubmitWorkshopFeedbackRequest request,
        CancellationToken cancellationToken = default);
}
