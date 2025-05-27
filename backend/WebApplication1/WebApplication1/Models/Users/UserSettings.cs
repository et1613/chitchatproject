using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.Users
{
    public class UserSettings
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string UserId { get; set; }

        public string Language { get; set; } = "tr";

        public string TimeZone { get; set; }

        public bool ShowOnlineStatus { get; set; } = true;

        public bool ShowLastSeen { get; set; } = true;

        public bool ShowReadReceipts { get; set; } = true;

        public bool ShowTypingStatus { get; set; } = true;

        public bool EnableNotifications { get; set; } = true;

        public bool EnableEmailNotifications { get; set; } = true;

        public bool EnablePushNotifications { get; set; } = true;

        public bool EnableSoundNotifications { get; set; } = true;

        public bool EnableVibrationNotifications { get; set; } = true;

        public string Theme { get; set; } = "light";

        public string FontSize { get; set; } = "medium";

        public bool EnableAutoSave { get; set; } = true;

        public int AutoSaveInterval { get; set; } = 5; // minutes

        public bool EnableTwoFactorAuth { get; set; } = false;

        public bool EnableLoginNotifications { get; set; } = true;

        public bool EnableActivityLogging { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastModified { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
    }
} 