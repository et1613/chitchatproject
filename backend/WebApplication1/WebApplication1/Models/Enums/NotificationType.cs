using System.ComponentModel;

namespace WebApplication1.Models.Enums
{
    public enum NotificationType
    {
        [Description("Yeni Mesaj")]
        NewMessage,             // Yeni mesaj bildirimi

        [Description("Arkadaşlık İsteği")]
        FriendRequest,         // Arkadaşlık isteği bildirimi

        [Description("Arkadaşlık Kabul")]
        FriendRequestAccepted, // Arkadaşlık isteği kabul bildirimi

        [Description("Arkadaşlık Red")]
        FriendRequestRejected, // Arkadaşlık isteği red bildirimi

        [Description("Mesaj Okundu")]
        MessageRead,           // Mesaj okundu bildirimi

        [Description("Mesaj Düzenlendi")]
        MessageEdited,         // Mesaj düzenlendi bildirimi

        [Description("Mesaj Silindi")]
        MessageDeleted,        // Mesaj silindi bildirimi

        [Description("Sistem Bildirimi")]
        SystemNotification,    // Sistem bildirimi

        [Description("Grup Daveti")]
        GroupInvitation,       // Grup daveti bildirimi

        [Description("Grup Güncelleme")]
        GroupUpdate,           // Grup güncelleme bildirimi

        [Description("Mention")]
        Mention,                // Mention bildirimi
        MessageReceived,
        MessageDeletedForYou,
        MessageReplied,
        MessageStatusChanged,
        UserMentioned,
        ChatRoomCreated,
        ChatRoomJoined,
        ChatRoomLeft,
        SystemMessage
    }
} 