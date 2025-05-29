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

namespace WebApplication1.Services
{
    public class ConnectionInfo
    {
        public required WebSocket Socket { get; set; }
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        public string? IpAddress { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public int MessagesSent { get; set; } = 0;
        public int MessagesReceived { get; set; } = 0;
    }

    public class ConnectionManager
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

        public async Task AddClientAsync(string userId, WebSocket socket)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }
            if (socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }

            try
            {
                await _semaphore.WaitAsync();
                try
                {
                    if (_connections.TryGetValue(userId, out var existingConnection))
                    {
                        try
                        {
                            if (existingConnection.Socket.State == WebSocketState.Open)
                            {
                                await existingConnection.Socket.CloseAsync(
                                    WebSocketCloseStatus.NormalClosure,
                                    "New connection established",
                                    CancellationToken.None);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error closing existing connection for user {UserId}", userId);
                        }
                    }

                    var connectionInfo = new ConnectionInfo
                    {
                        Socket = socket,
                        ConnectedAt = DateTime.UtcNow,
                        LastActivity = DateTime.UtcNow
                    };

                    _connections.AddOrUpdate(userId, connectionInfo, (_, _) => connectionInfo);
                    _logger.LogInformation("New WebSocket connection added for user {UserId}", userId);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding WebSocket connection for user {UserId}", userId);
                throw;
            }
        }

        public async Task RemoveClientAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }

            try
            {
                await _semaphore.WaitAsync();
                try
                {
                    if (_connections.TryRemove(userId, out var connectionInfo))
                    {
                        try
                        {
                            if (connectionInfo.Socket.State == WebSocketState.Open)
                            {
                                await connectionInfo.Socket.CloseAsync(
                                    WebSocketCloseStatus.NormalClosure,
                                    "Connection removed",
                                    CancellationToken.None);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error closing WebSocket connection for user {UserId}", userId);
                        }
                    }
                    _logger.LogInformation("WebSocket connection removed for user {UserId}", userId);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing WebSocket connection for user {UserId}", userId);
                throw;
            }
        }

        public WebSocket? GetClient(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }

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
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }

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

        public async Task SendNotificationAsync(Notification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }
            if (string.IsNullOrEmpty(notification.UserId))
            {
                throw new ArgumentException("Notification user ID cannot be null or empty");
            }

            try
            {
                if (_connections.TryGetValue(notification.UserId, out var connectionInfo))
                {
                    if (connectionInfo.Socket.State == WebSocketState.Open)
                    {
                        var notificationJson = System.Text.Json.JsonSerializer.Serialize(notification);
                        var notificationBytes = Encoding.UTF8.GetBytes(notificationJson);

                        if (notificationBytes.Length > MaxMessageSize)
                        {
                            _logger.LogWarning("Notification message too large for user {UserId}", notification.UserId);
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
                        _connections.TryRemove(notification.UserId, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user {UserId}", notification.UserId);
                throw;
            }
        }

        public async Task BroadcastToAllAsync(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentException("Message cannot be null or empty", nameof(message));
            }

            try
            {
                var messageBytes = Encoding.UTF8.GetBytes(message);
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
                _logger.LogError(ex, "Error broadcasting message to all clients");
                throw;
            }
        }

        public async Task BroadcastToGroupAsync(IEnumerable<string> userIds, string message)
        {
            if (userIds == null)
            {
                throw new ArgumentNullException(nameof(userIds));
            }
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentException("Message cannot be null or empty", nameof(message));
            }

            try
            {
                var messageBytes = Encoding.UTF8.GetBytes(message);
                if (messageBytes.Length > MaxMessageSize)
                {
                    _logger.LogWarning("Group broadcast message too large");
                    return;
                }

                var tasks = userIds
                    .Where(userId => !string.IsNullOrEmpty(userId))
                    .Where(userId => _connections.TryGetValue(userId, out var connectionInfo) && 
                                   connectionInfo.Socket.State == WebSocketState.Open)
                    .Select(userId => _connections[userId])
                    .Select(async connectionInfo =>
                    {
                        try
                        {
                            await connectionInfo.Socket.SendAsync(
                                new ArraySegment<byte>(messageBytes),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None);

                            connectionInfo.LastActivity = DateTime.UtcNow;
                            connectionInfo.MessagesSent++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error broadcasting message to a client in group");
                        }
                    });

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting message to group");
                throw;
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
