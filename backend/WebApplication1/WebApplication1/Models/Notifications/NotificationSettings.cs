using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebApplication1.Models.Enums;
using WebApplication1.Models.Users;

namespace WebApplication1.Models.Notifications
{
    public class NotificationSettings
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public required string UserId { get; set; }
        public required virtual User User { get; set; }

        public bool EnablePushNotifications { get; set; } = true;
        public bool EnableEmailNotifications { get; set; } = true;
        public bool EnableSound { get; set; } = true;
        public bool EnableVibration { get; set; } = true;

        public TimeSpan? QuietHoursStart { get; set; }
        public TimeSpan? QuietHoursEnd { get; set; }

        public Dictionary<NotificationType, bool> TypeSettings { get; set; } = new()
        {
            { NotificationType.MessageReceived, true },
            { NotificationType.MessageRead, true },
            { NotificationType.MessageEdited, true },
            { NotificationType.MessageDeleted, true },
            { NotificationType.MessageDeletedForYou, true },
            { NotificationType.MessageReplied, true },
            { NotificationType.MessageStatusChanged, true },
            { NotificationType.FriendRequest, true },
            { NotificationType.FriendRequestAccepted, true },
            { NotificationType.FriendRequestRejected, true },
            { NotificationType.UserMentioned, true },
            { NotificationType.ChatRoomCreated, true },
            { NotificationType.ChatRoomJoined, true },
            { NotificationType.ChatRoomLeft, true },
            { NotificationType.SystemMessage, true }
        };

        public Dictionary<NotificationPriority, bool> PrioritySettings { get; set; } = new()
        {
            { NotificationPriority.Low, true },
            { NotificationPriority.Normal, true },
            { NotificationPriority.High, true },
            { NotificationPriority.Urgent, true }
        };

        public bool IsNotificationEnabled(NotificationType type, NotificationPriority priority)
        {
            if (!EnablePushNotifications)
                return false;

            if (IsInQuietHours())
                return priority == NotificationPriority.Urgent;

            return TypeSettings.GetValueOrDefault(type, true) && 
                   PrioritySettings.GetValueOrDefault(priority, true);
        }

        public bool IsInQuietHours()
        {
            if (!QuietHoursStart.HasValue || !QuietHoursEnd.HasValue)
                return false;

            var now = DateTime.Now.TimeOfDay;
            return now >= QuietHoursStart.Value && now <= QuietHoursEnd.Value;
        }

        public void UpdateTypeSetting(NotificationType type, bool enabled)
        {
            TypeSettings[type] = enabled;
        }

        public void UpdatePrioritySetting(NotificationPriority priority, bool enabled)
        {
            PrioritySettings[priority] = enabled;
        }

        public void SetQuietHours(TimeSpan? start, TimeSpan? end)
        {
            QuietHoursStart = start;
            QuietHoursEnd = end;
        }
    }
} 