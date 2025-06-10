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
        [Key, ForeignKey("User")]
        public required string UserId { get; set; }
        public required virtual User User { get; set; }

        public bool EmailNotifications { get; set; } = true;
        public bool PushNotifications { get; set; } = true;
        public bool InAppNotifications { get; set; } = true;
        public bool DesktopNotifications { get; set; } = true;
        public bool MobileNotifications { get; set; } = true;
        public bool SoundEnabled { get; set; } = true;
        public bool VibrationEnabled { get; set; } = true;
        public string NotificationSound { get; set; } = "default";
        public int NotificationVolume { get; set; } = 80;
        public bool DoNotDisturb { get; set; } = false;
        public TimeSpan? DoNotDisturbStart { get; set; }
        public TimeSpan? DoNotDisturbEnd { get; set; }
        public bool ShowPreview { get; set; } = true;
        public bool GroupNotifications { get; set; } = true;
        public int NotificationRetentionDays { get; set; } = 30;
        public bool AutoDeleteRead { get; set; } = false;
        public bool AutoDeleteUnread { get; set; } = false;
        public int AutoDeleteDays { get; set; } = 90;
        public string? CustomSettings { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? LastModifiedBy { get; set; }
        public bool IsActive { get; set; } = true;
        public string TimeZone { get; set; } = "UTC";
        public string Language { get; set; } = "en";
        public string Theme { get; set; } = "light";

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
            if (!PushNotifications)
                return false;

            if (IsInQuietHours())
                return priority == NotificationPriority.Urgent;

            return TypeSettings.GetValueOrDefault(type, true) && 
                   PrioritySettings.GetValueOrDefault(priority, true);
        }

        public bool IsInQuietHours()
        {
            if (!DoNotDisturbStart.HasValue || !DoNotDisturbEnd.HasValue)
                return false;

            var now = DateTime.Now.TimeOfDay;
            return now >= DoNotDisturbStart.Value && now <= DoNotDisturbEnd.Value;
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
            DoNotDisturbStart = start;
            DoNotDisturbEnd = end;
        }
    }
} 