namespace Ethos.Api.Application.Notifications;

public interface IExternalNotificationSender
{
    Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default);

    Task SendWhatsAppAsync(string toPhone, string message, CancellationToken cancellationToken = default);

    Task SendSmsAsync(string toPhone, string message, CancellationToken cancellationToken = default);
}
