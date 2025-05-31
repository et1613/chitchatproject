using System.ComponentModel;

namespace WebApplication1.Models.Enums
{
    public enum MessageType
    {
        [Description("Metin")]
        Text,

        [Description("Resim")]
        Image,

        [Description("Dosya")]
        File,

        [Description("Ses")]
        Voice,

        [Description("Video")]
        Video,

        [Description("Konum")]
        Location,

        [Description("Kişi")]
        Contact,

        [Description("Sistem")]
        System
    }
} 