using WebApplication1.Models.Chat;
using WebApplication1.Models.Messages;

namespace WebApplication1.Services
{
    public interface IChatService
    {
        Task<Message> SendMessageAsync(string userId, string chatRoomId, string content);
        Task<ChatRoom> CreateChatRoomAsync(string name, string description, string creatorId);
        Task<ChatRoom> GetChatRoomAsync(string chatRoomId);
        Task<IEnumerable<ChatRoom>> GetUserChatRoomsAsync(string userId);
        Task<bool> AddUserToChatRoomAsync(string userId, string chatRoomId);
        Task<bool> RemoveUserFromChatRoomAsync(string userId, string chatRoomId);
        Task<IEnumerable<Message>> GetChatRoomMessagesAsync(string chatRoomId, int skip = 0, int take = 50);
        Task<bool> MarkMessageAsReadAsync(string messageId);
        Task<bool> DeleteMessageAsync(string messageId, string userId);
        Task<bool> UpdateMessageAsync(string messageId, string content, string userId);
    }
} 