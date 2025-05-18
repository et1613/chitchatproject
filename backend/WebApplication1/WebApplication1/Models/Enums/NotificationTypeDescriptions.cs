namespace WebApplication1.Models.Enums
{
    public static class NotificationTypeDescriptions
    {
        public static string GetDescription(NotificationType notificationType)
        {
            return notificationType switch
            {
                NotificationType.NewMessage => "Yeni Mesaj",
                NotificationType.FriendRequest => "Arkadaşlık İsteği",
                NotificationType.FriendRequestAccepted => "Arkadaşlık Kabul",
                NotificationType.FriendRequestRejected => "Arkadaşlık Red",
                NotificationType.MessageRead => "Mesaj Okundu",
                NotificationType.MessageEdited => "Mesaj Düzenlendi",
                NotificationType.MessageDeleted => "Mesaj Silindi",
                NotificationType.SystemNotification => "Sistem Bildirimi",
                NotificationType.GroupInvitation => "Grup Daveti",
                NotificationType.GroupUpdate => "Grup Güncelleme",
                NotificationType.Mention => "Mention",
                _ => "Bildirim"
            };
        }
    }
}
