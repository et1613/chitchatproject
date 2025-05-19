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
        public required string UserName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public required string Email { get; set; }

        [Required]
        public required string PasswordHash { get; set; }

        [StringLength(100)]
        public string? FirstName { get; set; }

        [StringLength(100)]
        public string? LastName { get; set; }

        public string? ProfilePicture { get; set; }
        public string? Bio { get; set; }
        public DateTime? LastSeen { get; set; }
        public UserStatus Status { get; set; } = UserStatus.Offline;
        public UserRole Role { get; set; } = UserRole.Member;
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
        public virtual ICollection<User> Friends { get; set; } = new List<User>();
        public virtual ICollection<ChatRoom> ChatRooms { get; set; } = new List<ChatRoom>();
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
        public virtual ICollection<FriendRequest> SentFriendRequests { get; set; } = new List<FriendRequest>();
        public virtual ICollection<FriendRequest> ReceivedFriendRequests { get; set; } = new List<FriendRequest>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public required virtual NotificationSettings NotificationSettings { get; set; }
        public virtual ICollection<User> BlockedUsers { get; set; } = new List<User>();
        public virtual ICollection<User> BlockedByUsers { get; set; } = new List<User>();
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
            return BlockedByUsers.Any(b => b.Id == userId);
        }

        public bool HasBlocked(string userId)
        {
            return BlockedUsers.Any(b => b.Id == userId);
        }

        public void BlockUser(User targetUser)
        {
            if (!BlockedUsers.Any(u => u.Id == targetUser.Id))
            {
                BlockedUsers.Add(targetUser);
            }
        }


        public void UnblockUser(User targetUser)
        {
            var userToRemove = BlockedUsers.FirstOrDefault(u => u.Id == targetUser.Id);
            if (userToRemove != null)
            {
                BlockedUsers.Remove(userToRemove);
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

            Messages.Add(message);

            var receiverIds = chatRoom.Participants
                .Where(u => u.Id != this.Id)
                .Select(u => u.Id)
                .ToList();

            foreach (var receiverId in receiverIds)
            {
                var notification = new Notification
                {
                    UserId = receiverId,
                    User = this, // Fix: Set the required 'User' property  
                    Message = message,
                    Type = NotificationType.NewMessage,
                    Status = false,
                    CreatedAt = DateTime.UtcNow
                };
                Notifications.Add(notification);
            }
        }


        public void JoinChatRoom(string chatRoomId)
        {
            var chatRoom = ChatRooms.FirstOrDefault(cr => cr.Id == chatRoomId);
            if (chatRoom != null && !ChatRooms.Contains(chatRoom))
            {
                ChatRooms.Add(chatRoom);
                chatRoom.AddParticipant(this);
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
                var receiver = ReceivedFriendRequests.FirstOrDefault(r => r.SenderId == userId)?.Sender;
                if (receiver == null)
                {
                    throw new InvalidOperationException("Receiver user not found.");
                }

                var friendRequest = new FriendRequest
                {
                    SenderId = this.Id,
                    Sender = this, // Fix: Set the required 'Sender' property  
                    ReceiverId = userId,
                    Receiver = receiver, // Fix: Set the required 'Receiver' property  
                    SentAt = DateTime.UtcNow,
                    Accepted = false
                };
                SentFriendRequests.Add(friendRequest);

                var notification = new Notification
                {
                    UserId = userId,
                    User = this, // Fix: Set the required 'User' property  
                    Type = NotificationType.FriendRequest,
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
                var receiver = ReceivedFriendRequests.FirstOrDefault(r => r.SenderId == userId)?.Sender;
                if (receiver == null)
                {
                    throw new InvalidOperationException("Receiver user not found.");
                }

                SentFriendRequests.Add(new FriendRequest
                {
                    SenderId = this.Id,
                    Sender = this, // Fix: Set the required 'Sender' property  
                    ReceiverId = userId,
                    Receiver = receiver, // Fix: Set the required 'Receiver' property  
                    Accepted = true,
                    SentAt = DateTime.UtcNow
                });
            }

            var notification = new Notification
            {
                UserId = userId,
                User = this, // Fix: Set the required 'User' property  
                Type = accept ? NotificationType.FriendRequestAccepted : NotificationType.FriendRequestRejected,
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
                RelatedEntityType = relatedEntityType,
                User = this 
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