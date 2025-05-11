using System;
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
        public string MessageId { get; set; }
        public virtual Message Message { get; set; }

        [Required]
        public string FileType { get; set; }

        [Required]
        public string FileUrl { get; set; }

        public long FileSize { get; set; }
        public string FileName { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        public async Task UploadAttachment(Stream fileStream, string fileName)
        {
            if (fileStream == null || string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File stream and file name are required");

            // Generate unique file name
            var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
            
            // Determine file type from extension
            FileType = Path.GetExtension(fileName).ToLower();
            FileName = fileName;
            FileSize = fileStream.Length;

            // Upload to storage (example using a hypothetical storage service)
            FileUrl = await StorageService.UploadFileAsync(fileStream, uniqueFileName);

            // Update message if it exists
            if (Message != null)
            {
                Message.Attachments.Add(this);
            }
        }

        public async Task DeleteAttachment()
        {
            if (IsDeleted) return;

            // Delete from storage
            if (!string.IsNullOrEmpty(FileUrl))
            {
                await StorageService.DeleteFileAsync(FileUrl);
            }

            // Update status
            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
            FileUrl = null;

            // Remove from message if it exists
            if (Message != null)
            {
                Message.Attachments.Remove(this);
            }
        }

        public bool IsValidFileType()
        {
            // Define allowed file types
            var allowedTypes = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", ".txt" };
            return allowedTypes.Contains(FileType.ToLower());
        }

        public bool IsWithinSizeLimit()
        {
            // Define maximum file size (e.g., 10MB)
            const long maxSize = 10 * 1024 * 1024;
            return FileSize <= maxSize;
        }
    }
} 