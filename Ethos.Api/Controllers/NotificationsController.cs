using Ethos.Api.Application.Notifications;
using Ethos.Api.Contracts.Notifications;
using Ethos.Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> GetMyNotifications(
        [FromQuery] NotificationType? type = null)
    {
        var response = await _notificationService.GetMyNotificationsAsync(type);
        return Ok(response);
    }

    [HttpGet("me/unread-count")]
    public async Task<ActionResult<object>> GetMyUnreadCount()
    {
        var count = await _notificationService.GetMyUnreadCountAsync();
        return Ok(new { unreadCount = count });
    }

    [HttpPatch("me/{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var success = await _notificationService.MarkAsReadAsync(id);

        if (!success)
        {
            return NotFound(new { message = "Notification not found." });
        }

        return Ok(new { message = "Notification marked as read." });
    }

    [HttpPatch("me/read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await _notificationService.MarkAllAsReadAsync();
        return Ok(new { message = "All notifications marked as read." });
    }

    [HttpDelete("me/{id:guid}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        try
        {
            var success = await _notificationService.DeleteNotificationAsync(id);
            if (!success)
            {
                return NotFound(new { message = "Notification not found." });
            }

            return Ok(new { message = "Notification deleted successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("reminders/process")]
    public async Task<IActionResult> ProcessReminders(
        [FromServices] IStudentNotificationReminderService reminderService)
    {
        var expiryCount = await reminderService.ProcessPackageExpiryRemindersAsync();
        var lowCreditCount = await reminderService.ProcessLowCreditRemindersAsync();
        var feedbackCount = await reminderService.ProcessFeedbackRemindersAsync();

        return Ok(new
        {
            expiryRemindersSent = expiryCount,
            lowCreditRemindersSent = lowCreditCount,
            feedbackRemindersSent = feedbackCount
        });
    }
}
