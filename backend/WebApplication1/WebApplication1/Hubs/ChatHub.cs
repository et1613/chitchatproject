using Microsoft.AspNetCore.SignalR;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Messages;
using WebApplication1.Services;
using System.Security.Claims;

namespace WebApplication1.Hubs
{
    public class ChatHub : Hub
    {
        private readonly SignalRConnectionManager _connectionManager;
        private readonly IChatService _chatService;

        public ChatHub(SignalRConnectionManager connectionManager, IChatService chatService)
        {
            _connectionManager = connectionManager;
            _chatService = chatService;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await _connectionManager.AddClientAsync(userId, Context.ConnectionId);
                await Clients.All.SendAsync("UserConnected", userId);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await _connectionManager.RemoveClientAsync(userId);
                await Clients.All.SendAsync("UserDisconnected", userId);
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