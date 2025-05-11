using System.ComponentModel;

namespace WebApplication1.Models.Enums
{
    public enum EditType
    {
        [Description("İçerik Değişikliği")]
        ContentChange,    // Mesaj içeriği değiştirildi

        [Description("Medya Ekleme")]
        MediaAdded,       // Medya eklendi

        [Description("Medya Silme")]
        MediaRemoved,     // Medya silindi

        [Description("Medya Değişikliği")]
        MediaChanged,     // Medya değiştirildi

        [Description("Format Değişikliği")]
        FormatChange,     // Metin formatı değiştirildi

        [Description("Link Değişikliği")]
        LinkChange,       // Link değiştirildi

        [Description("Mention Değişikliği")]
        MentionChange     // Mention değiştirildi
    }
} 