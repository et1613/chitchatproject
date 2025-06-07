using System.Collections.Generic;
using System.Threading.Tasks;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Messages;
using WebApplication1.Models.Users;
using System.Net.WebSockets;

namespace WebApplication1.Services
{
    public interface IChatService
    {
        Task SendDirectMessage(User sender, string receiverId, string content);
        Task<Message> SendMessageAsync(string senderId, string chatRoomId, string content);
        Task<List<Message>> GetChatHistoryAsync(string chatRoomId, string userId, int skip = 0, int take = 50);
        Task<bool> DeleteMessageAsync(string messageId, string userId);
        Task<bool> EditMessageAsync(string messageId, string userId, string newContent);
        Task HandleWebSocketConnection(WebSocket webSocket, string userId);
        Task BroadcastMessageToRoom(string chatRoomId, string message, string senderId);
        Task<ChatRoom> CreateChatRoomAsync(string name, string? description, string creatorId);
        Task<ChatRoom?> GetChatRoomAsync(string chatRoomId);
        Task<IEnumerable<ChatRoom>> GetUserChatRoomsAsync(string userId);
        Task<bool> AddUserToChatRoomAsync(string userId, string chatRoomId);
        Task<bool> RemoveUserFromChatRoomAsync(string userId, string chatRoomId);
        Task<IEnumerable<Message>> GetChatRoomMessagesAsync(string chatRoomId, int skip = 0, int take = 50);
        Task<bool> MarkMessageAsReadAsync(string messageId);
        Task<bool> UpdateMessageAsync(string messageId, string content, string userId);
    }
} 