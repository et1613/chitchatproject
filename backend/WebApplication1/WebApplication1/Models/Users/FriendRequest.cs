using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebApplication1.Models.Enums;

namespace WebApplication1.Models.Users
{
    public class FriendRequest
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public required string SenderId { get; set; }
        public required virtual User Sender { get; set; }

        [Required]
        public required string ReceiverId { get; set; }
        public required virtual User Receiver { get; set; }

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public DateTime? RespondedAt { get; set; }
        public bool Accepted { get; set; }
        public string? RejectionReason { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime? BlockedAt { get; set; }
        public string? BlockedByUserId { get; set; }
        public virtual User? BlockedByUser { get; set; }

        public void Accept()
        {
            if (RespondedAt.HasValue)
                throw new InvalidOperationException("Bu arkadaşlık isteği zaten yanıtlanmış");

            Accepted = true;
            RespondedAt = DateTime.UtcNow;
        }

        public void Reject(string? reason = null)
        {
            if (RespondedAt.HasValue)
                throw new InvalidOperationException("Bu arkadaşlık isteği zaten yanıtlanmış");

            Accepted = false;
            RejectionReason = reason;
            RespondedAt = DateTime.UtcNow;
        }

        public void Block(string blockedByUserId)
        {
            if (!IsBlocked)
            {
                IsBlocked = true;
                BlockedAt = DateTime.UtcNow;
                BlockedByUserId = blockedByUserId;
            }
        }

        public void Unblock()
        {
            if (IsBlocked)
            {
                IsBlocked = false;
                BlockedAt = null;
                BlockedByUserId = null;
            }
        }

        public bool IsExpired => !RespondedAt.HasValue && (DateTime.UtcNow - SentAt).TotalDays > 30;

        public override string ToString()
        {
            return $"{Sender?.UserName ?? SenderId} -> {Receiver?.UserName ?? ReceiverId} ({SentAt:g})";
        }
    }
} 