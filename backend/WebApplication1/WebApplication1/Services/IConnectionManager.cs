using System.Net.WebSockets;
using WebApplication1.Models.Users;

namespace WebApplication1.Services
{
    public interface IConnectionManager
    {
        bool AddClient(string userId, WebSocket webSocket, string? ipAddress);
        Task RemoveClientAsync(string userId);
        void RemoveClient(string userId);
        Task<ConnectionStatus> GetConnectionStatusAsync(string userId);
        Task<IEnumerable<ActiveUser>> GetActiveUsersAsync();
        Task<UserStatus> GetUserStatusAsync(string userId);
        Task<IEnumerable<UserSession>> GetUserSessionsAsync(string userId);
        Task<bool> RevokeSessionAsync(string sessionId, string userId);
        Task<IEnumerable<UserSession>> GetActiveSessionsAsync(string userId);
        Task BroadcastMessageAsync(string message, string? type);
        Task SendNotificationAsync(string userId, string message, string? type);
        Task SendMessageToClient(string userId, string message);
        WebSocket? GetClient(string userId);
        bool IsUserConnected(string userId);
        Task CleanupInactiveConnectionsAsync();
        int GetActiveConnectionCount();
        IEnumerable<string> GetConnectedUserIds();
    }
} 