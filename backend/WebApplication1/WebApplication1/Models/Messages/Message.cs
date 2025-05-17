using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebApplication1.Models.Enums;
using WebApplication1.Models.Chat;
using System.Linq;
using WebApplication1.Models.Notifications;
using WebApplication1.Models.Users;

namespace WebApplication1.Models.Messages
{
    public enum MessageStatus { Sent, Delivered, Read }

    public class Message
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string SenderId { get; set; }
        public virtual User Sender { get; set; }

        [Required]
        public string ChatRoomId { get; set; }
        public virtual ChatRoom ChatRoom { get; set; }

        public string? ReplyToMessageId { get; set; }
        public virtual Message? ReplyToMessage { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        public bool IsEdited { get; set; } = false;
        public DateTime? EditedAt { get; set; }
        public MessageStatus Status { get; set; } = MessageStatus.Sent;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        private HashSet<string> HiddenForUsers { get; set; } = new();
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
                    MessageId = Id,
                    Type = "MessageRead",
                    Status = false,
                    CreatedAt = DateTime.UtcNow
                };
                Notifications.Add(notification);
            }
        }

        public void DeleteMessage()
        {
            if (IsDeleted)
                throw new InvalidOperationException("Bu mesaj zaten silinmiş");

            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
            Content = "Bu mesaj silindi";
        }

        public void EditMessage(string newContent, string userId, EditType editType, string editReason = null)
        {
            if (IsDeleted)
                throw new InvalidOperationException("Silinmiş mesaj düzenlenemez");

            if (string.IsNullOrEmpty(newContent))
                throw new ArgumentException("Mesaj içeriği boş olamaz");

            if (SenderId != userId)
                throw new InvalidOperationException("Bu mesajı düzenleme yetkiniz yok");

            // Save old version to history
            var history = new MessageHistory
            {
                MessageId = Id,
                OldContent = Content,
                NewContent = newContent,
                EditedAt = DateTime.UtcNow,
                EditedByUserId = userId,
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

            // Notify participants about edit
            if (ChatRoom != null)
            {
                foreach (var participant in ChatRoom.Participants.Where(p => p.Id != userId))
                {
                    var notification = new Notification
                    {
                        UserId = participant.Id,
                        MessageId = Id,
                        Type = "MessageEdited",
                        Status = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    Notifications.Add(notification);
                }
            }
        }

        private string GetEditDescription(EditType editType, string editReason)
        {
            var description = editType switch
            {
                EditType.ContentEdit => "Mesaj içeriği düzenlendi",
                EditType.FormatEdit => "Mesaj formatı değiştirildi",
                EditType.AttachmentEdit => "Dosya eklendi/kaldırıldı",
                EditType.LinkEdit => "Link eklendi/kaldırıldı",
                EditType.Correction => "Yazım hatası düzeltildi",
                EditType.Translation => "Çeviri eklendi",
                _ => "Düzenleme yapıldı"
            };

            if (!string.IsNullOrEmpty(editReason))
            {
                description += $": {editReason}";
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
                    MessageId = Id,
                    Type = $"MessageStatusChanged_{newStatus}",
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

            DeleteMessage();
            
            // Notify all participants in chat room
            if (ChatRoom != null)
            {
                foreach (var participant in ChatRoom.Participants.Where(p => p.Id != userId))
                {
                    var notification = new Notification
                    {
                        UserId = participant.Id,
                        MessageId = Id,
                        Type = "MessageDeleted",
                        Status = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    Notifications.Add(notification);
                }
            }
        }

        public void DeleteForUser(string userId)
        {
            if (!HiddenForUsers.Contains(userId))
            {
                HiddenForUsers.Add(userId);
                
                // Notify the user
                var notification = new Notification
                {
                    UserId = userId,
                    MessageId = Id,
                    Type = "MessageDeletedForYou",
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
                    MessageId = reply.Id,
                    Type = "MessageReplied",
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

    public class MessageHistory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MessageId { get; set; }
        public string OldContent { get; set; }
        public string NewContent { get; set; }
        public DateTime EditedAt { get; set; } = DateTime.UtcNow;
        public string EditedByUserId { get; set; }
        public EditType EditType { get; set; }
        public string EditReason { get; set; }
        public string ChangeDescription { get; set; }

        public void SaveOldVersion(string messageId, string oldContent)
        {
            MessageId = messageId;
            OldContent = oldContent;
            EditedAt = DateTime.UtcNow;
        }
    }

    public class DeletedMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MessageId { get; set; }
        public string DeletedByUserId { get; set; }
        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;

        public void RestoreMessage()
        {
            // Not implemented
        }
    }
} 