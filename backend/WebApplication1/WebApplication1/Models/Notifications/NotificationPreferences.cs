using System;
using System.ComponentModel.DataAnnotations;
using WebApplication1.Models.Users;

namespace WebApplication1.Models.Notifications
{
    public class NotificationPreferences
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public required string UserId { get; set; }
        public required virtual User User { get; set; }

        // Temel Bildirim Ayarları
        public bool IsNotificationsEnabled { get; set; } = true;
        public string NotificationFrequency { get; set; } = "immediate"; // immediate, daily, weekly
        public bool DoNotDisturb { get; set; } = false;
        public TimeSpan? DoNotDisturbStart { get; set; }
        public TimeSpan? DoNotDisturbEnd { get; set; }

        // Bildirim Türleri
        public bool FriendRequestNotifications { get; set; } = true;
        public bool MessageNotifications { get; set; } = true;
        public bool GroupNotifications { get; set; } = true;
        public bool SystemNotifications { get; set; } = true;
        public bool MentionNotifications { get; set; } = true;
        public bool ReactionNotifications { get; set; } = true;
        public bool StatusChangeNotifications { get; set; } = true;
        public bool ProfileUpdateNotifications { get; set; } = true;
        public bool SecurityNotifications { get; set; } = true;
        public bool MarketingNotifications { get; set; } = false;
        public bool NewsletterNotifications { get; set; } = false;
        public bool EventNotifications { get; set; } = true;
        public bool AnnouncementNotifications { get; set; } = true;

        // Web Bildirim Ayarları
        public bool BrowserNotifications { get; set; } = true;
        public bool SoundEnabled { get; set; } = true;
        public string NotificationSound { get; set; } = "default";
        public int NotificationVolume { get; set; } = 80;
        public bool ShowPreview { get; set; } = true;

        // Bildirim Görünümü
        public string NotificationStyle { get; set; } = "default"; // default, compact, detailed
        public bool ShowNotificationCount { get; set; } = true;
        public bool ShowNotificationTime { get; set; } = true;
        public bool ShowNotificationIcon { get; set; } = true;
        public bool ShowNotificationBadge { get; set; } = true;
        public bool ShowNotificationToast { get; set; } = true;

        // Bildirim Yönetimi
        public int NotificationRetentionDays { get; set; } = 30;
        public bool AutoDeleteRead { get; set; } = false;
        public bool AutoDeleteUnread { get; set; } = false;
        public int AutoDeleteDays { get; set; } = 90;
        public bool ArchiveReadNotifications { get; set; } = true;
        public bool MarkAsReadOnOpen { get; set; } = true;
        public bool MarkAsReadOnReply { get; set; } = true;

        // Kullanıcı Tercihleri
        public string TimeZone { get; set; } = "UTC";
        public string Language { get; set; } = "en";
        public string Theme { get; set; } = "light";
        public string? CustomSettings { get; set; }

        // Sistem Bilgileri
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? LastModifiedBy { get; set; }
        public bool IsActive { get; set; } = true;
        public string Version { get; set; } = "1.0";

        // Yardımcı Metodlar
        public bool IsInQuietHours()
        {
            if (!DoNotDisturb || !DoNotDisturbStart.HasValue || !DoNotDisturbEnd.HasValue)
                return false;

            var now = DateTime.UtcNow.TimeOfDay;
            return now >= DoNotDisturbStart.Value && now <= DoNotDisturbEnd.Value;
        }

        public void SetQuietHours(TimeSpan start, TimeSpan end)
        {
            DoNotDisturb = true;
            DoNotDisturbStart = start;
            DoNotDisturbEnd = end;
        }

        public void DisableQuietHours()
        {
            DoNotDisturb = false;
            DoNotDisturbStart = null;
            DoNotDisturbEnd = null;
        }

        public bool IsNotificationTypeEnabled(string notificationType)
        {
            return notificationType switch
            {
                "FriendRequest" => FriendRequestNotifications,
                "Message" => MessageNotifications,
                "Group" => GroupNotifications,
                "System" => SystemNotifications,
                "Mention" => MentionNotifications,
                "Reaction" => ReactionNotifications,
                "StatusChange" => StatusChangeNotifications,
                "ProfileUpdate" => ProfileUpdateNotifications,
                "Security" => SecurityNotifications,
                "Marketing" => MarketingNotifications,
                "Newsletter" => NewsletterNotifications,
                "Event" => EventNotifications,
                "Announcement" => AnnouncementNotifications,
                _ => true
            };
        }
    }
} 