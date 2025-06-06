using Microsoft.AspNetCore.SignalR;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Messages;
using WebApplication1.Services;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Hubs
{
    public class ChatHub : Hub
    {
        private readonly SignalRConnectionManager _connectionManager;
        private readonly IChatService _chatService;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(SignalRConnectionManager connectionManager, IChatService chatService, ILogger<ChatHub> logger)
        {
            _connectionManager = connectionManager;
            _chatService = chatService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    throw new UnauthorizedAccessException("User not authenticated");

                _connectionManager.AddClient(userId, Context.ConnectionId);
                await Clients.Caller.SendAsync("Connected", "Successfully connected to chat hub");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnConnectedAsync");
                throw;
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    _connectionManager.RemoveClient(userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync");
                throw;
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(string chatRoomId, string content)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return;

            var message = await _chatService.SendMessageAsync(userId, chatRoomId, content);
            await Clients.Group(chatRoomId).SendAsync("ReceiveMessage", message);
        }

        public async Task JoinChatRoom(string chatRoomId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return;

            await Groups.AddToGroupAsync(Context.ConnectionId, chatRoomId);
            await Clients.Group(chatRoomId).SendAsync("UserJoined", userId, chatRoomId);
        }

        public async Task LeaveChatRoom(string chatRoomId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return;

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatRoomId);
            await Clients.Group(chatRoomId).SendAsync("UserLeft", userId, chatRoomId);
        }

        public async Task Typing(string chatRoomId, bool isTyping)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return;

            await Clients.Group(chatRoomId).SendAsync("UserTyping", userId, isTyping);
        }

        public async Task MarkAsRead(string chatRoomId, string messageId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return;

            await _chatService.MarkMessageAsReadAsync(messageId);
            await Clients.Group(chatRoomId).SendAsync("MessageRead", messageId, userId);
        }
    }
} 