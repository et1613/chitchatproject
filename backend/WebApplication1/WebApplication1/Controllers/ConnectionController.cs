using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Security.Claims;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ConnectionController : ControllerBase
    {
        private readonly ConnectionManager _connectionManager;
        private readonly ILogger<ConnectionController> _logger;

        public ConnectionController(
            ConnectionManager connectionManager,
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
        public IActionResult GetConnectionStatus()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var isConnected = _connectionManager.GetClient(userId) != null;
                var connectionInfo = _connectionManager.GetAllConnections()
                    .FirstOrDefault(c => c.Socket.State == WebSocketState.Open);

                return Ok(new
                {
                    isConnected,
                    connectionInfo = connectionInfo != null ? new
                    {
                        connectedAt = connectionInfo.ConnectedAt,
                        lastActivity = connectionInfo.LastActivity,
                        ipAddress = connectionInfo.IpAddress,
                        messagesSent = connectionInfo.MessagesSent,
                        messagesReceived = connectionInfo.MessagesReceived
                    } : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting connection status");
                return StatusCode(500, "An error occurred while getting connection status");
            }
        }

        [HttpPost("ping")]
        public async Task<IActionResult> PingConnection()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var isAlive = await _connectionManager.PingClient(userId);
                return Ok(new { isAlive });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinging connection");
                return StatusCode(500, "An error occurred while pinging connection");
            }
        }

        [HttpGet("active")]
        public IActionResult GetActiveConnections()
        {
            try
            {
                var connections = _connectionManager.GetAllConnections()
                    .Where(c => c.Socket.State == WebSocketState.Open)
                    .Select(c => new
                    {
                        connectedAt = c.ConnectedAt,
                        lastActivity = c.LastActivity,
                        ipAddress = c.IpAddress,
                        messagesSent = c.MessagesSent,
                        messagesReceived = c.MessagesReceived
                    });

                return Ok(new
                {
                    totalConnections = _connectionManager.GetConnectionCount(),
                    activeConnections = connections
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active connections");
                return StatusCode(500, "An error occurred while getting active connections");
            }
        }

        [HttpPost("cleanup")]
        [Authorize(Roles = "Admin")]
        public IActionResult CleanupConnections()
        {
            try
            {
                _connectionManager.CleanupClosedConnections();
                return Ok(new { message = "Connection cleanup completed" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up connections");
                return StatusCode(500, "An error occurred while cleaning up connections");
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
} 