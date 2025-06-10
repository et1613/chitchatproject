using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using WebApplication1.Models.Users;

namespace WebApplication1.Services
{
    public interface IConnectionManager
    {
        bool AddClient(string userId, WebSocket webSocket, string? ipAddress);
        Task RemoveClientAsync(string userId);
        void RemoveClient(string userId);
        UserStatus GetConnectionStatusAsync(string userId);
        IEnumerable<UserStatus> GetActiveUsersAsync();
        UserStatus GetUserStatusAsync(string userId);
        IEnumerable<UserStatus> GetUserSessionsAsync(string userId);
        Task<bool> RevokeSessionAsync(string sessionId, string userId);
        IEnumerable<UserStatus> GetActiveSessionsAsync(string userId);
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