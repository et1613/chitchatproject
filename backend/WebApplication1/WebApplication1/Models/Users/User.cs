using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using WebApplication1.Models.Enums;
using WebApplication1.Models.Messages;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Notifications;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.Users
{
    public class User
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [StringLength(50)]
        public required string UserName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public required string Email { get; set; }

        [Required]
        public required string PasswordHash { get; set; }

        [StringLength(100)]
        public string? DisplayName { get; set; }

        [StringLength(500)]
        public string? Bio { get; set; }

        public string? ProfilePictureUrl { get; set; }

        public UserStatus Status { get; set; }

        public UserRole Role { get; set; } = UserRole.Member;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastSeen { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsVerified { get; set; } = false;

        public DateTime? LastLoginAt { get; set; }

        public virtual UserSettings? UserSettings { get; set; }

        public virtual UserPreferences? UserPreferences { get; set; }

        public virtual ICollection<User> Friends { get; set; } = new List<User>();

        public virtual ICollection<BlockedUser> BlockedUsers { get; set; } = new List<BlockedUser>();

        public virtual ICollection<UserActivity> Activities { get; set; } = new List<UserActivity>();

        [InverseProperty("User")]
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        public virtual ICollection<ChatRoom> ChatRooms { get; set; } = new List<ChatRoom>();

        public virtual ICollection<FriendRequest> SentFriendRequests { get; set; } = new List<FriendRequest>();

        public virtual ICollection<FriendRequest> ReceivedFriendRequests { get; set; } = new List<FriendRequest>();

        public virtual ICollection<BlockedUser> BlockedByUsers { get; set; } = new List<BlockedUser>();

        public string GetFullName()
        {
            if (string.IsNullOrEmpty(DisplayName))
                return UserName;

            return DisplayName;
        }

        public void UpdateLastSeen()
        {
            LastSeen = DateTime.UtcNow;
            Status = UserStatus.Online;
        }

        public void SetStatus(UserStatus newStatus)
        {
            Status = newStatus;
            if (newStatus == UserStatus.Offline)
            {
                LastSeen = DateTime.UtcNow;
            }
        }

        public bool IsBlockedBy(string userId)
        {
            return BlockedUsers.Any(b => b.BlockerUserId == userId && b.IsCurrentlyBlocked);
        }

        public bool HasBlocked(string userId)
        {
            return BlockedUsers.Any(b => b.BlockedUserId == userId && b.IsCurrentlyBlocked);
        }

        public void BlockUser(User targetUser, string? reason = null, TimeSpan? duration = null)
        {
            var existingBlock = BlockedUsers.FirstOrDefault(b => b.BlockedUserId == targetUser.Id);
            
            if (existingBlock != null)
            {
                if (!existingBlock.IsActive)
                {
                    // Yeniden engelleme
                    existingBlock.IsActive = true;
                    existingBlock.BlockedAt = DateTime.UtcNow;
                    existingBlock.Reason = reason;
                    existingBlock.BlockCount++;
                    existingBlock.BlockExpiresAt = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : null;
                    existingBlock.IsPermanent = !duration.HasValue;
                }
            }
            else
            {
                // Yeni engelleme
                var blockedUser = new BlockedUser
                {
                    Id = Guid.NewGuid().ToString(),
                    BlockerUserId = this.Id,
                    BlockedUserId = targetUser.Id,
                    BlockerUser = this,
                    BlockedUserEntity = targetUser,
                    BlockedAt = DateTime.UtcNow,
                    Reason = reason,
                    BlockExpiresAt = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : null,
                    IsPermanent = !duration.HasValue
                };
                BlockedUsers.Add(blockedUser);
            }
        }

        public void UnblockUser(User targetUser, string? reason = null)
        {
            var blockedUser = BlockedUsers.FirstOrDefault(b => b.BlockedUserId == targetUser.Id);
            if (blockedUser != null && blockedUser.IsActive)
            {
                blockedUser.IsActive = false;
                blockedUser.LastUnblockedAt = DateTime.UtcNow;
                blockedUser.UnblockReason = reason;
            }
        }

        public void RecordFailedLogin()
        {
            // Implementation of RecordFailedLogin method
        }

        public void ResetFailedLoginAttempts()
        {
            // Implementation of ResetFailedLoginAttempts method
        }

        public bool IsLockedOut => false; // Implementation of IsLockedOut property

        public void UpdateProfile(string? firstName, string? lastName, string? bio, string? profilePicture)
        {
            DisplayName = firstName;
            Bio = bio;
            ProfilePictureUrl = profilePicture;
        }

        public void UpdatePreferences(string? language, string? timeZone, bool showOnlineStatus, 
            bool showLastSeen, bool showReadReceipts, bool showTypingStatus)
        {
            // Implementation of UpdatePreferences method
        }

        public override string ToString()
        {
            return $"{GetFullName()} ({UserName})";
        }

        public void SendMessage(string content, ChatRoom chatRoom)
        {
            var message = new Message
            {
                SenderId = this.Id,
                Sender = this,
                Content = content,
                Timestamp = DateTime.UtcNow,
                ChatRoomId = chatRoom.Id,
                ChatRoom = chatRoom
            };

            Activities.Add(new UserActivity
            {
                UserId = Id,
                ActivityType = "MessageSent",
                Description = $"Sent a message to {chatRoom.Name}",
                IpAddress = string.Empty,
                UserAgent = string.Empty,
                Location = string.Empty,
                IsSuccessful = true,
                ErrorMessage = string.Empty,
                RelatedEntityId = chatRoom.Id,
                RelatedEntityType = "ChatRoom",
                User = this
            });

            var receiverIds = chatRoom.Participants
                .Where(u => u.Id != this.Id)
                .Select(u => u.Id)
                .ToList();

            foreach (var receiverId in receiverIds)
            {
                var notification = new Notification
                {
                    UserId = receiverId,
                    User = this,
                    Message = message,
                    Type = NotificationType.NewMessage,
                    Status = false,
                    CreatedAt = DateTime.UtcNow
                };
                Activities.Add(new UserActivity
                {
                    UserId = Id,
                    ActivityType = "NotificationCreated",
                    Description = $"Created a notification for {receiverId}",
                    IpAddress = string.Empty,
                    UserAgent = string.Empty,
                    Location = string.Empty,
                    IsSuccessful = true,
                    ErrorMessage = string.Empty,
                    RelatedEntityId = notification.Id,
                    RelatedEntityType = "Notification",
                    User = this
                });
            }
        }

        public void JoinChatRoom(string chatRoomId)
        {
            // Implementation of JoinChatRoom method
        }

        public void LeaveChatRoom(string chatRoomId)
        {
            // Implementation of LeaveChatRoom method
        }

        public void SendFriendRequest(string userId)
        {
            // Implementation of SendFriendRequest method
        }

        public void ResponseFriendRequest(string userId, bool accept)
        {
            // Implementation of ResponseFriendRequest method
        }

        public void RecordActivity(string activityType, string description, string? ipAddress = null,
           string? userAgent = null, string? location = null, bool isSuccessful = true,
           string? errorMessage = null, string? relatedEntityId = null, string? relatedEntityType = null)
        {
            Activities.Add(new UserActivity
            {
                UserId = Id,
                ActivityType = activityType,
                Description = description,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Location = location,
                IsSuccessful = isSuccessful,
                ErrorMessage = errorMessage,
                RelatedEntityId = relatedEntityId,
                RelatedEntityType = relatedEntityType,
                User = this
            });
        }

        public bool HasPermission(UserRole requiredRole)
        {
            // Implementation of HasPermission method
            return false;
        }

        public void PromoteToRole(UserRole newRole)
        {
            // Implementation of PromoteToRole method
        }

        public void DemoteFromRole(UserRole newRole)
        {
            // Implementation of DemoteFromRole method
        }

        public IEnumerable<BlockedUser> GetActiveBlocks()
        {
            return BlockedUsers.Where(b => b.IsCurrentlyBlocked);
        }

        public IEnumerable<BlockedUser> GetBlockHistory()
        {
            return BlockedUsers.OrderByDescending(b => b.BlockedAt);
        }

        public int GetTotalBlockCount()
        {
            return BlockedUsers.Sum(b => b.BlockCount);
        }
    }
} 