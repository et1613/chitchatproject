using System;
using System.Collections.Generic;
using WebApplication1.Models.Enums;
using WebApplication1.Services;
using System.Text.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.Notifications
{
    public class NotificationTemplate
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Content { get; set; } = null!;
        public NotificationType Type { get; set; }

        [Column(TypeName = "json")]
        public string ParametersJson { get; set; } = JsonSerializer.Serialize(new Dictionary<string, string>());

        [NotMapped]
        public Dictionary<string, string> Parameters
        {
            get => JsonSerializer.Deserialize<Dictionary<string, string>>(ParametersJson) ?? new Dictionary<string, string>();
            set => ParametersJson = JsonSerializer.Serialize(value);
        }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
} 