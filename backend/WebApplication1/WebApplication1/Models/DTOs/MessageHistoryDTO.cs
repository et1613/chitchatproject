using System;
using WebApplication1.Models.Enums;

namespace WebApplication1.Models.DTOs
{
    public class MessageHistoryDTO
    {
        public string Id { get; set; }
        public string MessageId { get; set; }
        public string OldContent { get; set; }
        public string NewContent { get; set; }
        public DateTime EditedAt { get; set; }
        public string EditedByUserId { get; set; }
        public string EditedByUserName { get; set; }
        public EditType EditType { get; set; }
        public string EditReason { get; set; }
        public string ChangeDescription { get; set; }
        public string ContentDiff { get; set; }
        public bool HasContentChanged { get; set; }

        public static MessageHistoryDTO FromMessageHistory(MessageHistory history)
        {
            return new MessageHistoryDTO
            {
                Id = history.Id,
                MessageId = history.MessageId,
                OldContent = history.OldContent,
                NewContent = history.NewContent,
                EditedAt = history.EditedAt,
                EditedByUserId = history.EditedByUserId,
                EditedByUserName = history.EditedByUser?.UserName ?? "Bilinmeyen Kullanıcı",
                EditType = history.EditType,
                EditReason = history.EditReason,
                ChangeDescription = history.ChangeDescription,
                ContentDiff = history.GetContentDiff(),
                HasContentChanged = history.HasContentChanged
            };
        }
    }
} 