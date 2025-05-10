using System;

namespace WebApplication1.Models
{
    public class Attachment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MessageId { get; set; }
        public string FileType { get; set; }
        public string FileUrl { get; set; }
        public long FileSize { get; set; }

        public void UploadAttachment()
        {
            // Not implemented
        }

        public void DeleteAttachment()
        {
            // Not implemented
        }
    }
} 