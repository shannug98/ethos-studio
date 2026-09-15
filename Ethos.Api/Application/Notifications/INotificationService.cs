using Ethos.Api.Contracts.Notifications;
using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Application.Notifications;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationResponse>> GetMyNotificationsAsync(NotificationType? type = null);

    Task<int> GetMyUnreadCountAsync();

    Task<bool> MarkAsReadAsync(Guid notificationId);

    Task MarkAllAsReadAsync();

    Task<bool> DeleteNotificationAsync(Guid notificationId);

    Task<NotificationResponse> SendNotificationAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default);
}
