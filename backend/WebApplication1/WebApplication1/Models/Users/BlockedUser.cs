using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.Users
{
    public class BlockedUser
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public required string BlockerUserId { get; set; }

        [Required]
        public required string BlockedUserId { get; set; }

        [Required]
        public DateTime BlockedAt { get; set; } = DateTime.UtcNow;

        public string? Reason { get; set; }

        // Yeni eklenen özellikler
        public DateTime? BlockExpiresAt { get; set; }
        public bool IsPermanent { get; set; } = true;
        public int BlockCount { get; set; } = 1;
        public DateTime? LastUnblockedAt { get; set; }
        public string? UnblockReason { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        [ForeignKey("BlockerUserId")]
        public virtual required User BlockerUser { get; set; }

        [ForeignKey("BlockedUserId")]
        public virtual required User BlockedUserEntity { get; set; }

        public bool IsExpired => BlockExpiresAt.HasValue && BlockExpiresAt.Value < DateTime.UtcNow;
        public bool IsCurrentlyBlocked => IsActive && !IsExpired;
    }
} 