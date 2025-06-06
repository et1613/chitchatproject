using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Security.Claims;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using WebApplication1.Models.Users;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ConnectionController : ControllerBase
    {
        private readonly IConnectionManager _connectionManager;
        private readonly ILogger<ConnectionController> _logger;

        public ConnectionController(
            IConnectionManager connectionManager,
            ILogger<ConnectionController> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        [HttpGet("ws")]
        public async Task<IActionResult> ConnectWebSocket()
        {
            if (!HttpContext.WebSockets.IsWebSocketRequest)
                return BadRequest("WebSocket request expected");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return BadRequest("Invalid request");

            var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            if (!_connectionManager.AddClient(userId, webSocket, ipAddress))
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Connection limit exceeded", CancellationToken.None);
                return BadRequest("Connection limit exceeded");
            }

            try
            {
                await HandleWebSocketConnection(webSocket, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket connection error for user {UserId}", userId);
            }
            finally
            {
                _connectionManager.RemoveClient(userId);
            }

            return new EmptyResult();
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetConnectionStatus()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var status = await _connectionManager.GetConnectionStatusAsync(userId);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting connection status");
                return StatusCode(500, "Error getting connection status");
            }
        }

        [HttpGet("active-users")]
        public async Task<IActionResult> GetActiveUsers()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var activeUsers = await _connectionManager.GetActiveUsersAsync();
                return Ok(activeUsers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active users");
                return StatusCode(500, "Error getting active users");
            }
        }

        [HttpGet("user-status/{userId}")]
        public async Task<IActionResult> GetUserStatus(string userId)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                var status = await _connectionManager.GetUserStatusAsync(userId);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user status");
                return StatusCode(500, "Error getting user status");
            }
        }

        [HttpPost("disconnect")]
        public async Task<IActionResult> DisconnectUser()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                await _connectionManager.RemoveClientAsync(userId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting user");
                return StatusCode(500, "Error disconnecting user");
            }
        }

        [HttpPost("disconnect/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DisconnectUserByAdmin(string userId)
        {
            try
            {
                await _connectionManager.RemoveClientAsync(userId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting user by admin");
                return StatusCode(500, "Error disconnecting user by admin");
            }
        }

        [HttpGet("sessions")]
        public async Task<IActionResult> GetUserSessions()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var sessions = await _connectionManager.GetUserSessionsAsync(userId);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user sessions");
                return StatusCode(500, "Error getting user sessions");
            }
        }

        [HttpDelete("sessions/{sessionId}")]
        public async Task<IActionResult> RevokeSession(string sessionId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _connectionManager.RevokeSessionAsync(sessionId, userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking session");
                return StatusCode(500, "Error revoking session");
            }
        }

        [HttpPost("sessions/revoke-all")]
        public async Task<IActionResult> RevokeAllSessions()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    _connectionManager.RemoveClient(userId);
                }
                return Ok(new { message = "All connections removed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing all connections");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("sessions/active")]
        public async Task<IActionResult> GetActiveSessions()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var sessions = await _connectionManager.GetActiveSessionsAsync(userId);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active sessions");
                return StatusCode(500, "Error getting active sessions");
            }
        }

        [HttpPost("broadcast")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BroadcastMessage([FromBody] BroadcastMessageRequest request)
        {
            try
            {
                await _connectionManager.BroadcastMessageAsync(request.Message, request.Type);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting message");
                return StatusCode(500, "Error broadcasting message");
            }
        }

        [HttpPost("notify/{userId}")]
        public async Task<IActionResult> SendNotification(string userId, [FromBody] NotificationRequest request)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                await _connectionManager.SendNotificationAsync(userId, request.Message, request.Type);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification");
                return StatusCode(500, "Error sending notification");
            }
        }

        private async Task HandleWebSocketConnection(WebSocket webSocket, string userId)
        {
            var buffer = new byte[1024 * 4];
            var receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);

            while (!receiveResult.CloseStatus.HasValue)
            {
                if (receiveResult.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                    await HandleWebSocketMessage(userId, message);
                }

                receiveResult = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), CancellationToken.None);
            }

            await webSocket.CloseAsync(
                receiveResult.CloseStatus.Value,
                receiveResult.CloseStatusDescription,
                CancellationToken.None);
        }

        private async Task HandleWebSocketMessage(string userId, string message)
        {
            try
            {
                var messageData = JsonSerializer.Deserialize<WebSocketMessage>(message);
                if (messageData == null) return;

                switch (messageData.Type.ToLower())
                {
                    case "ping":
                        await _connectionManager.SendMessageToClient(userId, JsonSerializer.Serialize(new { type = "pong" }));
                        break;
                    case "status":
                        var status = new
                        {
                            type = "status",
                            userId = userId,
                            timestamp = DateTime.UtcNow,
                            isConnected = true
                        };
                        await _connectionManager.SendMessageToClient(userId, JsonSerializer.Serialize(status));
                        break;
                    default:
                        _logger.LogWarning("Unknown message type: {Type}", messageData.Type);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling WebSocket message from user {UserId}", userId);
                var error = new
                {
                    type = "error",
                    message = "Error processing message"
                };
                await _connectionManager.SendMessageToClient(userId, JsonSerializer.Serialize(error));
            }
        }
    }

    public class WebSocketMessage
    {
        public string Type { get; set; }
        public string Content { get; set; }
    }

    public class BroadcastMessageRequest
    {
        public required string Message { get; set; }
        public string? Type { get; set; }
    }

    public class NotificationRequest
    {
        public required string Message { get; set; }
        public string? Type { get; set; }
    }
} 