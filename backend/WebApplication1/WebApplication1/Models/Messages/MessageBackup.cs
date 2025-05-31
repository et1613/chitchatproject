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
        public string ChatRoomId { get; set; }

        [Required]
        public string UserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int MessageCount { get; set; }

        public long BackupSize { get; set; }

        [Required]
        public string BackupPath { get; set; }

        [NotMapped]
        public string FileName => $"backup_{Id}_{CreatedAt:yyyyMMddHHmmss}.json";
    }
} 