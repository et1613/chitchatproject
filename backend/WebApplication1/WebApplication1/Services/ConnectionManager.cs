using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Services
{
    public class ConnectionInfo
    {
        public WebSocket Socket { get; set; }
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        public string? IpAddress { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public int MessagesSent { get; set; } = 0;
        public int MessagesReceived { get; set; } = 0;
    }

    public class ConnectionManager
    {
        private readonly ConcurrentDictionary<string, ConnectionInfo> Clients = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> Groups = new();
        private readonly ConcurrentDictionary<string, string> UserTokens = new();
        private readonly int MaxConnectionsPerUser = 1;
        private readonly ILogger<ConnectionManager> _logger;
        private readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new();

        public event EventHandler<string> OnClientConnected;
        public event EventHandler<string> OnClientDisconnected;
        public event EventHandler<(string userId, string message)> OnMessageSent;

        public ConnectionManager(ILogger<ConnectionManager> logger)
        {
            _logger = logger;
        }

        public bool AddClient(string userId, WebSocket connection, string? ipAddress = null, string? token = null)
        {
            if (!ValidateConnection(userId, token))
            {
                _logger.LogWarning("Invalid connection attempt for user {UserId}", userId);
                return false;
            }

            EnforceConnectionLimit(userId);

            var info = new ConnectionInfo { Socket = connection, ConnectedAt = DateTime.UtcNow, IpAddress = ipAddress };
            Clients.AddOrUpdate(userId, info, (key, old) => info);
            OnClientConnected?.Invoke(this, userId);
            _logger.LogInformation("Client connected: {UserId}", userId);
            return true;
        }

        public void RemoveClient(string userId)
        {
            Clients.TryRemove(userId, out _);
            OnClientDisconnected?.Invoke(this, userId);
            _logger.LogInformation("Client disconnected: {UserId}", userId);
        }

        public async Task SendMessageToClient(string userId, string message)
        {
            if (Clients.TryGetValue(userId, out var info))
            {
                var socket = info.Socket;
                if (socket.State == WebSocketState.Open)
                {
                    var buffer = Encoding.UTF8.GetBytes(message);
                    try
                    {
                        await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                        info.MessagesSent++;
                        info.LastActivity = DateTime.UtcNow;
                        OnMessageSent?.Invoke(this, (userId, message));
                        _logger.LogInformation("Message sent to {UserId}: {Message}", userId, message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending message to {UserId}", userId);
                        RemoveClient(userId);
                    }
                }
                else
                {
                    RemoveClient(userId);
                }
            }
        }

        public async Task BroadcastMessage(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            var toRemove = new List<string>();
            foreach (var kvp in Clients)
            {
                var socket = kvp.Value.Socket;
                if (socket.State == WebSocketState.Open)
                {
                    try
                    {
                        await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                        kvp.Value.MessagesSent++;
                        kvp.Value.LastActivity = DateTime.UtcNow;
                        OnMessageSent?.Invoke(this, (kvp.Key, message));
                    }
                    catch
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
                else
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var userId in toRemove)
            {
                RemoveClient(userId);
            }
        }

        public List<string> GetConnectedUserIds()
        {
            return Clients.Keys.ToList();
        }

        public List<ConnectionInfo> GetAllConnections()
        {
            return Clients.Values.ToList();
        }

        public int GetConnectionCount() => Clients.Count;

        public void CleanupClosedConnections()
        {
            var toRemove = Clients.Where(kvp => kvp.Value.Socket.State != WebSocketState.Open)
                                   .Select(kvp => kvp.Key)
                                   .ToList();
            foreach (var userId in toRemove)
            {
                RemoveClient(userId);
            }
        }

        // Chat Odaları (Gruplar)
        public void AddToGroup(string groupId, string userId)
        {
            Groups.AddOrUpdate(groupId, new HashSet<string> { userId }, (key, set) =>
            {
                set.Add(userId);
                return set;
            });
            _logger.LogInformation("User {UserId} added to group {GroupId}", userId, groupId);
        }

        public void RemoveFromGroup(string groupId, string userId)
        {
            if (Groups.TryGetValue(groupId, out var users))
            {
                users.Remove(userId);
                _logger.LogInformation("User {UserId} removed from group {GroupId}", userId, groupId);
            }
        }

        public async Task BroadcastToGroup(string groupId, string message)
        {
            if (Groups.TryGetValue(groupId, out var users))
            {
                foreach (var userId in users)
                {
                    await SendMessageToClient(userId, message);
                }
            }
        }

        // Bağlantı Limitleri
        private void EnforceConnectionLimit(string userId)
        {
            if (Clients.Count(kvp => kvp.Key == userId) >= MaxConnectionsPerUser)
            {
                var oldConnection = Clients.FirstOrDefault(kvp => kvp.Key == userId);
                if (oldConnection.Value != null)
                {
                    RemoveClient(userId);
                    _logger.LogWarning("Connection limit exceeded for user {UserId}, old connection removed", userId);
                }
            }
        }

        // Bağlantı Doğrulama
        private bool ValidateConnection(string userId, string? token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            // Token doğrulama mantığı burada uygulanabilir
            UserTokens[userId] = token;
            return true;
        }

        // Sağlık Kontrolü
        public async Task<bool> PingClient(string userId)
        {
            if (Clients.TryGetValue(userId, out var info))
            {
                var socket = info.Socket;
                if (socket.State == WebSocketState.Open)
                {
                    try
                    {
                        var buffer = Encoding.UTF8.GetBytes("ping");
                        await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                        return true;
                    }
                    catch
                    {
                        RemoveClient(userId);
                        return false;
                    }
                }
            }
            return false;
        }

        // Yedeklilik
        public async Task HandleConnectionFailure(string userId)
        {
            _logger.LogWarning("Connection failure for user {UserId}, attempting reconnection", userId);
            // Yeniden bağlanma mantığı burada uygulanabilir
            await Task.Delay(1000); // Simüle edilmiş yeniden bağlanma
        }

        // Simülasyon
        public async Task SimulateConnection(string userId)
        {
            _logger.LogInformation("Simulating connection for user {UserId}", userId);
            // Simülasyon mantığı burada uygulanabilir
            await Task.Delay(1000); // Simüle edilmiş işlem
        }

        public void AddClient(string userId, string connectionId)
        {
            _userConnections.AddOrUpdate(
                userId,
                new HashSet<string> { connectionId },
                (_, connections) =>
                {
                    connections.Add(connectionId);
                    return connections;
                });
        }

        public HashSet<string> GetUserConnections(string userId)
        {
            return _userConnections.TryGetValue(userId, out var connections) ? connections : new HashSet<string>();
        }

        public bool IsUserOnline(string userId)
        {
            return _userConnections.ContainsKey(userId);
        }

        public IEnumerable<string> GetAllOnlineUsers()
        {
            return _userConnections.Keys;
        }
    }
} 