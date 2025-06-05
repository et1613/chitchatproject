using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Services
{
    public class SignalRConnectionManager
    {
        private readonly ConcurrentDictionary<string, string> _connections = new();
        private readonly ILogger<SignalRConnectionManager> _logger;

        public SignalRConnectionManager(ILogger<SignalRConnectionManager> logger)
        {
            _logger = logger;
        }

        public async Task AddClientAsync(string userId, string connectionId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            if (string.IsNullOrEmpty(connectionId))
                throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));

            try
            {
                _connections.AddOrUpdate(userId, connectionId, (_, _) => connectionId);
                _logger.LogInformation("New SignalR connection added for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding SignalR connection for user {UserId}", userId);
                throw;
            }
        }

        public async Task RemoveClientAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

            try
            {
                _connections.TryRemove(userId, out _);
                _logger.LogInformation("SignalR connection removed for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing SignalR connection for user {UserId}", userId);
                throw;
            }
        }

        public string? GetConnectionId(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

            try
            {
                return _connections.TryGetValue(userId, out var connectionId) ? connectionId : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SignalR connection for user {UserId}", userId);
                return null;
            }
        }

        public bool IsUserConnected(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

            try
            {
                return _connections.ContainsKey(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user connection status for user {UserId}", userId);
                return false;
            }
        }

        public IEnumerable<string> GetConnectedUserIds()
        {
            return _connections.Keys;
        }
    }
} 