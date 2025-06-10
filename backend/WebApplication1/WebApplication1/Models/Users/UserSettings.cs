using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.Users
{
    public class UserSettings
    {
        [Key, ForeignKey("User")]
        public required string UserId { get; set; }
        public required virtual User User { get; set; }

        [Required]
        public required string TimeZone { get; set; }

        [Required]
        public required string Language { get; set; }

        [Required]
        public required string Theme { get; set; }

        public bool NotificationsEnabled { get; set; }

        public bool EmailNotificationsEnabled { get; set; }

        public bool PushNotificationsEnabled { get; set; }

        public bool SoundEnabled { get; set; }

        public bool RememberMe { get; set; }

        public int SessionTimeout { get; set; }

        public bool ShowOnlineStatus { get; set; }

        public bool ShowLastSeen { get; set; }

        public bool ShowReadReceipts { get; set; }

        public bool ShowTypingIndicator { get; set; }

        public bool AutoSaveDrafts { get; set; }

        public int DraftAutoSaveInterval { get; set; }

        public bool EnableMessageSearch { get; set; }

        public bool EnableFileSharing { get; set; }

        public int MaxFileSize { get; set; }

        public string[] AllowedFileTypes { get; set; } = Array.Empty<string>();

        public bool EnableVoiceMessages { get; set; }

        public bool EnableVideoCalls { get; set; }

        public bool EnableScreenSharing { get; set; }

        public bool EnableLocationSharing { get; set; }

        public bool EnableContactSync { get; set; }

        public bool EnableCalendarSync { get; set; }

        public bool EnableTaskSync { get; set; }

        public bool EnableNoteSync { get; set; }

        public bool EnableCloudBackup { get; set; }

        public int BackupFrequency { get; set; }

        public DateTime LastBackup { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; }
    }
} 