using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebApplication1.Models.Enums;
using WebApplication1.Models.Chat;
using System.Linq;
using WebApplication1.Models.Notifications;
using WebApplication1.Models.Users;
using WebApplication1.Models.DTOs;
using System.Threading.Tasks;

namespace WebApplication1.Models.Messages
{
    public enum MessageStatus { Sent, Delivered, Read }

    public class Message
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public required string SenderId { get; set; }
        public required virtual User Sender { get; set; }

        [Required]
        public required string ChatRoomId { get; set; }
        public required virtual ChatRoom ChatRoom { get; set; }

        public string? ReplyToMessageId { get; set; }
        public virtual Message? ReplyToMessage { get; set; }

        [Required]
        public required string Content { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        public bool IsEdited { get; set; } = false;
        public DateTime? EditedAt { get; set; }
        public MessageStatus Status { get; set; } = MessageStatus.Sent;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedByUserId { get; set; }
        public virtual User? DeletedByUser { get; set; }
        public string? DeleteReason { get; set; }
        public int EditCount { get; set; }

        public List<string> HiddenForUsers { get; set; } = new();
        public IReadOnlyCollection<string> HiddenUsers => HiddenForUsers;

        // Navigation properties
        public virtual ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
        public virtual ICollection<MessageHistory> EditHistory { get; set; } = new List<MessageHistory>();
        public virtual ICollection<Message> Replies { get; set; } = new List<Message>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        public void MarkAsRead()
        {
            if (IsDeleted)
                throw new InvalidOperationException("Silinmiş mesaj okundu olarak işaretlenemez");

            IsRead = true;
            Status = MessageStatus.Read;
            Timestamp = DateTime.UtcNow;

            // Notify sender that message was read  
            if (Sender != null)
            {
                var notification = new Notification
                {
                    UserId = SenderId,
                    User = Sender,
                    MessageId = Id,
                    Type = NotificationType.MessageRead,
                    Status = false,
                    CreatedAt = DateTime.UtcNow
                };
                Notifications.Add(notification);
            }
        }

        public void DeleteMessage(string userId, string? reason = null)
        {
            if (IsDeleted)
                throw new InvalidOperationException("Bu mesaj zaten silinmiş");

            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
            DeletedByUserId = userId;
            DeleteReason = reason;
            Content = "Bu mesaj silindi";
        }

        public void EditMessage(string newContent, string userId, EditType editType, string? editReason = null)
        {
            if (IsDeleted)
                throw new InvalidOperationException("Silinmiş mesaj düzenlenemez");

            if (string.IsNullOrEmpty(newContent))
                throw new ArgumentException("Mesaj içeriği boş olamaz");

            if (SenderId != userId)
                throw new InvalidOperationException("Bu mesajı düzenleme yetkiniz yok");

            // Get the user who is editing the message  
            var editingUser = ChatRoom?.Participants.FirstOrDefault(p => p.Id == userId);
            if (editingUser == null)
                throw new InvalidOperationException("Düzenleme yapan kullanıcı bulunamadı");

            // Save old version to history  
            var history = new MessageHistory
            {
                MessageId = Id,
                Message = this,
                OldContent = Content,
                NewContent = newContent,
                EditedAt = DateTime.UtcNow,
                EditedByUserId = userId,
                EditedByUser = editingUser,
                EditType = editType,
                EditReason = editReason,
                ChangeDescription = GetEditDescription(editType, editReason)
            };
            EditHistory.Add(history);

            // Update message  
            Content = newContent;
            IsEdited = true;
            EditedAt = DateTime.UtcNow;
            Timestamp = DateTime.UtcNow;
            EditCount++;

            // Notify participants about edit  
            if (ChatRoom != null)
            {
                foreach (var participant in ChatRoom.Participants.Where(p => p.Id != userId))
                {
                    var notification = new Notification
                    {
                        UserId = participant.Id,
                        User = participant,
                        MessageId = Id,
                        Type = NotificationType.MessageEdited,
                        Status = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    Notifications.Add(notification);
                }
            }
        }

        public static string GetEditDescription(EditType type, string? additionalInfo = null)
        {
            var description = EditTypeDescriptions.GetDescription(type);

            if (!string.IsNullOrEmpty(additionalInfo))
            {
                description += $": {additionalInfo}";
            }

            return description;
        }

        public List<MessageHistoryDTO> GetEditHistory(int? limit = null)
        {
            var history = EditHistory
                .OrderByDescending(h => h.EditedAt)
                .Select(h => MessageHistoryDTO.FromMessageHistory(h));

            if (limit.HasValue)
            {
                history = history.Take(limit.Value);
            }

            return history.ToList();
        }

        public void UpdateStatus(MessageStatus newStatus)
        {
            if (IsDeleted)
                throw new InvalidOperationException("Silinmiş mesajın durumu güncellenemez");

            var oldStatus = Status;
            Status = newStatus;

            if (newStatus == MessageStatus.Read)
                IsRead = true;

            // Notify sender about status change
            if (Sender != null && oldStatus != newStatus)
            {
                var notification = new Notification
                {
                    UserId = SenderId,
                    User = Sender,
                    MessageId = Id,
                    Type = NotificationType.MessageStatusChanged,
                    Status = false,
                    CreatedAt = DateTime.UtcNow
                };
                Notifications.Add(notification);
            }
        }

        public void DeleteForEveryone(string userId)
        {
            if (SenderId != userId)
                throw new InvalidOperationException("Bu mesajı herkes için silme yetkiniz yok");

            DeleteMessage(userId, "Herkes için silindi");

            // Notify all participants in chat room  
            if (ChatRoom != null)
            {
                foreach (var participant in ChatRoom.Participants.Where(p => p.Id != userId))
                {
                    var notification = new Notification
                    {
                        UserId = participant.Id,
                        User = participant,
                        MessageId = Id,
                        Type = NotificationType.MessageDeleted,
                        Status = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    Notifications.Add(notification);
                }
            }
        }

        public void DeleteForUser(string userId)
        {
            var user = ChatRoom?.Participants.FirstOrDefault(p => p.Id == userId);
            if (user == null)
                throw new InvalidOperationException("Kullanıcı bulunamadı");

            if (!HiddenForUsers.Contains(userId))
            {
                HiddenForUsers.Add(userId);

                // Notify the user  
                var notification = new Notification
                {
                    UserId = userId,
                    User = user,
                    MessageId = Id,
                    Type = NotificationType.MessageDeletedForYou,
                    Status = false,
                    CreatedAt = DateTime.UtcNow
                };
                Notifications.Add(notification);
            }
        }

        public void AddReply(Message reply)
        {
            if (IsDeleted)
                throw new InvalidOperationException("Silinmiş mesaja yanıt eklenemez");

            reply.ReplyToMessageId = Id;
            Replies.Add(reply);

            // Notify original message sender about reply  
            if (Sender != null && SenderId != reply.SenderId)
            {
                var notification = new Notification
                {
                    UserId = SenderId,
                    User = Sender,
                    MessageId = reply.Id,
                    Type = NotificationType.MessageReplied,
                    Status = false,
                    CreatedAt = DateTime.UtcNow
                };
                Notifications.Add(notification);
            }
        }

        public bool IsVisibleToUser(string userId)
        {
            return !HiddenForUsers.Contains(userId) && !IsDeleted;
        }

        public List<Message> GetReplies()
        {
            return Replies.OrderBy(r => r.Timestamp).ToList();
        }
    }

    public class DeletedMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public required string MessageId { get; set; }
        public required string DeletedByUserId { get; set; }
        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
        public string? DeleteReason { get; set; }

        public void RestoreMessage()
        {
            // Implementation will be added
        }
    }
}