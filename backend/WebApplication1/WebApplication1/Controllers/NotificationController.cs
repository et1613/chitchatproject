using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using WebApplication1.Models.Notifications;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using WebApplication1.Models.Enums;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<NotificationController> _logger;

        public NotificationController(
            INotificationService notificationService,
            ILogger<NotificationController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var notification = await _notificationService.CreateNotificationAsync(
                    request.UserId,
                    request.Message,
                    Enum.Parse<NotificationType>(request.Type),
                    NotificationPriority.Normal);

                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification");
                return StatusCode(500, "Error sending notification");
            }
        }

        [HttpPost("broadcast")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BroadcastNotification([FromBody] BroadcastNotificationRequest request)
        {
            try
            {
                // Get all users and send notification to each
                var notification = await _notificationService.CreateNotificationAsync(
                    "system", // System user ID for broadcast
                    request.Message,
                    Enum.Parse<NotificationType>(request.Type),
                    NotificationPriority.High);

                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting notification");
                return StatusCode(500, "Error broadcasting notification");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool includeRead = false)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var skip = (page - 1) * pageSize;
                var notifications = await _notificationService.GetUserNotificationsAsync(userId, skip, pageSize);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications");
                return StatusCode(500, "Error getting notifications");
            }
        }

        [HttpGet("unread")]
        public async Task<IActionResult> GetUnreadNotifications()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var count = await _notificationService.GetUnreadNotificationCountAsync(userId);
                return Ok(new { UnreadCount = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notifications");
                return StatusCode(500, "Error getting unread notifications");
            }
        }

        [HttpPost("{notificationId}/read")]
        public async Task<IActionResult> MarkAsRead(string notificationId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _notificationService.MarkAsReadAsync(notificationId, userId);
                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read");
                return StatusCode(500, "Error marking notification as read");
            }
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _notificationService.MarkAllAsReadAsync(userId);
                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read");
                return StatusCode(500, "Error marking all notifications as read");
            }
        }

        [HttpDelete("{notificationId}")]
        public async Task<IActionResult> DeleteNotification(string notificationId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _notificationService.DeleteNotificationAsync(notificationId, userId);
                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification");
                return StatusCode(500, "Error deleting notification");
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteAllNotifications()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Get all notifications and delete them one by one
                var notifications = await _notificationService.GetUserNotificationsAsync(userId, 0, int.MaxValue);
                foreach (var notification in notifications)
                {
                    await _notificationService.DeleteNotificationAsync(notification.Id, userId);
                }

                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting all notifications");
                return StatusCode(500, "Error deleting all notifications");
            }
        }

        [HttpGet("preferences")]
        public async Task<IActionResult> GetNotificationPreferences()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var preferences = await _notificationService.GetUserPreferencesAsync(userId);
                return Ok(preferences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification preferences");
                return StatusCode(500, "Error getting notification preferences");
            }
        }

        [HttpPut("preferences")]
        public async Task<IActionResult> UpdateNotificationPreferences([FromBody] UpdateNotificationPreferencesRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var currentPreferences = await _notificationService.GetUserPreferencesAsync(userId);
                
                // Update notification types
                foreach (var type in request.NotificationTypes ?? new List<string>())
                {
                    if (Enum.TryParse<NotificationType>(type, out var notificationType))
                    {
                        await _notificationService.EnableNotificationTypeAsync(userId, notificationType);
                    }
                }

                // Update channels
                if (request.EmailEnabled)
                    await _notificationService.SetNotificationChannelAsync(userId, NotificationChannel.Email, true);
                if (request.PushEnabled)
                    await _notificationService.SetNotificationChannelAsync(userId, NotificationChannel.Push, true);
                if (request.InAppEnabled)
                    await _notificationService.SetNotificationChannelAsync(userId, NotificationChannel.InApp, true);

                var updatedPreferences = await _notificationService.GetUserPreferencesAsync(userId);
                return Ok(updatedPreferences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification preferences");
                return StatusCode(500, "Error updating notification preferences");
            }
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> SubscribeToNotifications([FromBody] SubscribeToNotificationsRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Enable push notifications for the user
                await _notificationService.SetNotificationChannelAsync(userId, NotificationChannel.Push, true);
                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to notifications");
                return StatusCode(500, "Error subscribing to notifications");
            }
        }

        [HttpPost("unsubscribe")]
        public async Task<IActionResult> UnsubscribeFromNotifications([FromBody] UnsubscribeFromNotificationsRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Disable push notifications for the user
                await _notificationService.SetNotificationChannelAsync(userId, NotificationChannel.Push, false);
                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from notifications");
                return StatusCode(500, "Error unsubscribing from notifications");
            }
        }
    }

    public class SendNotificationRequest
    {
        public required string UserId { get; set; }
        public required string Title { get; set; }
        public required string Message { get; set; }
        public string Type { get; set; } = "Info";
        public Dictionary<string, string>? Data { get; set; }
    }

    public class BroadcastNotificationRequest
    {
        public required string Title { get; set; }
        public required string Message { get; set; }
        public string Type { get; set; } = "Info";
        public Dictionary<string, string>? Data { get; set; }
    }

    public class UpdateNotificationPreferencesRequest
    {
        public bool EmailEnabled { get; set; } = true;
        public bool PushEnabled { get; set; } = true;
        public bool InAppEnabled { get; set; } = true;
        public List<string>? NotificationTypes { get; set; }
    }

    public class SubscribeToNotificationsRequest
    {
        public required string DeviceToken { get; set; }
        public required string Platform { get; set; }
    }

    public class UnsubscribeFromNotificationsRequest
    {
        public required string DeviceToken { get; set; }
    }
} 