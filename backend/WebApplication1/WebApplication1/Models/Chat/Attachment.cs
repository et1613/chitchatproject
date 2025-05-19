using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Threading.Tasks;
using WebApplication1.Models.Messages;
using WebApplication1.Services;

namespace WebApplication1.Models.Chat
{
    public class Attachment(IStorageService storageService)
    {
        private readonly IStorageService _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MessageId { get; set; } = string.Empty;
        public virtual Message? Message { get; set; }
        public string FileType { get; set; } = string.Empty;
        public string? FileUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        public async Task UploadAttachment(Stream fileStream, string fileName)
        {
            if (fileStream == null || string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File stream and file name are required");

            var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
            FileType = Path.GetExtension(fileName).ToLower();
            FileName = fileName;
            FileSize = fileStream.Length;
            FileUrl = await _storageService.UploadFileAsync(fileStream, uniqueFileName);

        }

        public async Task DeleteAttachment()
        {
            if (IsDeleted) return;

            if (!string.IsNullOrEmpty(FileUrl))
            {
                await _storageService.DeleteFileAsync(FileUrl);
            }

            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
            FileUrl = null;

        }

        public bool IsValidFileType()
        {
            var allowedTypes = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", ".txt" };
            return allowedTypes.Contains(FileType.ToLower());
        }

        public bool IsWithinSizeLimit()
        {
            const long maxSize = 10 * 1024 * 1024;
            return FileSize <= maxSize;
        }
    }
} 