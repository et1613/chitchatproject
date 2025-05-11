using System.ComponentModel;

namespace WebApplication1.Models.Enums
{
    public enum NotificationPriority
    {
        [Description("Düşük")]
        Low,        // Düşük öncelikli bildirimler

        [Description("Normal")]
        Normal,     // Normal öncelikli bildirimler

        [Description("Yüksek")]
        High,       // Yüksek öncelikli bildirimler

        [Description("Acil")]
        Urgent      // Acil bildirimler
    }
} 