using System;
using System.Collections.Generic;
using WebApplication1.Models.Enums;
using WebApplication1.Services;

namespace WebApplication1.Models.Notifications
{
    public class NotificationTemplate
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Content { get; set; } = null!;
        public NotificationType Type { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
} 