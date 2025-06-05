using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.Users
{
    public class UserPreferences
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public required string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual required User User { get; set; }

        [Required]
        public required string DisplayName { get; set; }

        public string? ProfilePicture { get; set; }

        public string? Bio { get; set; }

        public string? Location { get; set; }

        public string? Website { get; set; }

        public string? PhoneNumber { get; set; }

        public bool IsPhoneNumberPublic { get; set; }

        public bool IsEmailPublic { get; set; }

        public bool IsLocationPublic { get; set; }

        public bool IsOnlineStatusPublic { get; set; }

        public bool IsLastSeenPublic { get; set; }

        public bool IsReadReceiptsPublic { get; set; }

        public bool IsTypingIndicatorPublic { get; set; }

        public bool IsProfilePicturePublic { get; set; }

        public bool IsBioPublic { get; set; }

        public bool IsWebsitePublic { get; set; }

        public bool IsActivityStatusPublic { get; set; }

        public bool IsFriendListPublic { get; set; }

        public bool IsGroupListPublic { get; set; }

        public bool IsMessageHistoryPublic { get; set; }

        public bool IsMediaGalleryPublic { get; set; }

        public bool IsTaggedPhotosPublic { get; set; }

        public bool IsCheckInsPublic { get; set; }

        public bool IsEventsPublic { get; set; }

        public bool IsNotesPublic { get; set; }

        public bool IsTasksPublic { get; set; }

        public bool IsCalendarPublic { get; set; }

        public bool IsContactListPublic { get; set; }

        public bool IsDeviceListPublic { get; set; }

        public bool IsLoginHistoryPublic { get; set; }

        public bool IsSecuritySettingsPublic { get; set; }

        public bool IsNotificationSettingsPublic { get; set; }

        public bool IsPrivacySettingsPublic { get; set; }

        public bool IsBlockedUsersListPublic { get; set; }

        public bool IsMutedUsersListPublic { get; set; }

        public bool IsRestrictedUsersListPublic { get; set; }

        public bool IsReportedUsersListPublic { get; set; }

        public bool IsDeletedMessagesListPublic { get; set; }

        public bool IsArchivedMessagesListPublic { get; set; }

        public bool IsStarredMessagesListPublic { get; set; }

        public bool IsPinnedMessagesListPublic { get; set; }

        public bool IsSavedItemsListPublic { get; set; }

        public bool IsRecentSearchesListPublic { get; set; }

        public bool IsRecentContactsListPublic { get; set; }

        public bool IsRecentGroupsListPublic { get; set; }

        public bool IsRecentFilesListPublic { get; set; }

        public bool IsRecentLinksListPublic { get; set; }

        public bool IsRecentLocationsListPublic { get; set; }

        public bool IsRecentEventsListPublic { get; set; }

        public bool IsRecentNotesListPublic { get; set; }

        public bool IsRecentTasksListPublic { get; set; }

        public bool IsRecentCalendarItemsListPublic { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
} 