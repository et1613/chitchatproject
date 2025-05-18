using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebApplication1.Models.Enums;
using WebApplication1.Models.Messages;
using WebApplication1.Models.Users;
using WebApplication1.Models.Notifications;

namespace WebApplication1.Models.Chat
{
    public class ChatRoom
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [StringLength(100)]
        public required string Name { get; set; }

        public string? Description { get; set; }
        public string? Picture { get; set; }

        [Required]
        [ForeignKey("Admin")]
        public required string AdminId { get; set; }
        public required virtual User Admin { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsPrivate { get; set; }
        public int MaxParticipants { get; set; } = 100;
        public bool AllowMessageEditing { get; set; } = true;
        public int MessageEditTimeLimit { get; set; } = 5; // dakika cinsinden
        public int MaxPinnedMessages { get; set; } = 5;

        // Navigation properties
        public virtual ICollection<User> Participants { get; set; } = new List<User>();
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
        public virtual ICollection<ChatRoomRole> UserRoles { get; set; } = new List<ChatRoomRole>();
        public virtual ICollection<MessageReadStatus> MessageReadStatuses { get; set; } = new List<MessageReadStatus>();
        public virtual ICollection<PinnedMessage> PinnedMessages { get; set; } = new List<PinnedMessage>();

        public void AddParticipant(User user)
        {
            if (user is null)
                throw new ArgumentNullException(nameof(user), "Kullanıcı null olamaz");

            if (Participants.Count >= MaxParticipants)
                throw new InvalidOperationException("Sohbet odası maksimum katılımcı sayısına ulaştı");

            if (Participants.Any(p => p.Id == user.Id))
                throw new InvalidOperationException("Kullanıcı zaten sohbet odasında");

            Participants.Add(user);
            UserRoles.Add(new ChatRoomRole { UserId = user.Id, Role = "Member" });

            // Diğer katılımcılara bildirim gönder
            NotifyParticipants(user.Id, "UserJoinedChat");
        }

        public void RemoveParticipant(User user)
        {
            if (user is null)
                throw new ArgumentNullException(nameof(user), "Kullanıcı null olamaz");

            if (!Participants.Any(p => p.Id == user.Id))
                throw new InvalidOperationException("Kullanıcı sohbet odasında değil");

            Participants.Remove(user);
            var userRole = UserRoles.FirstOrDefault(r => r.UserId == user.Id);
            if (userRole is not null)
                UserRoles.Remove(userRole);

            // Diğer katılımcılara bildirim gönder
            NotifyParticipants(user.Id, "UserLeftChat");

            // Admin kontrolü
            if (user.Id == AdminId && Participants.Count > 0)
            {
                var newAdmin = Participants.First();
                SetAdmin(newAdmin);
            }
        }

        public void SetAdmin(User newAdmin)
        {
            if (newAdmin is null)
                throw new ArgumentNullException(nameof(newAdmin), "Yeni admin null olamaz");

            if (!Participants.Any(p => p.Id == newAdmin.Id))
                throw new InvalidOperationException("Admin olarak atanacak kullanıcı sohbet odasında olmalı");

            AdminId = newAdmin.Id;
            var adminRole = UserRoles.FirstOrDefault(r => r.UserId == newAdmin.Id);
            if (adminRole != null)
                adminRole.Role = "Admin";
            else
                UserRoles.Add(new ChatRoomRole { UserId = newAdmin.Id, Role = "Admin" });

            NotifyParticipants(newAdmin.Id, "NewAdmin");
        }

        public void SendMessage(string senderId, string content)
        {
            if (string.IsNullOrEmpty(content))
                throw new ArgumentException("Mesaj içeriği boş olamaz", nameof(content));

            var sender = Participants.FirstOrDefault(p => p.Id == senderId);
            if (sender == null)
                throw new InvalidOperationException("Mesaj gönderen kullanıcı sohbet odasında değil");

            var message = new Message
            {
                SenderId = senderId,
                ChatRoomId = Id,
                Content = content,
                Timestamp = DateTime.UtcNow,
                IsEdited = false
            };

            Messages.Add(message);
            NotifyParticipants(senderId, "NewMessage", message.Id);
        }

        public void EditMessage(string messageId, string userId, string newContent)
        {
            if (!AllowMessageEditing)
                throw new InvalidOperationException("Mesaj düzenleme özelliği kapalı");

            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message == null)
                throw new InvalidOperationException("Mesaj bulunamadı");

            if (message.SenderId != userId && !IsUserAdmin(userId))
                throw new InvalidOperationException("Bu mesajı düzenleme yetkiniz yok");

            var timeSinceCreation = DateTime.UtcNow - message.Timestamp;
            if (timeSinceCreation.TotalMinutes > MessageEditTimeLimit)
                throw new InvalidOperationException($"Mesaj düzenleme süresi doldu ({MessageEditTimeLimit} dakika)");

            message.Content = newContent;
            message.IsEdited = true;
            message.EditedAt = DateTime.UtcNow;

            NotifyParticipants(userId, "MessageEdited", messageId);
        }

        public void DeleteMessage(string messageId, string userId)
        {
            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message == null)
                throw new InvalidOperationException("Mesaj bulunamadı");

            if (message.SenderId != userId && !IsUserAdmin(userId))
                throw new InvalidOperationException("Bu mesajı silme yetkiniz yok");

            Messages.Remove(message);
            NotifyParticipants(userId, "MessageDeleted", messageId);
        }

        public List<Message> SearchMessages(string searchTerm, string userId)
        {
            if (!IsUserInRoom(userId))
                throw new InvalidOperationException("Mesaj aramak için sohbet odasında olmalısınız");

            return Messages
                .Where(m => m.Content.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.Timestamp)
                .ToList();
        }

        public void UpdateSettings(string name, string description, bool isPrivate, int maxParticipants)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Sohbet odası adı boş olamaz");

            if (maxParticipants < Participants.Count)
                throw new ArgumentException("Maksimum katılımcı sayısı mevcut katılımcı sayısından az olamaz");

            Name = name;
            Description = description;
            IsPrivate = isPrivate;
            MaxParticipants = maxParticipants;

            NotifyParticipants(null, "RoomSettingsUpdated");
        }

        private bool IsUserAdmin(string userId)
        {
            return AdminId == userId || UserRoles.Any(r => r.UserId == userId && r.Role == "Admin");
        }

        private bool IsUserInRoom(string userId)
        {
            return Participants.Count > 0 && Participants.Any(p => p.Id == userId);
        }

        private void NotifyParticipants(string? excludedUserId, string notificationType, string? messageId = null)
        {
            if (!Enum.TryParse(notificationType, out NotificationType parsedNotificationType))
                throw new ArgumentException($"Invalid notification type: {notificationType}", nameof(notificationType));

            foreach (var participant in Participants.Where(p => p.Id != excludedUserId))
            {
                var notification = new Notification
                {
                    UserId = participant.Id,
                    MessageId = messageId,
                    Type = parsedNotificationType,
                    Status = false,
                    CreatedAt = DateTime.UtcNow
                };
                participant.Notifications.Add(notification);
            }
        }

        public List<Message> GetVisibleMessagesForUser(string userId)
        {
            if (!IsUserInRoom(userId))
                throw new InvalidOperationException("Mesajları görüntülemek için sohbet odasında olmalısınız");

            return Messages
                .Where(m => m.IsVisibleToUser(userId))
                .OrderBy(m => m.Timestamp)
                .ToList();
        }

        public void PinMessage(string messageId, string userId)
        {
            if (!IsUserAdmin(userId))
                throw new InvalidOperationException("Mesaj pinlemek için admin yetkisi gerekiyor");

            if (PinnedMessages.Count >= MaxPinnedMessages)
                throw new InvalidOperationException($"Maksimum pinlenmiş mesaj sayısına ulaşıldı ({MaxPinnedMessages})");

            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message == null)
                throw new InvalidOperationException("Mesaj bulunamadı");

            if (PinnedMessages.Any(p => p.MessageId == messageId))
                throw new InvalidOperationException("Bu mesaj zaten pinlenmiş");

            PinnedMessages.Add(new PinnedMessage
            {
                MessageId = messageId,
                PinnedByUserId = userId,
                PinnedAt = DateTime.UtcNow,
                Message = message
            });

            NotifyParticipants(null, "MessagePinned", messageId);
        }

        public void UnpinMessage(string messageId, string userId)
        {
            if (!IsUserAdmin(userId))
                throw new InvalidOperationException("Mesaj pinini kaldırmak için admin yetkisi gerekiyor");

            var pinnedMessage = PinnedMessages.FirstOrDefault(p => p.MessageId == messageId);
            if (pinnedMessage == null)
                throw new InvalidOperationException("Bu mesaj pinlenmemiş");

            PinnedMessages.Remove(pinnedMessage);
            NotifyParticipants(null, "MessageUnpinned", messageId);
        }

        public void MarkMessageAsRead(string messageId, string userId)
        {
            if (!IsUserInRoom(userId))
                throw new InvalidOperationException("Mesajı okundu olarak işaretlemek için sohbet odasında olmalısınız");

            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message == null)
                throw new InvalidOperationException("Mesaj bulunamadı");

            var readStatus = MessageReadStatuses.FirstOrDefault(r => r.MessageId == messageId && r.UserId == userId);
            if (readStatus == null)
            {
                MessageReadStatuses.Add(new MessageReadStatus
                {
                    MessageId = messageId,
                    UserId = userId,
                    ReadAt = DateTime.UtcNow
                });
            }
        }

        public List<Message> GetPinnedMessages()
        {
            return PinnedMessages
                .OrderByDescending(p => p.PinnedAt)
                .Select(p => Messages.First(m => m.Id == p.MessageId))
                .ToList();
        }

        public List<string> GetMessageReaders(string messageId)
        {
            return MessageReadStatuses
                .Where(r => r.MessageId == messageId)
                .Select(r => r.UserId)
                .ToList();
        }
    }

    public class ChatRoomRole
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public required string UserId { get; set; }
        public required string Role { get; set; } // "Admin", "Moderator", "Member"
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }

    public class MessageReadStatus
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public required string MessageId { get; set; }
        public required string UserId { get; set; }
        public DateTime ReadAt { get; set; }
    }

    public class PinnedMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public required string MessageId { get; set; }
        public required string PinnedByUserId { get; set; }
        public DateTime PinnedAt { get; set; }
        
        [ForeignKey("MessageId")]
        public required Message Message { get; set; }
    }
} 