using System;
using System.ComponentModel.DataAnnotations;
using WebApplication1.Models.Enums;

namespace WebApplication1.Models.Notifications
{
    public class NotificationTemplate
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public NotificationType Type { get; set; }

        [Required]
        public string TitleTemplate { get; set; }

        [Required]
        public string ContentTemplate { get; set; }

        public string? Icon { get; set; }
        public string? Color { get; set; }
        public NotificationPriority DefaultPriority { get; set; } = NotificationPriority.Normal;

        public TimeSpan? DefaultExpiration { get; set; }

        public bool IsActive { get; set; } = true;

        public string FormatTitle(params object[] args)
        {
            try
            {
                return string.Format(TitleTemplate, args);
            }
            catch
            {
                return TitleTemplate;
            }
        }

        public string FormatContent(params object[] args)
        {
            try
            {
                return string.Format(ContentTemplate, args);
            }
            catch
            {
                return ContentTemplate;
            }
        }

        public Notification CreateNotification(string userId, params object[] args)
        {
            return new Notification
            {
                UserId = userId,
                Type = Type,
                Title = FormatTitle(args),
                Content = FormatContent(args),
                Priority = (int)DefaultPriority,
                ExpiresAt = DefaultExpiration.HasValue ? DateTime.UtcNow.Add(DefaultExpiration.Value) : null
            };
        }
    }
} 