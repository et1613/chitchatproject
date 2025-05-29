using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using WebApplication1.Models.Notifications;
using WebApplication1.Models.Enums;
using WebApplication1.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text;

namespace WebApplication1.Services
{
    public interface INotificationService
    {
        // Temel Bildirim İşlemleri
        Task<Notification> CreateNotificationAsync(string userId, string content, NotificationType type, NotificationPriority priority = NotificationPriority.Normal);
        Task<bool> MarkAsReadAsync(string notificationId, string userId);
        Task<bool> MarkAllAsReadAsync(string userId);
        Task<bool> DeleteNotificationAsync(string notificationId, string userId);
        Task<Notification> GetNotificationAsync(string notificationId);
        Task<IEnumerable<Notification>> GetUserNotificationsAsync(string userId, int skip = 0, int take = 50);
        Task<int> GetUnreadNotificationCountAsync(string userId);

        // Bildirim Tercihleri
        Task<NotificationPreferences> GetUserPreferencesAsync(string userId);
        Task<bool> UpdateUserPreferencesAsync(string userId, NotificationPreferences preferences);
        Task<bool> EnableNotificationTypeAsync(string userId, NotificationType type);
        Task<bool> DisableNotificationTypeAsync(string userId, NotificationType type);
        Task<bool> SetNotificationChannelAsync(string userId, NotificationChannel channel, bool enabled);

        // Bildirim Şablonları
        Task<NotificationTemplate> CreateTemplateAsync(string name, string content, NotificationType type);
        Task<bool> UpdateTemplateAsync(string templateId, string content);
        Task<bool> DeleteTemplateAsync(string templateId);
        Task<NotificationTemplate> GetTemplateAsync(string templateId);
        Task<IEnumerable<NotificationTemplate>> GetAllTemplatesAsync();
        Task<Notification> SendNotificationFromTemplateAsync(string userId, string templateId, Dictionary<string, string> parameters);

        // Bildirim İstatistikleri
        Task<NotificationStats> GetUserNotificationStatsAsync(string userId);
        Task<NotificationStats> GetSystemNotificationStatsAsync();
        Task<Dictionary<NotificationType, int>> GetNotificationTypeDistributionAsync(string userId);
        Task<Dictionary<NotificationPriority, int>> GetNotificationPriorityDistributionAsync(string userId);

        // Bildirim Filtreleme ve Arama
        Task<IEnumerable<Notification>> SearchNotificationsAsync(string userId, string query);
        Task<IEnumerable<Notification>> FilterNotificationsByTypeAsync(string userId, NotificationType type);
        Task<IEnumerable<Notification>> FilterNotificationsByPriorityAsync(string userId, NotificationPriority priority);
        Task<IEnumerable<Notification>> GetNotificationsByDateRangeAsync(string userId, DateTime startDate, DateTime endDate);

        // Bildirim Gruplandırma
        Task<NotificationGroup> CreateNotificationGroupAsync(string name, List<string> userIds);
        Task<bool> AddUserToGroupAsync(string groupId, string userId);
        Task<bool> RemoveUserFromGroupAsync(string groupId, string userId);
        Task<bool> SendGroupNotificationAsync(string groupId, string content, NotificationType type);

        // Bildirim Zamanlama
        Task<ScheduledNotification> ScheduleNotificationAsync(string userId, string content, DateTime scheduledTime, NotificationType type);
        Task<bool> CancelScheduledNotificationAsync(string scheduledNotificationId);
        Task<IEnumerable<ScheduledNotification>> GetScheduledNotificationsAsync(string userId);
        Task<bool> UpdateScheduledNotificationAsync(string scheduledNotificationId, DateTime newScheduledTime);

        // Bildirim Doğrulama
        Task<bool> ValidateNotificationContentAsync(string content);
        Task<bool> ValidateNotificationTemplateAsync(string templateId);
        Task<bool> ValidateNotificationPreferencesAsync(NotificationPreferences preferences);

        // Bildirim Raporlama
        Task<NotificationReport> GenerateUserReportAsync(string userId, DateTime startDate, DateTime endDate);
        Task<NotificationReport> GenerateSystemReportAsync(DateTime startDate, DateTime endDate);
        Task<byte[]> ExportNotificationReportAsync(NotificationReport report, string format);

        Task NotifyUserAsync(string userId, string message);
    }

    public enum NotificationType
    {
        Message,
        FriendRequest,
        GroupInvite,
        SystemAlert,
        SecurityAlert,
        ProfileUpdate,
        ContentShare,
        EventReminder,
        Achievement,
        Custom
    }

    public enum NotificationPriority
    {
        Low,
        Normal,
        High,
        Urgent
    }

    public enum NotificationChannel
    {
        Email,
        Push,
        SMS,
        InApp,
        WebSocket
    }

    public class NotificationPreferences
    {
        public string UserId { get; set; }
        public Dictionary<NotificationType, bool> EnabledTypes { get; set; }
        public Dictionary<NotificationChannel, bool> EnabledChannels { get; set; }
        public bool EnableSound { get; set; }
        public bool EnableVibration { get; set; }
        public bool EnableDesktopNotifications { get; set; }
        public TimeSpan QuietHoursStart { get; set; }
        public TimeSpan QuietHoursEnd { get; set; }
        public List<string> BlockedSenders { get; set; }
    }

    public class NotificationTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Content { get; set; }
        public NotificationType Type { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class NotificationStats
    {
        public int TotalNotifications { get; set; }
        public int UnreadNotifications { get; set; }
        public int ReadNotifications { get; set; }
        public int DeletedNotifications { get; set; }
        public Dictionary<NotificationType, int> TypeDistribution { get; set; }
        public Dictionary<NotificationPriority, int> PriorityDistribution { get; set; }
        public DateTime LastNotificationDate { get; set; }
    }

    public class NotificationGroup
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> UserIds { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class ScheduledNotification
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string Content { get; set; }
        public NotificationType Type { get; set; }
        public DateTime ScheduledTime { get; set; }
        public bool IsRecurring { get; set; }
        public string RecurrencePattern { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class NotificationReport
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalNotifications { get; set; }
        public int DeliveredNotifications { get; set; }
        public int ReadNotifications { get; set; }
        public Dictionary<NotificationType, int> TypeDistribution { get; set; }
        public Dictionary<NotificationChannel, int> ChannelDistribution { get; set; }
        public double AverageDeliveryTime { get; set; }
        public double AverageReadTime { get; set; }
        public List<Notification> TopNotifications { get; set; }
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationService> _logger;
        private readonly IEmailService _emailService;
        private readonly ConnectionManager _connectionManager;

        public NotificationService(
            ApplicationDbContext context,
            ILogger<NotificationService> logger,
            IEmailService emailService,
            ConnectionManager connectionManager)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
            _connectionManager = connectionManager;
        }

        // Temel Bildirim İşlemleri
        public async Task<Notification> CreateNotificationAsync(string userId, string content, NotificationType type, NotificationPriority priority = NotificationPriority.Normal)
        {
            try
            {
                var notification = new Notification
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Content = content,
                    Type = type,
                    Priority = priority,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                // Bildirim tercihlerini kontrol et
                var preferences = await GetUserPreferencesAsync(userId);
                if (preferences.EnabledTypes.ContainsKey(type) && preferences.EnabledTypes[type])
                {
                    // Bildirimi ilgili kanallara gönder
                    await SendNotificationToChannelsAsync(notification, preferences);
                }

                return notification;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> MarkAsReadAsync(string notificationId, string userId)
        {
            try
            {
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

                if (notification == null)
                    return false;

                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as read", notificationId);
                throw;
            }
        }

        public async Task<bool> MarkAllAsReadAsync(string userId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .ToListAsync();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> DeleteNotificationAsync(string notificationId, string userId)
        {
            try
            {
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

                if (notification == null)
                    return false;

                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification {NotificationId}", notificationId);
                throw;
            }
        }

        public async Task<Notification> GetNotificationAsync(string notificationId)
        {
            try
            {
                return await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notification {NotificationId}", notificationId);
                throw;
            }
        }

        public async Task<IEnumerable<Notification>> GetUserNotificationsAsync(string userId, int skip = 0, int take = 50)
        {
            try
            {
                return await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notifications for user {UserId}", userId);
                throw;
            }
        }

        public async Task<int> GetUnreadNotificationCountAsync(string userId)
        {
            try
            {
                return await _context.Notifications
                    .CountAsync(n => n.UserId == userId && !n.IsRead);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notification count for user {UserId}", userId);
                throw;
            }
        }

        // Bildirim Tercihleri
        public async Task<NotificationPreferences> GetUserPreferencesAsync(string userId)
        {
            try
            {
                var preferences = await _context.NotificationPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (preferences == null)
                {
                    // Varsayılan tercihleri oluştur
                    preferences = new NotificationPreferences
                    {
                        UserId = userId,
                        EnabledTypes = new Dictionary<NotificationType, bool>
                        {
                            { NotificationType.Message, true },
                            { NotificationType.FriendRequest, true },
                            { NotificationType.GroupInvite, true },
                            { NotificationType.SystemAlert, true },
                            { NotificationType.SecurityAlert, true }
                        },
                        EnabledChannels = new Dictionary<NotificationChannel, bool>
                        {
                            { NotificationChannel.Email, true },
                            { NotificationChannel.Push, true },
                            { NotificationChannel.InApp, true }
                        },
                        EnableSound = true,
                        EnableVibration = true,
                        EnableDesktopNotifications = true,
                        QuietHoursStart = new TimeSpan(22, 0, 0),
                        QuietHoursEnd = new TimeSpan(8, 0, 0),
                        BlockedSenders = new List<string>()
                    };

                    _context.NotificationPreferences.Add(preferences);
                    await _context.SaveChangesAsync();
                }

                return preferences;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification preferences for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpdateUserPreferencesAsync(string userId, NotificationPreferences preferences)
        {
            try
            {
                var existingPreferences = await _context.NotificationPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (existingPreferences == null)
                {
                    preferences.UserId = userId;
                    _context.NotificationPreferences.Add(preferences);
                }
                else
                {
                    _context.Entry(existingPreferences).CurrentValues.SetValues(preferences);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification preferences for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> EnableNotificationTypeAsync(string userId, NotificationType type)
        {
            try
            {
                var preferences = await GetUserPreferencesAsync(userId);
                if (preferences.EnabledTypes.ContainsKey(type))
                {
                    preferences.EnabledTypes[type] = true;
                    await UpdateUserPreferencesAsync(userId, preferences);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling notification type {Type} for user {UserId}", type, userId);
                throw;
            }
        }

        public async Task<bool> DisableNotificationTypeAsync(string userId, NotificationType type)
        {
            try
            {
                var preferences = await GetUserPreferencesAsync(userId);
                if (preferences.EnabledTypes.ContainsKey(type))
                {
                    preferences.EnabledTypes[type] = false;
                    await UpdateUserPreferencesAsync(userId, preferences);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling notification type {Type} for user {UserId}", type, userId);
                throw;
            }
        }

        public async Task<bool> SetNotificationChannelAsync(string userId, NotificationChannel channel, bool enabled)
        {
            try
            {
                var preferences = await GetUserPreferencesAsync(userId);
                if (preferences.EnabledChannels.ContainsKey(channel))
                {
                    preferences.EnabledChannels[channel] = enabled;
                    await UpdateUserPreferencesAsync(userId, preferences);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting notification channel {Channel} for user {UserId}", channel, userId);
                throw;
            }
        }

        // Bildirim Şablonları
        public async Task<NotificationTemplate> CreateTemplateAsync(string name, string content, NotificationType type)
        {
            try
            {
                var template = new NotificationTemplate
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Content = content,
                    Type = type,
                    Parameters = new Dictionary<string, string>(),
                    CreatedAt = DateTime.UtcNow
                };

                _context.NotificationTemplates.Add(template);
                await _context.SaveChangesAsync();

                return template;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification template");
                throw;
            }
        }

        public async Task<bool> UpdateTemplateAsync(string templateId, string content)
        {
            try
            {
                var template = await _context.NotificationTemplates
                    .FirstOrDefaultAsync(t => t.Id == templateId);

                if (template == null)
                    return false;

                template.Content = content;
                template.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification template {TemplateId}", templateId);
                throw;
            }
        }

        public async Task<bool> DeleteTemplateAsync(string templateId)
        {
            try
            {
                var template = await _context.NotificationTemplates
                    .FirstOrDefaultAsync(t => t.Id == templateId);

                if (template == null)
                    return false;

                _context.NotificationTemplates.Remove(template);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification template {TemplateId}", templateId);
                throw;
            }
        }

        public async Task<NotificationTemplate> GetTemplateAsync(string templateId)
        {
            try
            {
                return await _context.NotificationTemplates
                    .FirstOrDefaultAsync(t => t.Id == templateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notification template {TemplateId}", templateId);
                throw;
            }
        }

        public async Task<IEnumerable<NotificationTemplate>> GetAllTemplatesAsync()
        {
            try
            {
                return await _context.NotificationTemplates
                    .OrderBy(t => t.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all notification templates");
                throw;
            }
        }

        public async Task<Notification> SendNotificationFromTemplateAsync(string userId, string templateId, Dictionary<string, string> parameters)
        {
            try
            {
                var template = await GetTemplateAsync(templateId);
                if (template == null)
                    throw new ArgumentException("Template not found");

                var content = template.Content;
                foreach (var param in parameters)
                {
                    content = content.Replace($"{{{param.Key}}}", param.Value);
                }

                return await CreateNotificationAsync(userId, content, template.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification from template {TemplateId} to user {UserId}", templateId, userId);
                throw;
            }
        }

        // Bildirim İstatistikleri
        public async Task<NotificationStats> GetUserNotificationStatsAsync(string userId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .ToListAsync();

                var stats = new NotificationStats
                {
                    TotalNotifications = notifications.Count,
                    UnreadNotifications = notifications.Count(n => !n.IsRead),
                    ReadNotifications = notifications.Count(n => n.IsRead),
                    DeletedNotifications = notifications.Count(n => n.IsDeleted),
                    TypeDistribution = notifications.GroupBy(n => n.Type)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    PriorityDistribution = notifications.GroupBy(n => n.Priority)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    LastNotificationDate = notifications.Max(n => n.CreatedAt)
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification stats for user {UserId}", userId);
                throw;
            }
        }

        public async Task<NotificationStats> GetSystemNotificationStatsAsync()
        {
            try
            {
                var notifications = await _context.Notifications.ToListAsync();

                var stats = new NotificationStats
                {
                    TotalNotifications = notifications.Count,
                    UnreadNotifications = notifications.Count(n => !n.IsRead),
                    ReadNotifications = notifications.Count(n => n.IsRead),
                    DeletedNotifications = notifications.Count(n => n.IsDeleted),
                    TypeDistribution = notifications.GroupBy(n => n.Type)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    PriorityDistribution = notifications.GroupBy(n => n.Priority)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    LastNotificationDate = notifications.Max(n => n.CreatedAt)
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system notification stats");
                throw;
            }
        }

        public async Task<Dictionary<NotificationType, int>> GetNotificationTypeDistributionAsync(string userId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .ToListAsync();

                return notifications.GroupBy(n => n.Type)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification type distribution for user {UserId}", userId);
                throw;
            }
        }

        public async Task<Dictionary<NotificationPriority, int>> GetNotificationPriorityDistributionAsync(string userId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .ToListAsync();

                return notifications.GroupBy(n => n.Priority)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification priority distribution for user {UserId}", userId);
                throw;
            }
        }

        // Bildirim Filtreleme ve Arama
        public async Task<IEnumerable<Notification>> SearchNotificationsAsync(string userId, string query)
        {
            try
            {
                return await _context.Notifications
                    .Where(n => n.UserId == userId && n.Content.Contains(query))
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching notifications for user {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<Notification>> FilterNotificationsByTypeAsync(string userId, NotificationType type)
        {
            try
            {
                return await _context.Notifications
                    .Where(n => n.UserId == userId && n.Type == type)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering notifications by type for user {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<Notification>> FilterNotificationsByPriorityAsync(string userId, NotificationPriority priority)
        {
            try
            {
                return await _context.Notifications
                    .Where(n => n.UserId == userId && n.Priority == priority)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering notifications by priority for user {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<Notification>> GetNotificationsByDateRangeAsync(string userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                return await _context.Notifications
                    .Where(n => n.UserId == userId && n.CreatedAt >= startDate && n.CreatedAt <= endDate)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications by date range for user {UserId}", userId);
                throw;
            }
        }

        // Bildirim Gruplandırma
        public async Task<NotificationGroup> CreateNotificationGroupAsync(string name, List<string> userIds)
        {
            try
            {
                var group = new NotificationGroup
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    UserIds = userIds,
                    CreatedAt = DateTime.UtcNow
                };

                _context.NotificationGroups.Add(group);
                await _context.SaveChangesAsync();

                return group;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification group");
                throw;
            }
        }

        public async Task<bool> AddUserToGroupAsync(string groupId, string userId)
        {
            try
            {
                var group = await _context.NotificationGroups
                    .FirstOrDefaultAsync(g => g.Id == groupId);

                if (group == null)
                    return false;

                if (!group.UserIds.Contains(userId))
                {
                    group.UserIds.Add(userId);
                    group.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user {UserId} to notification group {GroupId}", userId, groupId);
                throw;
            }
        }

        public async Task<bool> RemoveUserFromGroupAsync(string groupId, string userId)
        {
            try
            {
                var group = await _context.NotificationGroups
                    .FirstOrDefaultAsync(g => g.Id == groupId);

                if (group == null)
                    return false;

                if (group.UserIds.Contains(userId))
                {
                    group.UserIds.Remove(userId);
                    group.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user {UserId} from notification group {GroupId}", userId, groupId);
                throw;
            }
        }

        public async Task<bool> SendGroupNotificationAsync(string groupId, string content, NotificationType type)
        {
            try
            {
                var group = await _context.NotificationGroups
                    .FirstOrDefaultAsync(g => g.Id == groupId);

                if (group == null)
                    return false;

                foreach (var userId in group.UserIds)
                {
                    await CreateNotificationAsync(userId, content, type);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending group notification to group {GroupId}", groupId);
                throw;
            }
        }

        // Bildirim Zamanlama
        public async Task<ScheduledNotification> ScheduleNotificationAsync(string userId, string content, DateTime scheduledTime, NotificationType type)
        {
            try
            {
                var scheduledNotification = new ScheduledNotification
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Content = content,
                    Type = type,
                    ScheduledTime = scheduledTime,
                    IsRecurring = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ScheduledNotifications.Add(scheduledNotification);
                await _context.SaveChangesAsync();

                return scheduledNotification;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling notification for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> CancelScheduledNotificationAsync(string scheduledNotificationId)
        {
            try
            {
                var scheduledNotification = await _context.ScheduledNotifications
                    .FirstOrDefaultAsync(n => n.Id == scheduledNotificationId);

                if (scheduledNotification == null)
                    return false;

                _context.ScheduledNotifications.Remove(scheduledNotification);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling scheduled notification {NotificationId}", scheduledNotificationId);
                throw;
            }
        }

        public async Task<IEnumerable<ScheduledNotification>> GetScheduledNotificationsAsync(string userId)
        {
            try
            {
                return await _context.ScheduledNotifications
                    .Where(n => n.UserId == userId)
                    .OrderBy(n => n.ScheduledTime)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting scheduled notifications for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpdateScheduledNotificationAsync(string scheduledNotificationId, DateTime newScheduledTime)
        {
            try
            {
                var scheduledNotification = await _context.ScheduledNotifications
                    .FirstOrDefaultAsync(n => n.Id == scheduledNotificationId);

                if (scheduledNotification == null)
                    return false;

                scheduledNotification.ScheduledTime = newScheduledTime;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating scheduled notification {NotificationId}", scheduledNotificationId);
                throw;
            }
        }

        // Bildirim Doğrulama
        public async Task<bool> ValidateNotificationContentAsync(string content)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content))
                    return false;

                if (content.Length > 500) // Maksimum içerik uzunluğu
                    return false;

                // Zararlı içerik kontrolü
                var harmfulWords = new[] { "spam", "scam", "hack" }; // Örnek zararlı kelimeler
                if (harmfulWords.Any(word => content.ToLower().Contains(word)))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating notification content");
                throw;
            }
        }

        public async Task<bool> ValidateNotificationTemplateAsync(string templateId)
        {
            try
            {
                var template = await GetTemplateAsync(templateId);
                if (template == null)
                    return false;

                // Şablon içeriği doğrulama
                if (string.IsNullOrWhiteSpace(template.Content))
                    return false;

                // Parametre kontrolü
                var parameters = template.Content.Split(new[] { '{', '}' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(p => p.Contains(":"))
                    .Select(p => p.Split(':')[0].Trim())
                    .ToList();

                if (parameters.Any(p => !template.Parameters.ContainsKey(p)))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating notification template {TemplateId}", templateId);
                throw;
            }
        }

        public async Task<bool> ValidateNotificationPreferencesAsync(NotificationPreferences preferences)
        {
            try
            {
                if (preferences == null)
                    return false;

                if (string.IsNullOrWhiteSpace(preferences.UserId))
                    return false;

                if (preferences.EnabledTypes == null || !preferences.EnabledTypes.Any())
                    return false;

                if (preferences.EnabledChannels == null || !preferences.EnabledChannels.Any())
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating notification preferences");
                throw;
            }
        }

        // Bildirim Raporlama
        public async Task<NotificationReport> GenerateUserReportAsync(string userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId && n.CreatedAt >= startDate && n.CreatedAt <= endDate)
                    .ToListAsync();

                var report = new NotificationReport
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalNotifications = notifications.Count,
                    DeliveredNotifications = notifications.Count(n => n.DeliveredAt.HasValue),
                    ReadNotifications = notifications.Count(n => n.IsRead),
                    TypeDistribution = notifications.GroupBy(n => n.Type)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    ChannelDistribution = notifications.GroupBy(n => n.Channel)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    AverageDeliveryTime = notifications
                        .Where(n => n.DeliveredAt.HasValue)
                        .Average(n => (n.DeliveredAt.Value - n.CreatedAt).TotalSeconds),
                    AverageReadTime = notifications
                        .Where(n => n.IsRead && n.ReadAt.HasValue)
                        .Average(n => (n.ReadAt.Value - n.CreatedAt).TotalSeconds),
                    TopNotifications = notifications
                        .OrderByDescending(n => n.Priority)
                        .Take(10)
                        .ToList()
                };

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating notification report for user {UserId}", userId);
                throw;
            }
        }

        public async Task<NotificationReport> GenerateSystemReportAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.CreatedAt >= startDate && n.CreatedAt <= endDate)
                    .ToListAsync();

                var report = new NotificationReport
                {
                    Id = Guid.NewGuid().ToString(),
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalNotifications = notifications.Count,
                    DeliveredNotifications = notifications.Count(n => n.DeliveredAt.HasValue),
                    ReadNotifications = notifications.Count(n => n.IsRead),
                    TypeDistribution = notifications.GroupBy(n => n.Type)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    ChannelDistribution = notifications.GroupBy(n => n.Channel)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    AverageDeliveryTime = notifications
                        .Where(n => n.DeliveredAt.HasValue)
                        .Average(n => (n.DeliveredAt.Value - n.CreatedAt).TotalSeconds),
                    AverageReadTime = notifications
                        .Where(n => n.IsRead && n.ReadAt.HasValue)
                        .Average(n => (n.ReadAt.Value - n.CreatedAt).TotalSeconds),
                    TopNotifications = notifications
                        .OrderByDescending(n => n.Priority)
                        .Take(10)
                        .ToList()
                };

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating system notification report");
                throw;
            }
        }

        public async Task<byte[]> ExportNotificationReportAsync(NotificationReport report, string format)
        {
            try
            {
                // Rapor formatına göre dışa aktarma işlemi
                switch (format.ToLower())
                {
                    case "json":
                        return System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(report));
                    case "csv":
                        // CSV formatında dışa aktarma
                        var csv = new StringBuilder();
                        csv.AppendLine("Id,UserId,StartDate,EndDate,TotalNotifications,DeliveredNotifications,ReadNotifications");
                        csv.AppendLine($"{report.Id},{report.UserId},{report.StartDate},{report.EndDate},{report.TotalNotifications},{report.DeliveredNotifications},{report.ReadNotifications}");
                        return System.Text.Encoding.UTF8.GetBytes(csv.ToString());
                    case "pdf":
                        // PDF formatında dışa aktarma
                        // PDF oluşturma kodu buraya eklenecek
                        throw new NotImplementedException("PDF export not implemented yet");
                    default:
                        throw new ArgumentException("Unsupported export format");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting notification report");
                throw;
            }
        }

        // Yardımcı Metodlar
        private async Task SendNotificationToChannelsAsync(Notification notification, NotificationPreferences preferences)
        {
            try
            {
                foreach (var channel in preferences.EnabledChannels.Where(c => c.Value))
                {
                    switch (channel.Key)
                    {
                        case NotificationChannel.Email:
                            await _emailService.SendEmailAsync(
                                notification.UserId,
                                $"New {notification.Type} Notification",
                                notification.Content);
                            break;

                        case NotificationChannel.Push:
                            // Push notification gönderme kodu
                            break;

                        case NotificationChannel.SMS:
                            // SMS gönderme kodu
                            break;

                        case NotificationChannel.InApp:
                            // In-app notification gönderme kodu
                            break;

                        case NotificationChannel.WebSocket:
                            await _connectionManager.SendNotificationAsync(notification);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to channels for notification {NotificationId}", notification.Id);
                throw;
            }
        }

        public async Task NotifyUserAsync(string userId, string message)
        {
            try
            {
                var socket = _connectionManager.GetClient(userId);
                if (socket != null && socket.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    var notification = new
                    {
                        type = "notification",
                        message = message,
                        timestamp = DateTime.UtcNow
                    };

                    var notificationJson = System.Text.Json.JsonSerializer.Serialize(notification);
                    var notificationBytes = System.Text.Encoding.UTF8.GetBytes(notificationJson);

                    await socket.SendAsync(
                        new System.ArraySegment<byte>(notificationBytes),
                        System.Net.WebSockets.WebSocketMessageType.Text,
                        true,
                        System.Threading.CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user {UserId}", userId);
            }
        }
    }
} 