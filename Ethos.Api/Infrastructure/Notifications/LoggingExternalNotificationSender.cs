using Ethos.Api.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Infrastructure.Notifications;

public class LoggingExternalNotificationSender : IExternalNotificationSender
{
    private readonly ILogger<LoggingExternalNotificationSender> _logger;

    public LoggingExternalNotificationSender(ILogger<LoggingExternalNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        var maskedEmail = MaskEmail(toEmail);
        _logger.LogInformation(
            "[ExternalNotification:Email] Recipient: {Recipient} | Subject: {Subject} | Status: Queued",
            maskedEmail,
            subject);

        return Task.CompletedTask;
    }

    public Task SendWhatsAppAsync(string toPhone, string message, CancellationToken cancellationToken = default)
    {
        var maskedPhone = MaskPhone(toPhone);
        _logger.LogInformation(
            "[ExternalNotification:WhatsApp] Recipient: {Recipient} | Channel: WhatsApp | Status: Queued",
            maskedPhone);

        return Task.CompletedTask;
    }

    public Task SendSmsAsync(string toPhone, string message, CancellationToken cancellationToken = default)
    {
        var maskedPhone = MaskPhone(toPhone);
        _logger.LogInformation(
            "[ExternalNotification:SMS] Recipient: {Recipient} | Channel: SMS | Status: Queued",
            maskedPhone);

        return Task.CompletedTask;
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "[EMPTY]";
        var parts = email.Trim().Split('@');
        if (parts.Length != 2) return "******";
        var local = parts[0];
        var domain = parts[1];
        if (local.Length <= 2) return $"{local[0]}*@{domain}";
        return $"{local[0]}***{local[^1]}@{domain}";
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "[EMPTY]";
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4) return "******";
        return $"{new string('*', digits.Length - 4)}{digits[^4..]}";
    }
}
