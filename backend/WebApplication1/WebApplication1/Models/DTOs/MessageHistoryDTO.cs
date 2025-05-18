using System;
using WebApplication1.Models.Enums;
using WebApplication1.Models.Messages;

namespace WebApplication1.Models.DTOs
{
    public class MessageHistoryDTO
    {
        public required string Id { get; set; }
        public required string MessageId { get; set; }
        public required string OldContent { get; set; }
        public required string NewContent { get; set; }
        public DateTime EditedAt { get; set; }
        public required string EditedByUserId { get; set; }
        public required string EditedByUserName { get; set; }
        public EditType EditType { get; set; }
        public required string EditReason { get; set; }
        public required string ChangeDescription { get; set; }
        public required string ContentDiff { get; set; }
        public bool HasContentChanged { get; set; }

        public static MessageHistoryDTO FromMessageHistory(MessageHistory historyEntity)
        {
            return new MessageHistoryDTO
            {
                Id = historyEntity.Id,
                MessageId = historyEntity.MessageId,
                OldContent = historyEntity.OldContent,
                NewContent = historyEntity.NewContent ?? string.Empty,
                EditedAt = historyEntity.EditedAt,
                EditedByUserId = historyEntity.EditedByUserId,
                EditedByUserName = historyEntity.EditedByUser?.UserName ?? "Bilinmeyen Kullanıcı",
                EditType = historyEntity.EditType,
                EditReason = historyEntity.EditReason ?? string.Empty,
                ChangeDescription = historyEntity.ChangeDescription ?? string.Empty,
                ContentDiff = historyEntity.GetContentDiff(),
                HasContentChanged = historyEntity.HasContentChanged
            };
        }
    }
} 