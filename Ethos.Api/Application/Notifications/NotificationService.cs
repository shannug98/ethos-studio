using Ethos.Api.Contracts.Notifications;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Application.Notifications;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IExternalNotificationSender _externalSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IExternalNotificationSender externalSender,
        ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _externalSender = externalSender;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetMyNotificationsAsync(NotificationType? type = null)
    {
        var userId = _currentUser.UserId;

        var query = _dbContext.NotificationRecipients
            .Include(nr => nr.Notification)
            .Where(nr => nr.UserId == userId && nr.DeletedAt == null);

        if (type.HasValue)
        {
            query = query.Where(nr => nr.Notification.Type == type.Value);
        }

        return await query
            .OrderByDescending(nr => nr.CreatedAt)
            .Select(nr => new NotificationResponse
            {
                Id = nr.NotificationId,
                Type = nr.Notification.Type,
                Title = nr.Notification.Title,
                Message = nr.Notification.Message,
                Channel = nr.Notification.Channel,
                ActionUrl = nr.Notification.ActionUrl,
                EventKey = nr.Notification.EventKey,
                IsRead = nr.IsRead,
                ReadAt = nr.ReadAt,
                CreatedAt = nr.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<int> GetMyUnreadCountAsync()
    {
        var userId = _currentUser.UserId;

        return await _dbContext.NotificationRecipients
            .CountAsync(nr => nr.UserId == userId && !nr.IsRead && nr.DeletedAt == null);
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId)
    {
        var userId = _currentUser.UserId;

        var recipient = await _dbContext.NotificationRecipients
            .FirstOrDefaultAsync(nr => nr.NotificationId == notificationId && nr.UserId == userId && nr.DeletedAt == null);

        if (recipient == null)
        {
            return false;
        }

        if (!recipient.IsRead)
        {
            recipient.IsRead = true;
            recipient.ReadAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }

        return true;
    }

    public async Task MarkAllAsReadAsync()
    {
        var userId = _currentUser.UserId;

        var unreadRecipients = await _dbContext.NotificationRecipients
            .Where(nr => nr.UserId == userId && !nr.IsRead && nr.DeletedAt == null)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var recipient in unreadRecipients)
        {
            recipient.IsRead = true;
            recipient.ReadAt = now;
        }

        if (unreadRecipients.Count > 0)
        {
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<bool> DeleteNotificationAsync(Guid notificationId)
    {
        var userId = _currentUser.UserId;

        var recipient = await _dbContext.NotificationRecipients
            .FirstOrDefaultAsync(nr => nr.NotificationId == notificationId && nr.UserId == userId && nr.DeletedAt == null);

        if (recipient == null)
        {
            return false;
        }

        // Soft delete: set DeletedAt timestamp so record is retained for auditability
        recipient.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<NotificationResponse> SendNotificationAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.UserId == Guid.Empty)
        {
            throw new ArgumentException("A valid target UserId is required for notification creation.", nameof(request));
        }

        // 1. Deduplication check via deterministic EventKey
        if (!string.IsNullOrWhiteSpace(request.EventKey))
        {
            var existing = await _dbContext.Notifications
                .Include(n => n.Recipients)
                .FirstOrDefaultAsync(n => n.EventKey == request.EventKey, cancellationToken);

            if (existing != null)
            {
                _logger.LogInformation(
                    "Notification with EventKey '{EventKey}' already exists. Skipping duplicate emission.",
                    request.EventKey);

                var existingRecipient = existing.Recipients.FirstOrDefault(r => r.UserId == request.UserId);
                return MapToResponse(existing, existingRecipient);
            }
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            Title = request.Title ?? string.Empty,
            Message = request.Message ?? string.Empty,
            Channel = request.Channel,
            ActionUrl = request.ActionUrl,
            EventKey = request.EventKey,
            CreatedAt = DateTime.UtcNow
        };

        var recipient = new NotificationRecipient
        {
            Id = Guid.NewGuid(),
            NotificationId = notification.Id,
            UserId = request.UserId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _dbContext.Notifications.Add(notification);
            _dbContext.NotificationRecipients.Add(recipient);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique index on EventKey caught concurrent race condition safely
            _logger.LogWarning(
                "Concurrent duplicate notification caught for EventKey '{EventKey}'. Loading existing.",
                request.EventKey);

            var existing = await _dbContext.Notifications
                .AsNoTracking()
                .Include(n => n.Recipients)
                .FirstOrDefaultAsync(n => n.EventKey == request.EventKey, cancellationToken);

            if (existing != null)
            {
                var existingRecipient = existing.Recipients.FirstOrDefault(r => r.UserId == request.UserId);
                return MapToResponse(existing, existingRecipient);
            }

            throw;
        }

        // 2. Best-effort external dispatch (Email, WhatsApp, SMS)
        if (request.SendExternal)
        {
            DispatchExternalNotification(request, notification);
        }

        return MapToResponse(notification, recipient);
    }

    /// <summary>
    /// Dispatches external communications (Email / WhatsApp / SMS) asynchronously.
    /// ARCHITECTURE NOTE:
    /// In the current phase, external dispatch is performed asynchronously via Task.Run to ensure that
    /// external provider network latency, timeouts, or API outages NEVER block or roll back the committed
    /// database transaction or student API response.
    /// 
    /// PRODUCTION EVOLUTION PATH:
    /// For guaranteed at-least-once delivery surviving process restarts, this will evolve into a
    /// Transactional Outbox pattern: persisting pending outbound messages in an Outbox table within the
    /// same DB transaction, polled and retried by a resilient background worker (e.g. Hangfire / IHostedService).
    /// </summary>
    private void DispatchExternalNotification(CreateNotificationRequest request, Notification notification)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var cleanEmail = request.RecipientEmail?
                    .Replace("\r", "")
                    .Replace("\n", "")
                    .Trim();

                var cleanPhone = request.RecipientPhone?
                    .Replace("\r", "")
                    .Replace("\n", "")
                    .Trim();

                if (!string.IsNullOrWhiteSpace(cleanEmail))
                {
                    await _externalSender.SendEmailAsync(
                        cleanEmail,
                        notification.Title,
                        notification.Message);
                }

                if (!string.IsNullOrWhiteSpace(cleanPhone))
                {
                    await _externalSender.SendWhatsAppAsync(
                        cleanPhone,
                        $"*{notification.Title}*\n{notification.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to dispatch external communication for notification '{NotificationId}'",
                    notification.Id);
            }
        });
    }

    private static NotificationResponse MapToResponse(Notification notification, NotificationRecipient? recipient)
    {
        return new NotificationResponse
        {
            Id = notification.Id,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            Channel = notification.Channel,
            ActionUrl = notification.ActionUrl,
            EventKey = notification.EventKey,
            IsRead = recipient?.IsRead ?? false,
            ReadAt = recipient?.ReadAt,
            CreatedAt = recipient?.CreatedAt ?? notification.CreatedAt
        };
    }
}
