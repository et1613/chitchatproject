using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Enums;
using WebApplication1.Models.Messages;
using WebApplication1.Models.Users;

namespace WebApplication1.Models.Notifications
{
    public class Notification
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [ForeignKey("User")]
        public required string UserId { get; set; }

        [Required]
        public required virtual User User { get; set; }

        public string? MessageId { get; set; }
        public virtual Message? Message { get; set; }

        public string? ChatRoomId { get; set; }
        public virtual ChatRoom? ChatRoom { get; set; }

        [Required]
        public NotificationType Type { get; set; }

        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Data { get; set; } // JSON formatında ek veri

        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiresAt { get; set; }

        [Required]
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

        public string? GroupId { get; set; }
        public int GroupCount { get; set; } = 1;

        public void MarkAsRead()
        {
            if (!IsRead)
            {
                IsRead = true;
                ReadAt = DateTime.UtcNow;
            }
        }

        public void Delete()
        {
            if (!IsDeleted)
            {
                IsDeleted = true;
                DeletedAt = DateTime.UtcNow;
            }
        }

        public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

        public bool Status { get; internal set; }

        public string GetNotificationTitle()
        {
            return Title ?? Type switch
            {
                NotificationType.MessageReceived => "Yeni Mesaj",
                NotificationType.MessageRead => "Mesaj Okundu",
                NotificationType.MessageEdited => "Mesaj Düzenlendi",
                NotificationType.MessageDeleted => "Mesaj Silindi",
                NotificationType.MessageDeletedForYou => "Mesaj Sizin İçin Silindi",
                NotificationType.MessageReplied => "Mesajınıza Yanıt Verildi",
                NotificationType.MessageStatusChanged => "Mesaj Durumu Değişti",
                NotificationType.FriendRequest => "Yeni Arkadaşlık İsteği",
                NotificationType.FriendRequestAccepted => "Arkadaşlık İsteği Kabul Edildi",
                NotificationType.FriendRequestRejected => "Arkadaşlık İsteği Reddedildi",
                NotificationType.UserMentioned => "Kullanıcı Sizi Etiketledi",
                NotificationType.ChatRoomCreated => "Yeni Sohbet Odası",
                NotificationType.ChatRoomJoined => "Sohbet Odasına Katılındı",
                NotificationType.ChatRoomLeft => "Sohbet Odasından Çıkıldı",
                NotificationType.SystemMessage => "Sistem Bildirimi",
                _ => "Bildirim"
            };
        }

        public string GetNotificationContent()
        {
            if (!string.IsNullOrEmpty(Content))
                return Content;

            var baseContent = Type switch
            {
                NotificationType.MessageReceived => $"{Message?.Sender?.UserName ?? "Bir kullanıcı"} size mesaj gönderdi",
                NotificationType.MessageRead => $"{Message?.Sender?.UserName ?? "Bir kullanıcı"} mesajınızı okudu",
                NotificationType.MessageEdited => $"{Message?.Sender?.UserName ?? "Bir kullanıcı"} mesajı düzenledi",
                NotificationType.MessageDeleted => $"{Message?.Sender?.UserName ?? "Bir kullanıcı"} mesajı sildi",
                NotificationType.MessageDeletedForYou => "Bir mesaj sizin için silindi",
                NotificationType.MessageReplied => $"{Message?.Sender?.UserName ?? "Bir kullanıcı"} mesajınıza yanıt verdi",
                NotificationType.MessageStatusChanged => "Mesaj durumu değişti",
                NotificationType.FriendRequest => $"{Message?.Sender?.UserName ?? "Bir kullanıcı"} size arkadaşlık isteği gönderdi",
                NotificationType.FriendRequestAccepted => $"{Message?.Sender?.UserName ?? "Bir kullanıcı"} arkadaşlık isteğinizi kabul etti",
                NotificationType.FriendRequestRejected => $"{Message?.Sender?.UserName ?? "Bir kullanıcı"} arkadaşlık isteğinizi reddetti",
                NotificationType.UserMentioned => $"{Message?.Sender?.UserName ?? "Bir kullanıcı"} sizi etiketledi",
                NotificationType.ChatRoomCreated => "Yeni bir sohbet odası oluşturuldu",
                NotificationType.ChatRoomJoined => "Bir sohbet odasına katıldınız",
                NotificationType.ChatRoomLeft => "Bir sohbet odasından çıktınız",
                NotificationType.SystemMessage => "Sistem bildirimi",
                _ => "Bildirim"
            };

            if (GroupCount > 1)
            {
                baseContent = $"{GroupCount} yeni {baseContent.ToLower()}";
            }

            return baseContent;
        }

        public override string ToString()
        {
            return $"[{Type}] {GetNotificationTitle()} - {GetNotificationContent()}";
        }
    }
} 