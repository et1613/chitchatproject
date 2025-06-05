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

        public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
        public DateTime? RespondedAt { get; set; }
        public string ResponseMessage { get; set; } = string.Empty;
        public bool IsBlocked { get; set; } = false;
        public DateTime? BlockedAt { get; set; }
        public string BlockReason { get; set; } = string.Empty;

        public void Accept()
        {
            if (RespondedAt.HasValue)
                throw new InvalidOperationException("Bu arkadaşlık isteği zaten yanıtlanmış");

            Status = FriendRequestStatus.Accepted;
            RespondedAt = DateTime.UtcNow;
        }

        public void Reject(string? reason = null)
        {
            if (RespondedAt.HasValue)
                throw new InvalidOperationException("Bu arkadaşlık isteği zaten yanıtlanmış");

            Status = FriendRequestStatus.Rejected;
            ResponseMessage = reason ?? string.Empty; // Ensure a non-null value is assigned
            RespondedAt = DateTime.UtcNow;
        }


        public void Block(string blockedByUserId)
        {
            if (!IsBlocked)
            {
                IsBlocked = true;
                BlockedAt = DateTime.UtcNow;
                BlockReason = blockedByUserId;
            }
        }

        public void Unblock()
        {
            if (IsBlocked)
            {
                IsBlocked = false;
                BlockedAt = null;
                BlockReason = string.Empty; // Use an empty string instead of null
            }
        }


        public bool IsExpired => !RespondedAt.HasValue && (DateTime.UtcNow - CreatedAt).TotalDays > 30;

        public override string ToString()
        {
            return $"{Sender?.UserName ?? SenderId} -> {Receiver?.UserName ?? ReceiverId} ({CreatedAt:g})";
        }
    }
} 