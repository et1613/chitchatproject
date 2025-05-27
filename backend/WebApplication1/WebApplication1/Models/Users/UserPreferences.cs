using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.Users
{
    public class UserPreferences
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string UserId { get; set; }

        public string MessageSound { get; set; } = "default";

        public string NotificationSound { get; set; } = "default";

        public string Ringtone { get; set; } = "default";

        public bool EnableMessagePreview { get; set; } = true;

        public bool EnableGroupChatNotifications { get; set; } = true;

        public bool EnableFriendRequestNotifications { get; set; } = true;

        public bool EnableBirthdayNotifications { get; set; } = true;

        public bool EnableEventNotifications { get; set; } = true;

        public bool EnableMarketingEmails { get; set; } = false;

        public bool EnableNewsletter { get; set; } = false;

        public bool EnableLocationSharing { get; set; } = false;

        public bool EnableStatusUpdates { get; set; } = true;

        public bool EnableProfileVisibility { get; set; } = true;

        public bool EnableFriendListVisibility { get; set; } = true;

        public bool EnableActivityVisibility { get; set; } = true;

        public string DefaultChatView { get; set; } = "list"; // list or grid

        public string DefaultMessageView { get; set; } = "bubble"; // bubble or compact

        public bool EnableMessageSearch { get; set; } = true;

        public bool EnableMessageTranslation { get; set; } = false;

        public string DefaultLanguage { get; set; } = "tr";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastModified { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
    }
} 