namespace Ethos.Api.Application.Notifications;

public interface IStudentNotificationReminderService
{
    Task<int> ProcessPackageExpiryRemindersAsync(CancellationToken cancellationToken = default);

    Task<int> ProcessLowCreditRemindersAsync(CancellationToken cancellationToken = default);

    Task<int> ProcessFeedbackRemindersAsync(CancellationToken cancellationToken = default);
}
