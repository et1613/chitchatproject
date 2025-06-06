using System;
using System.Threading.Tasks;
using WebApplication1.Data;
using WebApplication1.Models.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using WebApplication1.Models.Users;

namespace WebApplication1.Services
{
    public class UserNotificationPreferencesService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserNotificationPreferencesService> _logger;

        public UserNotificationPreferencesService(
            ApplicationDbContext context,
            ILogger<UserNotificationPreferencesService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<NotificationPreferences> GetUserPreferencesAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentNullException(nameof(userId), "User ID cannot be null or empty");
                }

                var preferences = await _context.NotificationPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (preferences == null)
                {
                    _logger.LogInformation("Creating default notification preferences for user {UserId}", userId);
                    preferences = new NotificationPreferences
                    {
                        UserId = userId,
                        User = new User { Id = userId }, 
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        LastModifiedBy = userId
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

        public async Task UpdateUserPreferencesAsync(string userId, NotificationPreferences preferences)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentNullException(nameof(userId), "User ID cannot be null or empty");
                }

                if (preferences == null)
                {
                    throw new ArgumentNullException(nameof(preferences), "Preferences cannot be null");
                }

                var existingPreferences = await _context.NotificationPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (existingPreferences == null)
                {
                    preferences.UserId = userId;
                    preferences.CreatedAt = DateTime.UtcNow;
                    preferences.UpdatedAt = DateTime.UtcNow;
                    preferences.LastModifiedBy = userId;
                    _context.NotificationPreferences.Add(preferences);
                }
                else
                {
                    preferences.UpdatedAt = DateTime.UtcNow;
                    preferences.LastModifiedBy = userId;
                    _context.Entry(existingPreferences).CurrentValues.SetValues(preferences);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated notification preferences for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification preferences for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> IsNotificationEnabledAsync(string userId, string notificationType)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentNullException(nameof(userId), "User ID cannot be null or empty");
                }

                if (string.IsNullOrEmpty(notificationType))
                {
                    throw new ArgumentNullException(nameof(notificationType), "Notification type cannot be null or empty");
                }

                var preferences = await GetUserPreferencesAsync(userId);

                if (!preferences.IsNotificationsEnabled)
                {
                    _logger.LogInformation("All notifications are disabled for user {UserId}", userId);
                    return false;
                }

                if (preferences.IsInQuietHours())
                {
                    _logger.LogInformation("User {UserId} is in quiet hours", userId);
                    return false;
                }

                var isEnabled = preferences.IsNotificationTypeEnabled(notificationType);
                _logger.LogInformation("Notification type {NotificationType} is {Status} for user {UserId}", 
                    notificationType, isEnabled ? "enabled" : "disabled", userId);
                return isEnabled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking notification status for user {UserId} and type {NotificationType}", 
                    userId, notificationType);
                return false; // Default to disabled on error for safety
            }
        }

        public async Task ResetToDefaultsAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentNullException(nameof(userId), "User ID cannot be null or empty");
                }

                var preferences = await GetUserPreferencesAsync(userId);
                
                // Temel Bildirim Ayarları
                preferences.IsNotificationsEnabled = true;
                preferences.NotificationFrequency = "immediate";
                preferences.DoNotDisturb = false;
                preferences.DoNotDisturbStart = null;
                preferences.DoNotDisturbEnd = null;

                // Bildirim Türleri
                preferences.FriendRequestNotifications = true;
                preferences.MessageNotifications = true;
                preferences.GroupNotifications = true;
                preferences.SystemNotifications = true;
                preferences.MentionNotifications = true;
                preferences.ReactionNotifications = true;
                preferences.StatusChangeNotifications = true;
                preferences.ProfileUpdateNotifications = true;
                preferences.SecurityNotifications = true;
                preferences.MarketingNotifications = false;
                preferences.NewsletterNotifications = false;
                preferences.EventNotifications = true;
                preferences.AnnouncementNotifications = true;

                // Web Bildirim Ayarları
                preferences.BrowserNotifications = true;
                preferences.SoundEnabled = true;
                preferences.NotificationSound = "default";
                preferences.NotificationVolume = 80;
                preferences.ShowPreview = true;

                // Bildirim Görünümü
                preferences.NotificationStyle = "default";
                preferences.ShowNotificationCount = true;
                preferences.ShowNotificationTime = true;
                preferences.ShowNotificationIcon = true;
                preferences.ShowNotificationBadge = true;
                preferences.ShowNotificationToast = true;

                // Bildirim Yönetimi
                preferences.NotificationRetentionDays = 30;
                preferences.AutoDeleteRead = false;
                preferences.AutoDeleteUnread = false;
                preferences.AutoDeleteDays = 90;
                preferences.ArchiveReadNotifications = true;
                preferences.MarkAsReadOnOpen = true;
                preferences.MarkAsReadOnReply = true;

                // Kullanıcı Tercihleri
                preferences.TimeZone = "UTC";
                preferences.Language = "en";
                preferences.Theme = "light";

                preferences.UpdatedAt = DateTime.UtcNow;
                preferences.LastModifiedBy = userId;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully reset notification preferences to defaults for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting notification preferences for user {UserId}", userId);
                throw;
            }
        }

        public async Task SetQuietHoursAsync(string userId, TimeSpan start, TimeSpan end)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentNullException(nameof(userId), "User ID cannot be null or empty");
                }

                if (start >= end)
                {
                    throw new ArgumentException("Start time must be before end time");
                }

                var preferences = await GetUserPreferencesAsync(userId);
                preferences.SetQuietHours(start, end);
                preferences.UpdatedAt = DateTime.UtcNow;
                preferences.LastModifiedBy = userId;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully set quiet hours for user {UserId}: {Start} to {End}", 
                    userId, start, end);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting quiet hours for user {UserId}", userId);
                throw;
            }
        }

        public async Task DisableQuietHoursAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentNullException(nameof(userId), "User ID cannot be null or empty");
                }

                var preferences = await GetUserPreferencesAsync(userId);
                preferences.DisableQuietHours();
                preferences.UpdatedAt = DateTime.UtcNow;
                preferences.LastModifiedBy = userId;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully disabled quiet hours for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling quiet hours for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> IsInQuietHoursAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentNullException(nameof(userId), "User ID cannot be null or empty");
                }

                var preferences = await GetUserPreferencesAsync(userId);
                var isInQuietHours = preferences.IsInQuietHours();
                
                _logger.LogInformation("User {UserId} is {Status} quiet hours", 
                    userId, isInQuietHours ? "in" : "not in");
                return isInQuietHours;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking quiet hours for user {UserId}", userId);
                return false;
            }
        }

        public async Task DeleteUserPreferencesAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentNullException(nameof(userId), "User ID cannot be null or empty");
                }

                var preferences = await _context.NotificationPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (preferences != null)
                {
                    _context.NotificationPreferences.Remove(preferences);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Successfully deleted notification preferences for user {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification preferences for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentNullException(nameof(userId), "User ID cannot be null or empty");
                }

                return await _context.NotificationPreferences
                    .AnyAsync(p => p.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if notification preferences exist for user {UserId}", userId);
                throw;
            }
        }
    }
} 