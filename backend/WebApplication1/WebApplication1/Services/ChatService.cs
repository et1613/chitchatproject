using System.Collections.Generic;
using System.Linq;
using WebApplication1.Models;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Messages;
using WebApplication1.Models.Users;

namespace WebApplication1.Services
{
    public class ChatService
    {
        public static List<ChatRoom> ChatRooms = new();

        public static ChatRoom GetChatRoomById(string id) => ChatRooms.FirstOrDefault(c => c.Id == id);

        public static void SendDirectMessage(User sender, string receiverId, string content)
        {
            var receiver = UserService.GetUserById(receiverId);
            if (receiver == null || sender.BlockedUsers.Contains(receiver)) return;

            var message = new Message
            {
                SenderId = sender.Id,
                ReceiverId = receiverId,
                Content = content,
                Timestamp = System.DateTime.UtcNow
            };

            // Not storing, notifying only (in-memory example)
            NotificationService.NotifyUser(receiverId, $"New message from {sender.UserName}: {content}");
        }
    }
} 