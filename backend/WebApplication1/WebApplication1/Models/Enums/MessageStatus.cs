using System.ComponentModel;

namespace WebApplication1.Models.Enums
{
    public enum MessageStatus
    {
        [Description("Gönderildi")]
        Sent,       // Mesaj gönderildi

        [Description("İletildi")]
        Delivered,  // Mesaj alıcıya iletildi

        [Description("Okundu")]
        Read        // Mesaj okundu
    }
} 