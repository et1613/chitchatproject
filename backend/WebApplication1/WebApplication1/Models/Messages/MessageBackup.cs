using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.Messages
{
    public class MessageBackup
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public required string ChatRoomId { get; set; }

        [Required]
        public required string UserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public int MessageCount { get; set; }

        public long BackupSize { get; set; }

        [Required]
        public required string BackupPath { get; set; }

        [NotMapped]
        public string FileName => $"backup_{Id}_{CreatedAt:yyyyMMddHHmmss}.json";

        public static MessageBackup Create(string chatRoomId, string userId, string backupPath)
        {
            return new MessageBackup
            {
                ChatRoomId = chatRoomId,
                UserId = userId,
                BackupPath = backupPath,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
} 