namespace WebApplication1.Models.Enums
{
    public static class EditTypeDescriptions
    {
        public static string GetDescription(EditType editType)
        {
            return editType switch
            {
                EditType.ContentChange => "Mesaj içeriği düzenlendi",
                EditType.FormatChange => "Mesaj formatı değiştirildi",
                EditType.MediaAdded => "Medya eklendi",
                EditType.MediaRemoved => "Medya silindi",
                EditType.MediaChanged => "Medya değiştirildi",
                EditType.LinkChange => "Link değiştirildi",
                EditType.MentionChange => "Mention değiştirildi",
                _ => "Düzenleme yapıldı"
            };
        }
    }
}
