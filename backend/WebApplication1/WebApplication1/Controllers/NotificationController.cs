using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using WebApplication1.Models.Notifications;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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

        // Temel Bildirim İşlemleri
        [HttpPost]
        public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationRequest request)
        {
            try
            {
                var notification = await _notificationService.CreateNotificationAsync(
                    userId: request.UserId,
                    content: request.Content,
                    type: request.Type,
                    priority: request.Priority
                );

                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification");
                return StatusCode(500, "Error creating notification");
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

        [HttpGet("{notificationId}")]
        public async Task<IActionResult> GetNotification(string notificationId)
        {
            try
            {
                var notification = await _notificationService.GetNotificationAsync(notificationId);
                if (notification == null)
                    return NotFound();

                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notification");
                return StatusCode(500, "Error retrieving notification");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserNotifications([FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var notifications = await _notificationService.GetUserNotificationsAsync(userId, skip, take);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user notifications");
                return StatusCode(500, "Error retrieving notifications");
            }
        }

        [HttpGet("unread/count")]
        public async Task<IActionResult> GetUnreadNotificationCount()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var count = await _notificationService.GetUnreadNotificationCountAsync(userId);
                return Ok(new { Count = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notification count");
                return StatusCode(500, "Error getting unread notification count");
            }
        }

        // Bildirim Tercihleri
        [HttpGet("preferences")]
        public async Task<IActionResult> GetUserPreferences()
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
                _logger.LogError(ex, "Error retrieving user preferences");
                return StatusCode(500, "Error retrieving preferences");
            }
        }

        [HttpPut("preferences")]
        public async Task<IActionResult> UpdateUserPreferences([FromBody] NotificationPreferences preferences)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _notificationService.UpdateUserPreferencesAsync(userId, preferences);
                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user preferences");
                return StatusCode(500, "Error updating preferences");
            }
        }

        [HttpPost("preferences/type/{type}/enable")]
        public async Task<IActionResult> EnableNotificationType(NotificationType type)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _notificationService.EnableNotificationTypeAsync(userId, type);
                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling notification type");
                return StatusCode(500, "Error enabling notification type");
            }
        }

        [HttpPost("preferences/type/{type}/disable")]
        public async Task<IActionResult> DisableNotificationType(NotificationType type)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _notificationService.DisableNotificationTypeAsync(userId, type);
                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling notification type");
                return StatusCode(500, "Error disabling notification type");
            }
        }

        [HttpPost("preferences/channel/{channel}")]
        public async Task<IActionResult> SetNotificationChannel(NotificationChannel channel, [FromBody] SetChannelRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _notificationService.SetNotificationChannelAsync(userId, channel, request.Enabled);
                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting notification channel");
                return StatusCode(500, "Error setting notification channel");
            }
        }

        // Bildirim Şablonları
        [HttpPost("templates")]
        public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest request)
        {
            try
            {
                var template = await _notificationService.CreateTemplateAsync(
                    name: request.Name,
                    content: request.Content,
                    type: request.Type
                );

                return Ok(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification template");
                return StatusCode(500, "Error creating template");
            }
        }

        [HttpPut("templates/{templateId}")]
        public async Task<IActionResult> UpdateTemplate(string templateId, [FromBody] UpdateTemplateRequest request)
        {
            try
            {
                var result = await _notificationService.UpdateTemplateAsync(templateId, request.Content);
                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification template");
                return StatusCode(500, "Error updating template");
            }
        }

        [HttpDelete("templates/{templateId}")]
        public async Task<IActionResult> DeleteTemplate(string templateId)
        {
            try
            {
                var result = await _notificationService.DeleteTemplateAsync(templateId);
                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification template");
                return StatusCode(500, "Error deleting template");
            }
        }

        [HttpGet("templates/{templateId}")]
        public async Task<IActionResult> GetTemplate(string templateId)
        {
            try
            {
                var template = await _notificationService.GetTemplateAsync(templateId);
                if (template == null)
                    return NotFound();

                return Ok(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notification template");
                return StatusCode(500, "Error retrieving template");
            }
        }

        [HttpGet("templates")]
        public async Task<IActionResult> GetAllTemplates()
        {
            try
            {
                var templates = await _notificationService.GetAllTemplatesAsync();
                return Ok(templates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all notification templates");
                return StatusCode(500, "Error retrieving templates");
            }
        }

        [HttpPost("templates/{templateId}/send")]
        public async Task<IActionResult> SendNotificationFromTemplate(string templateId, [FromBody] SendTemplateRequest request)
        {
            try
            {
                var notification = await _notificationService.SendNotificationFromTemplateAsync(
                    userId: request.UserId,
                    templateId: templateId,
                    parameters: request.Parameters
                );

                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification from template");
                return StatusCode(500, "Error sending notification");
            }
        }

        // Bildirim İstatistikleri
        [HttpGet("stats/user")]
        public async Task<IActionResult> GetUserNotificationStats()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var stats = await _notificationService.GetUserNotificationStatsAsync(userId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user notification stats");
                return StatusCode(500, "Error getting stats");
            }
        }

        [HttpGet("stats/system")]
        public async Task<IActionResult> GetSystemNotificationStats()
        {
            try
            {
                var stats = await _notificationService.GetSystemNotificationStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system notification stats");
                return StatusCode(500, "Error getting stats");
            }
        }

        [HttpGet("stats/type-distribution")]
        public async Task<IActionResult> GetNotificationTypeDistribution()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var distribution = await _notificationService.GetNotificationTypeDistributionAsync(userId);
                return Ok(distribution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification type distribution");
                return StatusCode(500, "Error getting distribution");
            }
        }

        [HttpGet("stats/priority-distribution")]
        public async Task<IActionResult> GetNotificationPriorityDistribution()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var distribution = await _notificationService.GetNotificationPriorityDistributionAsync(userId);
                return Ok(distribution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification priority distribution");
                return StatusCode(500, "Error getting distribution");
            }
        }

        // Bildirim Filtreleme ve Arama
        [HttpGet("search")]
        public async Task<IActionResult> SearchNotifications([FromQuery] string query)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var notifications = await _notificationService.SearchNotificationsAsync(userId, query);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching notifications");
                return StatusCode(500, "Error searching notifications");
            }
        }

        [HttpGet("filter/type/{type}")]
        public async Task<IActionResult> FilterNotificationsByType(NotificationType type)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var notifications = await _notificationService.FilterNotificationsByTypeAsync(userId, type);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering notifications by type");
                return StatusCode(500, "Error filtering notifications");
            }
        }

        [HttpGet("filter/priority/{priority}")]
        public async Task<IActionResult> FilterNotificationsByPriority(NotificationPriority priority)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var notifications = await _notificationService.FilterNotificationsByPriorityAsync(userId, priority);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering notifications by priority");
                return StatusCode(500, "Error filtering notifications");
            }
        }

        [HttpGet("filter/date-range")]
        public async Task<IActionResult> GetNotificationsByDateRange([FromQuery] DateRangeRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var notifications = await _notificationService.GetNotificationsByDateRangeAsync(
                    userId: userId,
                    startDate: request.StartDate,
                    endDate: request.EndDate
                );

                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications by date range");
                return StatusCode(500, "Error getting notifications");
            }
        }

        // Bildirim Gruplandırma
        [HttpPost("groups")]
        public async Task<IActionResult> CreateNotificationGroup([FromBody] CreateGroupRequest request)
        {
            try
            {
                var group = await _notificationService.CreateNotificationGroupAsync(
                    name: request.Name,
                    userIds: request.UserIds
                );

                return Ok(group);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification group");
                return StatusCode(500, "Error creating group");
            }
        }

        [HttpPost("groups/{groupId}/users/{userId}")]
        public async Task<IActionResult> AddUserToGroup(string groupId, string userId)
        {
            try
            {
                var result = await _notificationService.AddUserToGroupAsync(groupId, userId);
                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user to notification group");
                return StatusCode(500, "Error adding user to group");
            }
        }

        [HttpDelete("groups/{groupId}/users/{userId}")]
        public async Task<IActionResult> RemoveUserFromGroup(string groupId, string userId)
        {
            try
            {
                var result = await _notificationService.RemoveUserFromGroupAsync(groupId, userId);
                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user from notification group");
                return StatusCode(500, "Error removing user from group");
            }
        }

        [HttpPost("groups/{groupId}/notify")]
        public async Task<IActionResult> SendGroupNotification(string groupId, [FromBody] SendGroupNotificationRequest request)
        {
            try
            {
                var result = await _notificationService.SendGroupNotificationAsync(
                    groupId: groupId,
                    content: request.Content,
                    type: request.Type
                );

                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending group notification");
                return StatusCode(500, "Error sending group notification");
            }
        }

        // Bildirim Zamanlama
        [HttpPost("schedule")]
        public async Task<IActionResult> ScheduleNotification([FromBody] ScheduleNotificationRequest request)
        {
            try
            {
                var scheduledNotification = await _notificationService.ScheduleNotificationAsync(
                    userId: request.UserId,
                    content: request.Content,
                    scheduledTime: request.ScheduledTime,
                    type: request.Type
                );

                return Ok(scheduledNotification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling notification");
                return StatusCode(500, "Error scheduling notification");
            }
        }

        [HttpDelete("schedule/{scheduledNotificationId}")]
        public async Task<IActionResult> CancelScheduledNotification(string scheduledNotificationId)
        {
            try
            {
                var result = await _notificationService.CancelScheduledNotificationAsync(scheduledNotificationId);
                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling scheduled notification");
                return StatusCode(500, "Error canceling scheduled notification");
            }
        }

        [HttpGet("schedule")]
        public async Task<IActionResult> GetScheduledNotifications()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var notifications = await _notificationService.GetScheduledNotificationsAsync(userId);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting scheduled notifications");
                return StatusCode(500, "Error getting scheduled notifications");
            }
        }

        [HttpPut("schedule/{scheduledNotificationId}")]
        public async Task<IActionResult> UpdateScheduledNotification(string scheduledNotificationId, [FromBody] UpdateScheduledNotificationRequest request)
        {
            try
            {
                var result = await _notificationService.UpdateScheduledNotificationAsync(
                    scheduledNotificationId: scheduledNotificationId,
                    newScheduledTime: request.NewScheduledTime
                );

                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating scheduled notification");
                return StatusCode(500, "Error updating scheduled notification");
            }
        }

        // Bildirim Doğrulama
        [HttpPost("validate/content")]
        public async Task<IActionResult> ValidateNotificationContent([FromBody] ValidateContentRequest request)
        {
            try
            {
                var isValid = await _notificationService.ValidateNotificationContentAsync(request.Content);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating notification content");
                return StatusCode(500, "Error validating content");
            }
        }

        [HttpPost("validate/template/{templateId}")]
        public async Task<IActionResult> ValidateNotificationTemplate(string templateId)
        {
            try
            {
                var isValid = await _notificationService.ValidateNotificationTemplateAsync(templateId);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating notification template");
                return StatusCode(500, "Error validating template");
            }
        }

        [HttpPost("validate/preferences")]
        public async Task<IActionResult> ValidateNotificationPreferences([FromBody] NotificationPreferences preferences)
        {
            try
            {
                var isValid = await _notificationService.ValidateNotificationPreferencesAsync(preferences);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating notification preferences");
                return StatusCode(500, "Error validating preferences");
            }
        }

        // Bildirim Raporlama
        [HttpGet("reports/user")]
        public async Task<IActionResult> GenerateUserReport([FromQuery] GenerateReportRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var report = await _notificationService.GenerateUserReportAsync(
                    userId: userId,
                    startDate: request.StartDate,
                    endDate: request.EndDate
                );

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating user notification report");
                return StatusCode(500, "Error generating report");
            }
        }

        [HttpGet("reports/system")]
        public async Task<IActionResult> GenerateSystemReport([FromQuery] GenerateReportRequest request)
        {
            try
            {
                var report = await _notificationService.GenerateSystemReportAsync(
                    startDate: request.StartDate,
                    endDate: request.EndDate
                );

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating system notification report");
                return StatusCode(500, "Error generating report");
            }
        }

        [HttpGet("reports/export")]
        public async Task<IActionResult> ExportNotificationReport([FromQuery] ExportReportRequest request)
        {
            try
            {
                var report = await _notificationService.GenerateUserReportAsync(
                    userId: request.UserId,
                    startDate: request.StartDate,
                    endDate: request.EndDate
                );

                var data = await _notificationService.ExportNotificationReportAsync(report, request.Format);
                return File(data, GetContentType(request.Format), $"notification_report_{DateTime.UtcNow:yyyyMMddHHmmss}.{GetFileExtension(request.Format)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting notification report");
                return StatusCode(500, "Error exporting report");
            }
        }

        private string GetContentType(string format)
        {
            return format.ToLower() switch
            {
                "json" => "application/json",
                "csv" => "text/csv",
                "pdf" => "application/pdf",
                _ => "application/octet-stream"
            };
        }

        private string GetFileExtension(string format)
        {
            return format.ToLower() switch
            {
                "json" => "json",
                "csv" => "csv",
                "pdf" => "pdf",
                _ => "bin"
            };
        }
    }

    public class CreateNotificationRequest
    {
        [Required]
        public string UserId { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        public NotificationType Type { get; set; }

        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    }

    public class SetChannelRequest
    {
        [Required]
        public bool Enabled { get; set; }
    }

    public class CreateTemplateRequest
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        public NotificationType Type { get; set; }
    }

    public class UpdateTemplateRequest
    {
        [Required]
        public string Content { get; set; }
    }

    public class SendTemplateRequest
    {
        [Required]
        public string UserId { get; set; }

        public Dictionary<string, string> Parameters { get; set; }
    }

    public class DateRangeRequest
    {
        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }
    }

    public class CreateGroupRequest
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public List<string> UserIds { get; set; }
    }

    public class SendGroupNotificationRequest
    {
        [Required]
        public string Content { get; set; }

        [Required]
        public NotificationType Type { get; set; }
    }

    public class ScheduleNotificationRequest
    {
        [Required]
        public string UserId { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        public DateTime ScheduledTime { get; set; }

        [Required]
        public NotificationType Type { get; set; }
    }

    public class UpdateScheduledNotificationRequest
    {
        [Required]
        public DateTime NewScheduledTime { get; set; }
    }

    public class ValidateContentRequest
    {
        [Required]
        public string Content { get; set; }
    }

    public class GenerateReportRequest
    {
        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }
    }

    public class ExportReportRequest : GenerateReportRequest
    {
        [Required]
        public string UserId { get; set; }

        [Required]
        public string Format { get; set; }
    }
} 