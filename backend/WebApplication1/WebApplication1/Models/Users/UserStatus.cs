using System;
using System.ComponentModel;
using WebApplication1.Models.Enums;

namespace WebApplication1.Models.Users
{
    public enum UserStatus
    {
        [Description("Çevrimdışı")]
        Offline,        // Çevrimdışı

        [Description("Çevrimiçi")]
        Online,        // Çevrimiçi

        [Description("Uzakta")]
        Away,          // Uzakta

        [Description("Meşgul")]
        Busy,          // Meşgul

        [Description("Görünmez")]
        Invisible,     // Görünmez

        [Description("Rahatsız Etmeyin")]
        DoNotDisturb   // Rahatsız Etmeyin
    }

    public static class UserStatusExtensions
    {
        public static string GetDescription(this UserStatus status)
        {
            var field = status.GetType().GetField(status.ToString());
            if (field == null)
                return status.ToString();

            var attribute = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
            return attribute?.Description ?? status.ToString();

        }

        public static string GetStatusMessage(this UserStatus status)
        {
            return status switch
            {
                UserStatus.Offline => "Kullanıcı şu anda çevrimdışı",
                UserStatus.Online => "Kullanıcı şu anda çevrimiçi",
                UserStatus.Away => "Kullanıcı şu anda uzakta",
                UserStatus.Busy => "Kullanıcı şu anda meşgul",
                UserStatus.Invisible => "Kullanıcının durumu gizli",
                UserStatus.DoNotDisturb => "Kullanıcı şu anda rahatsız edilmek istemiyor",
                _ => "Durum bilinmiyor"
            };
        }

        public static TimeSpan GetMinimumDuration(this UserStatus status)
        {
            return status switch
            {
                UserStatus.Offline => TimeSpan.Zero,
                UserStatus.Online => TimeSpan.FromMinutes(1),
                UserStatus.Away => TimeSpan.FromMinutes(5),
                UserStatus.Busy => TimeSpan.FromMinutes(15),
                UserStatus.Invisible => TimeSpan.Zero,
                UserStatus.DoNotDisturb => TimeSpan.FromMinutes(30),
                _ => TimeSpan.Zero
            };
        }

        public static bool CanChangeStatus(this UserStatus currentStatus, UserStatus newStatus, UserRole userRole)
        {
            // Süper admin her duruma geçebilir
            if (userRole == UserRole.SuperAdmin)
                return true;

            // Görünmez durumu sadece admin ve üstü kullanabilir
            if (newStatus == UserStatus.Invisible && userRole < UserRole.Admin)
                return false;

            // Rahatsız Etmeyin durumu sadece moderatör ve üstü kullanabilir
            if (newStatus == UserStatus.DoNotDisturb && userRole < UserRole.Moderator)
                return false;

            // Çevrimdışı durumuna herkes geçebilir
            if (newStatus == UserStatus.Offline)
                return true;

            // Diğer durumlar için minimum süre kontrolü
            var minDuration = currentStatus.GetMinimumDuration();
            return minDuration == TimeSpan.Zero;
        }

        public static bool ShouldNotifyStatusChange(this UserStatus status)
        {
            return status switch
            {
                UserStatus.Offline => true,
                UserStatus.Online => true,
                UserStatus.Away => false,
                UserStatus.Busy => true,
                UserStatus.Invisible => false,
                UserStatus.DoNotDisturb => true,
                _ => false
            };
        }
    }
} 