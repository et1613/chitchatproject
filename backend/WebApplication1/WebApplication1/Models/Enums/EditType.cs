using System.ComponentModel;

namespace WebApplication1.Models.Enums
{
    public enum EditType
    {
        [Description("Mesaj içeriği düzenlendi")]
        ContentChange,

        [Description("Medya eklendi")]
        MediaAdded,

        [Description("Medya silindi")]
        MediaRemoved,

        [Description("Medya değiştirildi")]
        MediaChanged,

        [Description("Mesaj formatı değiştirildi")]
        FormatChange,

        [Description("Link eklendi/kaldırıldı")]
        LinkEdit,

        [Description("Mention değiştirildi")]
        MentionChange,

        [Description("Dosya eklendi/kaldırıldı")]
        AttachmentEdit,

        [Description("Yazım hatası düzeltildi")]
        Correction,

        [Description("Çeviri eklendi")]
        Translation,
        LinkChange
    }
}
