using System;

namespace WebApplication1.Models
{
    public class Notification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; }
        public string MessageId { get; set; }
        public string Type { get; set; }
        public bool Status { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public void SendNotification()
        {
            // Not implemented
        }

        public void MarkAsRead() => Status = true;
    }
} 