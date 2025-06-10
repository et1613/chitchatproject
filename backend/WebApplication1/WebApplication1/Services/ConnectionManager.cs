using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebApplication1.Models.Notifications;
using WebApplication1.Models.Users;

namespace WebApplication1.Services
{
    public class ConnectionInfo
    {
        public required WebSocket Socket { get; set; }
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        public string? IpAddress { get; set; }
        public string? DeviceInfo { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public int MessagesSent { get; set; } = 0;
        public int MessagesReceived { get; set; } = 0;
    }

    public class ConnectionManager : IConnectionManager
    {
        private readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();
        private readonly ILogger<ConnectionManager> _logger;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private const int MaxMessageSize = 1024 * 4; // 4KB
        private const int MaxConnectionsPerUser = 1;
        private readonly TimeSpan _connectionTimeout = TimeSpan.FromMinutes(30);

        public ConnectionManager(ILogger<ConnectionManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool AddClient(string userId, WebSocket webSocket, string? ipAddress)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            if (webSocket == null)
                throw new ArgumentNullException(nameof(webSocket));

            try
            {
                var connectionInfo = new ConnectionInfo
                {
                    Socket = webSocket,
                    IpAddress = ipAddress,
                    ConnectedAt = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow
                };

                return _connections.TryAdd(userId, connectionInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding WebSocket connection for user {UserId}", userId);
                return false;
            }
        }

        public async Task RemoveClientAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

            try
            {
                if (_connections.TryRemove(userId, out var connectionInfo))
                {
                    if (connectionInfo.Socket.State == WebSocketState.Open)
                    {
                        await connectionInfo.Socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Connection removed",
                            CancellationToken.None);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing WebSocket connection for user {UserId}", userId);
                throw;
            }
        }

        public void RemoveClient(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

            try
            {
                _connections.TryRemove(userId, out _);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing WebSocket connection for user {UserId}", userId);
                throw;
            }
        }

        public UserStatus GetConnectionStatusAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

            try
            {
                if (_connections.TryGetValue(userId, out var connectionInfo))
                {
                    return connectionInfo.Socket.State == WebSocketState.Open ? 
                        UserStatus.Online : UserStatus.Offline;
                }

                return UserStatus.Offline;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting connection status for user {UserId}", userId);
                throw;
            }
        }

        public IEnumerable<UserStatus> GetActiveUsersAsync()
        {
            try
            {
                var activeUsers = _connections
                    .Where(kvp => kvp.Value.Socket.State == WebSocketState.Open)
                    .Select(kvp => UserStatus.Online)
                    .ToList();

                return activeUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active users");
                throw;
            }
        }

        public UserStatus GetUserStatusAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

            try
            {
                if (_connections.TryGetValue(userId, out var connectionInfo))
                {
                    return connectionInfo.Socket.State == WebSocketState.Open ? 
                        UserStatus.Online : UserStatus.Offline;
                }

                return UserStatus.Offline;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user status for user {UserId}", userId);
                throw;
            }
        }

        public IEnumerable<UserStatus> GetUserSessionsAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

            try
            {
                if (_connections.TryGetValue(userId, out var connectionInfo))
                {
                    return new List<UserStatus>
                    {
                        connectionInfo.Socket.State == WebSocketState.Open ? 
                            UserStatus.Online : UserStatus.Offline
                    };
                }

                return Enumerable.Empty<UserStatus>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user sessions for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> RevokeSessionAsync(string sessionId, string userId)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

            try
            {
                if (_connections.TryGetValue(userId, out var connectionInfo))
                {
                    await RemoveClientAsync(userId);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking session {SessionId} for user {UserId}", sessionId, userId);
                throw;
            }
        }

        public IEnumerable<UserStatus> GetActiveSessionsAsync(string userId)
        {
            return GetUserSessionsAsync(userId);
        }

        public async Task BroadcastMessageAsync(string message, string? type)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

            try
            {
                var messageData = new
                {
                    type = type ?? "broadcast",
                    message = message,
                    timestamp = DateTime.UtcNow
                };

                var messageJson = System.Text.Json.JsonSerializer.Serialize(messageData);
                var messageBytes = Encoding.UTF8.GetBytes(messageJson);

                if (messageBytes.Length > MaxMessageSize)
                {
                    _logger.LogWarning("Broadcast message too large");
                    return;
                }

                var tasks = _connections
                    .Where(kvp => kvp.Value.Socket.State == WebSocketState.Open)
                    .Select(async kvp =>
                    {
                        try
                        {
                            await kvp.Value.Socket.SendAsync(
                                new ArraySegment<byte>(messageBytes),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None);

                            kvp.Value.LastActivity = DateTime.UtcNow;
                            kvp.Value.MessagesSent++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error broadcasting message to user {UserId}", kvp.Key);
                            _connections.TryRemove(kvp.Key, out _);
                        }
                    });

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting message");
                throw;
            }
        }

        public async Task SendNotificationAsync(string userId, string message, string? type)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

            try
            {
                if (_connections.TryGetValue(userId, out var connectionInfo))
                {
                    if (connectionInfo.Socket.State == WebSocketState.Open)
                    {
                        var notificationData = new
                        {
                            type = type ?? "notification",
                            message = message,
                            timestamp = DateTime.UtcNow
                        };

                        var notificationJson = System.Text.Json.JsonSerializer.Serialize(notificationData);
                        var notificationBytes = Encoding.UTF8.GetBytes(notificationJson);

                        if (notificationBytes.Length > MaxMessageSize)
                        {
                            _logger.LogWarning("Notification message too large for user {UserId}", userId);
                            return;
                        }

                        await connectionInfo.Socket.SendAsync(
                            new ArraySegment<byte>(notificationBytes),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None);

                        connectionInfo.LastActivity = DateTime.UtcNow;
                        connectionInfo.MessagesSent++;
                    }
                    else
                    {
                        _connections.TryRemove(userId, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user {UserId}", userId);
                throw;
            }
        }

        public async Task SendMessageToClient(string userId, string message)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

            try
            {
                if (_connections.TryGetValue(userId, out var connectionInfo))
                {
                    if (connectionInfo.Socket.State == WebSocketState.Open)
                    {
                        var messageBytes = Encoding.UTF8.GetBytes(message);

                        if (messageBytes.Length > MaxMessageSize)
                        {
                            _logger.LogWarning("Message too large for user {UserId}", userId);
                            return;
                        }

                        await connectionInfo.Socket.SendAsync(
                            new ArraySegment<byte>(messageBytes),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None);

                        connectionInfo.LastActivity = DateTime.UtcNow;
                        connectionInfo.MessagesSent++;
                    }
                    else
                    {
                        _connections.TryRemove(userId, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to user {UserId}", userId);
                throw;
            }
        }

        public WebSocket? GetClient(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

            try
            {
                if (_connections.TryGetValue(userId, out var connectionInfo))
                {
                    if (connectionInfo.Socket.State == WebSocketState.Open)
                    {
                        connectionInfo.LastActivity = DateTime.UtcNow;
                        return connectionInfo.Socket;
                    }
                    else
                    {
                        _connections.TryRemove(userId, out _);
                        return null;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting WebSocket connection for user {UserId}", userId);
                return null;
            }
        }

        public bool IsUserConnected(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

            try
            {
                return _connections.TryGetValue(userId, out var connectionInfo) && 
                       connectionInfo.Socket.State == WebSocketState.Open;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user connection status for user {UserId}", userId);
                return false;
            }
        }

        public async Task CleanupInactiveConnectionsAsync()
        {
            try
            {
                var inactiveConnections = _connections
                    .Where(kvp => DateTime.UtcNow - kvp.Value.LastActivity > _connectionTimeout)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var userId in inactiveConnections)
                {
                    await RemoveClientAsync(userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up inactive connections");
                throw;
            }
        }

        public int GetActiveConnectionCount()
        {
            return _connections.Count(kvp => kvp.Value.Socket.State == WebSocketState.Open);
        }

        public IEnumerable<string> GetConnectedUserIds()
        {
            return _connections
                .Where(kvp => kvp.Value.Socket.State == WebSocketState.Open)
                .Select(kvp => kvp.Key)
                .ToList();
        }
    }
} 
