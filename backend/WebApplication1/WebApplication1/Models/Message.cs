using System;
using System.Collections.Generic;

namespace WebApplication1.Models
{
    public enum MessageStatus { Sent, Delivered, Read }

    public class Message
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SenderId { get; set; }
        public string ReceiverId { get; set; }
        public string ChatRoomId { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        public bool IsEdited { get; set; } = false;
        public List<Attachment> Attachments { get; set; } = new();
        public MessageStatus Status { get; set; } = MessageStatus.Sent;
        public bool IsDeleted { get; set; } = false;
        public DateTime DeletedAt { get; set; }

        private HashSet<string> HiddenForUsers { get; set; } = new();
        public IReadOnlyCollection<string> HiddenUsers => HiddenForUsers;

        public void MarkAsRead() => IsRead = true;

        public void DeleteMessage()
        {
            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
        }

        public void EditMessage(string newContent)
        {
            IsEdited = true;
            Content = newContent;
        }

        public void UpdateStatus(MessageStatus newStatus) => Status = newStatus;

        public void DeleteForEveryone() => DeleteMessage();

        public void DeleteForUser(string userId)
        {
            if (!HiddenForUsers.Contains(userId))
                HiddenForUsers.Add(userId);
        }

        public bool IsVisibleToUser(string userId) => !HiddenForUsers.Contains(userId) && !IsDeleted;
    }

    public class MessageHistory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MessageId { get; set; }
        public string OldContent { get; set; }
        public DateTime EditedAt { get; set; } = DateTime.UtcNow;
        public string EditedByUserId { get; set; }
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