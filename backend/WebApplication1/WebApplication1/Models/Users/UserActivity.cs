using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebApplication1.Models.Enums;

namespace WebApplication1.Models.Users
{
    public class UserActivity
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string UserId { get; set; }

        [Required]
        public string ActivityType { get; set; }

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
        public virtual User User { get; set; }

        public static UserActivity CreateLogin(string userId, string ipAddress, string userAgent, bool isSuccessful, string? errorMessage = null)
        {
            return new UserActivity
            {
                UserId = userId,
                ActivityType = "Login",
                Description = isSuccessful ? "Başarılı giriş" : "Başarısız giriş denemesi",
                IpAddress = ipAddress,
                UserAgent = userAgent,
                IsSuccessful = isSuccessful,
                ErrorMessage = errorMessage
            };
        }

        public static UserActivity CreateLogout(string userId, string ipAddress)
        {
            return new UserActivity
            {
                UserId = userId,
                ActivityType = "Logout",
                Description = "Çıkış yapıldı",
                IpAddress = ipAddress
            };
        }

        public static UserActivity CreateProfileUpdate(string userId, string description)
        {
            return new UserActivity
            {
                UserId = userId,
                ActivityType = "ProfileUpdate",
                Description = description
            };
        }

        public static UserActivity CreateSecurityChange(string userId, string description)
        {
            return new UserActivity
            {
                UserId = userId,
                ActivityType = "SecurityChange",
                Description = description
            };
        }

        public static UserActivity CreateMessageAction(string userId, string actionType, string messageId)
        {
            return new UserActivity
            {
                UserId = userId,
                ActivityType = actionType,
                Description = $"{actionType} işlemi gerçekleştirildi",
                RelatedEntityId = messageId,
                RelatedEntityType = "Message"
            };
        }
    }
} 