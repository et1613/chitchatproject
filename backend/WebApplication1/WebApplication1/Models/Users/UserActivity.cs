using Microsoft.VisualStudio.Services.Users;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebApplication1.Models.Enums;
using static Microsoft.VisualStudio.Services.Graph.GraphResourceIds;

namespace WebApplication1.Models.Users
{
    public class UserActivity
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public required string UserId { get; set; }

        [Required]
        public required string ActivityType { get; set; }

        public string? Description { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? Location { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsSuccessful { get; set; } = true;
        public string? ErrorMessage { get; set; }
        public string? RelatedEntityId { get; set; }
        public string? RelatedEntityType { get; set; }

        [ForeignKey("UserId")]
        public virtual required User User { get; set; }

        public static UserActivity CreateLogin(User user, string ipAddress, string userAgent, bool isSuccessful, string? errorMessage = null)
        {
            return new UserActivity
            {
                UserId = user.Id,
                ActivityType = "Login",
                Description = isSuccessful ? "Başarılı giriş" : "Başarısız giriş denemesi",
                IpAddress = ipAddress,
                UserAgent = userAgent,
                IsSuccessful = isSuccessful,
                ErrorMessage = errorMessage,
                User = user
            };
        }


        public static UserActivity CreateLogout(User user, string ipAddress)
        {
            return new UserActivity
            {
                UserId = user.Id,
                ActivityType = "Logout",
                Description = "Çıkış yapıldı",
                IpAddress = ipAddress,
                User = user
            };
        }

        public static UserActivity CreateProfileUpdate(User user, string description)
        {
            return new UserActivity
            {
                UserId = user.Id,
                ActivityType = "ProfileUpdate",
                Description = description,
                User = user
            };
        }

        public static UserActivity CreateSecurityChange(User user, string description)
        {
            return new UserActivity
            {
                UserId = user.Id,
                ActivityType = "SecurityChange",
                Description = description,
                User = user
            };
        }

        public static UserActivity CreateMessageAction(User user, string actionType, string messageId)
        {
            return new UserActivity
            {
                UserId = user.Id,
                ActivityType = actionType,
                Description = $"{actionType} işlemi gerçekleştirildi",
                RelatedEntityId = messageId,
                RelatedEntityType = "Message",
                User = user
            };
        }

    }
} 