using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Threading.Tasks;
using WebApplication1.Models.Messages;

namespace WebApplication1.Models.Chat
{
    public class Attachment
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public required string MessageId { get; set; }

        public string? Url { get; set; }

        [Required]
        public required string FileName { get; set; }

        [Required]
        public required string FileType { get; set; }

        public long FileSize { get; set; }

        [Required]
        public required string UploadedBy { get; set; }

        public string? ThumbnailUrl { get; set; }

        [Required]
        public required string MimeType { get; set; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public DateTime? DeletedAt { get; set; }

        public string? DeletedBy { get; set; }

        public bool IsDeleted => DeletedAt.HasValue;

        [Column(TypeName = "jsonb")]
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        // Navigation property
        [ForeignKey("MessageId")]
        public required Message Message { get; set; }

        public bool IsImage() => MimeType.StartsWith("image/");
        public bool IsVideo() => MimeType.StartsWith("video/");
        public bool IsAudio() => MimeType.StartsWith("audio/");
        public bool IsDocument() => MimeType.StartsWith("application/");

        public void Delete(string userId)
        {
            DeletedAt = DateTime.UtcNow;
            DeletedBy = userId;
        }

        public bool IsValidFileType()
        {
            var allowedTypes = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", ".txt" };
            return allowedTypes.Contains(FileType.ToLower());
        }

        public bool IsWithinSizeLimit()
        {
            const long maxSize = 10 * 1024 * 1024; // 10MB
            return FileSize <= maxSize;
        }
    }
} 