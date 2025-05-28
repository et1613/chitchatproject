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
        public string? ChatRoomId { get; set; }
        public string? ChatRoomName { get; set; }
        public string? SenderId { get; set; }
        public string? SenderName { get; set; }
        public DateTime OriginalMessageTimestamp { get; set; }
        public int EditCount { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedByUserId { get; set; }
        public string? DeletedByUserName { get; set; }
        public string? DeleteReason { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

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
                HasContentChanged = historyEntity.HasContentChanged,
                ChatRoomId = historyEntity.Message?.ChatRoomId,
                ChatRoomName = historyEntity.Message?.ChatRoom?.Name,
                SenderId = historyEntity.Message?.SenderId,
                SenderName = historyEntity.Message?.Sender?.UserName,
                OriginalMessageTimestamp = historyEntity.Message?.Timestamp ?? DateTime.UtcNow,
                IsDeleted = historyEntity.Message?.IsDeleted ?? false,
                DeletedAt = historyEntity.Message?.DeletedAt,

            };
        }

        public string GetFormattedEditTime()
        {
            return EditedAt.ToString("dd.MM.yyyy HH:mm:ss");
        }

        public string GetFormattedOriginalTime()
        {
            return OriginalMessageTimestamp.ToString("dd.MM.yyyy HH:mm:ss");
        }

        public string GetEditSummary()
        {
            return $"{EditedByUserName} tarafından {GetFormattedEditTime()} tarihinde düzenlendi";
        }

        public string GetDeleteSummary()
        {
            if (!IsDeleted) return string.Empty;
            return $"{DeletedByUserName} tarafından {DeletedAt?.ToString("dd.MM.yyyy HH:mm:ss")} tarihinde silindi";
        }

        public bool HasMetadata(string key)
        {
            return Metadata.ContainsKey(key);
        }

        public string? GetMetadata(string key)
        {
            return Metadata.TryGetValue(key, out var value) ? value : null;
        }

        public void AddMetadata(string key, string value)
        {
            Metadata[key] = value;
        }

        public void RemoveMetadata(string key)
        {
            Metadata.Remove(key);
        }
    }
} 