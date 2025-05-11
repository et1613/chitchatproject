using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using WebApplication1.Models.Enums;
using WebApplication1.Models.Messages;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Notifications;

namespace WebApplication1.Models.Users
{
    public class User
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [StringLength(50)]
        public string UserName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        [StringLength(100)]
        public string? FirstName { get; set; }

        [StringLength(100)]
        public string? LastName { get; set; }

        public string? ProfilePicture { get; set; }
        public string? Bio { get; set; }
        public DateTime? LastSeen { get; set; }
        public UserStatus Status { get; set; } = UserStatus.Offline;
        public UserRole Role { get; set; } = UserRole.User;
        public bool IsActive { get; set; } = true;
        public bool IsVerified { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        // Kullanıcı tercihleri
        public string? Language { get; set; } = "tr";
        public string? TimeZone { get; set; }
        public bool ShowOnlineStatus { get; set; } = true;
        public bool ShowLastSeen { get; set; } = true;
        public bool ShowReadReceipts { get; set; } = true;
        public bool ShowTypingStatus { get; set; } = true;

        // Güvenlik
        public bool TwoFactorEnabled { get; set; } = false;
        public string? TwoFactorSecret { get; set; }
        public DateTime? TwoFactorEnabledAt { get; set; }
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEnd { get; set; }

        // Navigation properties
        public virtual ICollection<ChatRoom> ChatRooms { get; set; } = new List<ChatRoom>();
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
        public virtual ICollection<FriendRequest> SentFriendRequests { get; set; } = new List<FriendRequest>();
        public virtual ICollection<FriendRequest> ReceivedFriendRequests { get; set; } = new List<FriendRequest>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public virtual NotificationSettings NotificationSettings { get; set; }
        public virtual ICollection<BlockedUser> BlockedUsers { get; set; } = new List<BlockedUser>();
        public virtual ICollection<BlockedUser> BlockedByUsers { get; set; } = new List<BlockedUser>();
        public virtual UserStatistics Statistics { get; set; }
        public virtual ICollection<UserActivity> Activities { get; set; } = new List<UserActivity>();

        public string GetFullName()
        {
            if (string.IsNullOrEmpty(FirstName) && string.IsNullOrEmpty(LastName))
                return UserName;

            return $"{FirstName} {LastName}".Trim();
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
            return BlockedByUsers.Any(b => b.BlockerId == userId);
        }

        public bool HasBlocked(string userId)
        {
            return BlockedUsers.Any(b => b.BlockedId == userId);
        }

        public void BlockUser(string userId)
        {
            if (!HasBlocked(userId))
            {
                BlockedUsers.Add(new BlockedUser
                {
                    BlockerId = Id,
                    BlockedId = userId,
                    BlockedAt = DateTime.UtcNow
                });
            }
        }

        public void UnblockUser(string userId)
        {
            var blockedUser = BlockedUsers.FirstOrDefault(b => b.BlockedId == userId);
            if (blockedUser != null)
            {
                BlockedUsers.Remove(blockedUser);
            }
        }

        public void EnableTwoFactor(string secret)
        {
            TwoFactorEnabled = true;
            TwoFactorSecret = secret;
            TwoFactorEnabledAt = DateTime.UtcNow;
        }

        public void DisableTwoFactor()
        {
            TwoFactorEnabled = false;
            TwoFactorSecret = null;
            TwoFactorEnabledAt = null;
        }

        public void RecordFailedLogin()
        {
            FailedLoginAttempts++;
            if (FailedLoginAttempts >= 5)
            {
                LockoutEnd = DateTime.UtcNow.AddMinutes(15);
            }
        }

        public void ResetFailedLoginAttempts()
        {
            FailedLoginAttempts = 0;
            LockoutEnd = null;
        }

        public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd.Value > DateTime.UtcNow;

        public void UpdateProfile(string firstName, string lastName, string bio, string profilePicture)
        {
            FirstName = firstName;
            LastName = lastName;
            Bio = bio;
            ProfilePicture = profilePicture;
        }

        public void UpdatePreferences(string language, string timeZone, bool showOnlineStatus, 
            bool showLastSeen, bool showReadReceipts, bool showTypingStatus)
        {
            Language = language;
            TimeZone = timeZone;
            ShowOnlineStatus = showOnlineStatus;
            ShowLastSeen = showLastSeen;
            ShowReadReceipts = showReadReceipts;
            ShowTypingStatus = showTypingStatus;
        }

        public override string ToString()
        {
            return $"{GetFullName()} ({UserName})";
        }

        public void SendMessage(string receiverId, string content)
        {
            var message = new Message
            {
                SenderId = this.Id,
                ReceiverId = receiverId,
                Content = content,
                Timestamp = DateTime.UtcNow
            };

            Messages.Add(message);
            Statistics?.IncrementMessages();

            var notification = new Notification
            {
                UserId = receiverId,
                MessageId = message.Id,
                Type = "NewMessage",
                Status = false,
                CreatedAt = DateTime.UtcNow
            };
            Notifications.Add(notification);
        }

        public void JoinChatRoom(string chatRoomId)
        {
            var chatRoom = ChatRooms.FirstOrDefault(cr => cr.Id == chatRoomId);
            if (chatRoom != null && !ChatRooms.Contains(chatRoom))
            {
                ChatRooms.Add(chatRoom);
                chatRoom.AddParticipant(this);
                Statistics?.IncrementChatRooms();
            }
        }

        public void LeaveChatRoom(string chatRoomId)
        {
            var chatRoom = ChatRooms.FirstOrDefault(cr => cr.Id == chatRoomId);
            if (chatRoom != null)
            {
                ChatRooms.Remove(chatRoom);
                chatRoom.RemoveParticipant(this);
            }
        }

        public void SendFriendRequest(string userId)
        {
            if (!SentFriendRequests.Any(fr => fr.ReceiverId == userId))
            {
                var notification = new Notification
                {
                    UserId = userId,
                    Type = "FriendRequest",
                    Status = false,
                    CreatedAt = DateTime.UtcNow
                };
                Notifications.Add(notification);
            }
        }

        public void ResponseFriendRequest(string userId, bool accept)
        {
            if (accept)
            {
                SentFriendRequests.Add(new FriendRequest
                {
                    SenderId = this.Id,
                    ReceiverId = userId,
                    Accepted = true,
                    SentAt = DateTime.UtcNow
                });
                Statistics?.IncrementFriends();
            }

            var notification = new Notification
            {
                UserId = userId,
                Type = accept ? "FriendRequestAccepted" : "FriendRequestRejected",
                Status = false,
                CreatedAt = DateTime.UtcNow
            };
            Notifications.Add(notification);
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
                RelatedEntityType = relatedEntityType
            });
        }

        public bool HasPermission(UserRole requiredRole)
        {
            return Role >= requiredRole;
        }

        public void PromoteToRole(UserRole newRole)
        {
            if (newRole > Role)
            {
                Role = newRole;
            }
        }

        public void DemoteFromRole(UserRole newRole)
        {
            if (newRole < Role)
            {
                Role = newRole;
            }
        }
    }
} 