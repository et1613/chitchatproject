using System.Collections.Generic;
using System.Linq;
using WebApplication1.Models;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Messages;
using WebApplication1.Models.Users;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Repositories;
using WebApplication1.Data;
using Microsoft.Extensions.Logging;
using WebApplication1.Services;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace WebApplication1.Services
{
    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<ChatService> _logger;
        private readonly IEmailService _emailService;
        private readonly ConnectionManager _connectionManager;
        private readonly INotificationService _notificationService;
        private static readonly List<ChatRoom> ChatRooms = new();

        public ChatService(
            ApplicationDbContext context,
            IUserRepository userRepository,
            ILogger<ChatService> logger,
            IEmailService emailService,
            ConnectionManager connectionManager,
            INotificationService notificationService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        public static ChatRoom? GetChatRoomById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Chat room ID cannot be null or empty", nameof(id));
            }
            return ChatRooms.FirstOrDefault(c => c.Id == id);
        }

        public async Task SendDirectMessage(User sender, string receiverId, string content)
        {
            if (sender == null)
            {
                throw new ArgumentNullException(nameof(sender));
            }
            if (string.IsNullOrEmpty(receiverId))
            {
                throw new ArgumentException("Receiver ID cannot be null or empty", nameof(receiverId));
            }
            if (string.IsNullOrEmpty(content))
            {
                throw new ArgumentException("Message content cannot be null or empty", nameof(content));
            }

            var receiver = await _userRepository.GetByIdAsync(receiverId);
            if (receiver == null)
            {
                throw new ArgumentException($"Receiver with ID {receiverId} not found");
            }

            if (sender.BlockedUsers.Any(b => b.BlockedUserId.ToString() == receiverId))
            {
                _logger.LogWarning("Message blocked: Sender {SenderId} has blocked receiver {ReceiverId}", sender.Id, receiverId);
                return;
            }

            var message = new Message
            {
                Id = Guid.NewGuid().ToString(),
                SenderId = sender.Id,
                Sender = sender,
                ChatRoomId = "direct",
                ChatRoom = new ChatRoom 
                { 
                    Id = "direct", 
                    Name = "Direct Message",
                    AdminId = sender.Id,
                    Admin = sender,
                    CreatedAt = DateTime.UtcNow
                },
                Content = content,
                Timestamp = DateTime.UtcNow,
                IsDeleted = false,
                IsEdited = false,
                IsRead = false
            };

            try
            {
                await _notificationService.NotifyUserAsync(receiverId, $"New message from {sender.UserName}: {content}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to user {ReceiverId}", receiverId);
            }
        }

        public async Task<Message> SendMessageAsync(string senderId, string chatRoomId, string content)
        {
            if (string.IsNullOrEmpty(senderId))
            {
                throw new ArgumentException("Sender ID cannot be null or empty", nameof(senderId));
            }
            if (string.IsNullOrEmpty(chatRoomId))
            {
                throw new ArgumentException("Chat room ID cannot be null or empty", nameof(chatRoomId));
            }
            if (string.IsNullOrEmpty(content))
            {
                throw new ArgumentException("Message content cannot be null or empty", nameof(content));
            }

            try
            {
                _logger.LogInformation("Sending message: SenderId={SenderId}, ChatRoomId={ChatRoomId}", senderId, chatRoomId);

                var sender = await _userRepository.GetByIdAsync(senderId);
                if (sender == null)
                {
                    throw new ArgumentException($"Sender with ID {senderId} not found");
                }

                var chatRoom = await _context.ChatRooms
                    .Include(c => c.Participants)
                    .FirstOrDefaultAsync(c => c.Id == chatRoomId);

                if (chatRoom == null)
                {
                    throw new ArgumentException($"Chat room with ID {chatRoomId} not found");
                }

                if (!chatRoom.Participants.Any(p => p.Id == senderId))
                {
                    throw new UnauthorizedAccessException($"User {senderId} is not a participant in chat room {chatRoomId}");
                }

                var message = new Message
                {
                    Id = Guid.NewGuid().ToString(),
                    SenderId = senderId,
                    Sender = sender,
                    ChatRoomId = chatRoomId,
                    ChatRoom = chatRoom,
                    Content = content,
                    Timestamp = DateTime.UtcNow,
                    IsDeleted = false,
                    IsEdited = false,
                    IsRead = false
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Message sent successfully: MessageId={MessageId}", message.Id);

                foreach (var participant in chatRoom.Participants.Where(p => p.Id != senderId))
                {
                    try
                    {
                        await _emailService.SendEmailAsync(
                            participant.Email,
                            "New Message",
                            $"{sender.UserName} sent a new message: {content}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send email notification to user {UserId}", participant.Id);
                    }
                }

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message: SenderId={SenderId}, ChatRoomId={ChatRoomId}", senderId, chatRoomId);
                throw;
            }
        }

        public async Task<List<Message>> GetChatHistoryAsync(string chatRoomId, string userId, int skip = 0, int take = 50)
        {
            if (string.IsNullOrEmpty(chatRoomId))
            {
                throw new ArgumentException("Chat room ID cannot be null or empty", nameof(chatRoomId));
            }
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }
            if (skip < 0)
            {
                throw new ArgumentException("Skip count cannot be negative", nameof(skip));
            }
            if (take <= 0)
            {
                throw new ArgumentException("Take count must be positive", nameof(take));
            }

            try
            {
                var chatRoom = await _context.ChatRooms
                    .Include(c => c.Participants)
                    .FirstOrDefaultAsync(c => c.Id == chatRoomId);

                if (chatRoom == null)
                {
                    throw new ArgumentException($"Chat room with ID {chatRoomId} not found");
                }

                if (!chatRoom.Participants.Any(p => p.Id == userId))
                {
                    throw new UnauthorizedAccessException($"User {userId} is not a participant in chat room {chatRoomId}");
                }

                var messages = await _context.Messages
                    .Include(m => m.Sender)
                    .Where(m => m.ChatRoomId == chatRoomId && !m.IsDeleted)
                    .OrderByDescending(m => m.Timestamp)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();

                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat history: ChatRoomId={ChatRoomId}, UserId={UserId}", chatRoomId, userId);
                throw;
            }
        }

        public async Task<bool> DeleteMessageAsync(string messageId, string userId)
        {
            if (string.IsNullOrEmpty(messageId))
            {
                throw new ArgumentException("Message ID cannot be null or empty", nameof(messageId));
            }
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }

            try
            {
                var message = await _context.Messages
                    .Include(m => m.Sender)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                {
                    return false;
                }

                if (message.SenderId != userId)
                {
                    throw new UnauthorizedAccessException($"User {userId} is not authorized to delete message {messageId}");
                }

                message.IsDeleted = true;
                message.DeletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message: MessageId={MessageId}, UserId={UserId}", messageId, userId);
                throw;
            }
        }

        public async Task<bool> EditMessageAsync(string messageId, string userId, string newContent)
        {
            if (string.IsNullOrEmpty(messageId))
            {
                throw new ArgumentException("Message ID cannot be null or empty", nameof(messageId));
            }
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }
            if (string.IsNullOrEmpty(newContent))
            {
                throw new ArgumentException("New content cannot be null or empty", nameof(newContent));
            }

            try
            {
                var message = await _context.Messages
                    .Include(m => m.Sender)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                {
                    return false;
                }

                if (message.SenderId != userId)
                {
                    throw new UnauthorizedAccessException($"User {userId} is not authorized to edit message {messageId}");
                }

                if (DateTime.UtcNow - message.Timestamp > TimeSpan.FromHours(24))
                {
                    throw new InvalidOperationException("Message cannot be edited after 24 hours");
                }

                message.Content = newContent;
                message.EditedAt = DateTime.UtcNow;
                message.IsEdited = true;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message: MessageId={MessageId}, UserId={UserId}", messageId, userId);
                throw;
            }
        }

        public async Task HandleWebSocketConnection(WebSocket webSocket, string userId)
        {
            if (webSocket == null)
            {
                throw new ArgumentNullException(nameof(webSocket));
            }
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }

            try
            {
                await _connectionManager.AddClientAsync(userId, webSocket);

                var buffer = new byte[1024 * 4];
                var receiveResult = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), CancellationToken.None);

                while (!receiveResult.CloseStatus.HasValue)
                {
                    if (receiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        var messageJson = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                        var message = JsonSerializer.Deserialize<WebSocketMessage>(messageJson);

                        if (message != null)
                        {
                            switch (message.Type)
                            {
                                case "message":
                                    await HandleIncomingMessage(userId, message);
                                    break;
                                case "typing":
                                    await BroadcastTypingStatus(userId, message.ChatRoomId, true);
                                    break;
                                case "stop_typing":
                                    await BroadcastTypingStatus(userId, message.ChatRoomId, false);
                                    break;
                            }
                        }
                    }

                    receiveResult = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), CancellationToken.None);
                }

                await webSocket.CloseAsync(
                    receiveResult.CloseStatus.Value,
                    receiveResult.CloseStatusDescription,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling WebSocket connection for user {UserId}", userId);
                throw;
            }
            finally
            {
                try
                {
                    await _connectionManager.RemoveClientAsync(userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing client connection for user {UserId}", userId);
                }
            }
        }

        private async Task HandleIncomingMessage(string userId, WebSocketMessage message)
        {
            try
            {
                var chatMessage = await SendMessageAsync(userId, message.ChatRoomId, message.Content);
                await BroadcastMessageToRoom(message.ChatRoomId, message.Content, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling incoming message from user {UserId}", userId);
                await SendErrorMessage(userId, "Failed to send message. Please try again.");
            }
        }

        public async Task BroadcastMessageToRoom(string chatRoomId, string message, string senderId)
        {
            if (string.IsNullOrEmpty(chatRoomId))
            {
                throw new ArgumentException("Chat room ID cannot be null or empty", nameof(chatRoomId));
            }
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentException("Message cannot be null or empty", nameof(message));
            }
            if (string.IsNullOrEmpty(senderId))
            {
                throw new ArgumentException("Sender ID cannot be null or empty", nameof(senderId));
            }

            try
            {
                var chatRoom = await _context.ChatRooms
                    .Include(c => c.Participants)
                    .FirstOrDefaultAsync(c => c.Id == chatRoomId);

                if (chatRoom == null)
                {
                    return;
                }

                var sender = await _userRepository.GetByIdAsync(senderId);
                if (sender == null)
                {
                    return;
                }

                var messageData = new
                {
                    type = "message",
                    chatRoomId = chatRoomId,
                    senderId = senderId,
                    senderName = sender.UserName,
                    content = message,
                    timestamp = DateTime.UtcNow
                };

                var messageJson = JsonSerializer.Serialize(messageData);
                var messageBytes = Encoding.UTF8.GetBytes(messageJson);

                foreach (var participant in chatRoom.Participants)
                {
                    if (participant.Id != senderId)
                    {
                        var socket = _connectionManager.GetClient(participant.Id);
                        if (socket != null && socket.State == WebSocketState.Open)
                        {
                            await socket.SendAsync(
                                new ArraySegment<byte>(messageBytes),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting message to room {ChatRoomId}", chatRoomId);
                throw;
            }
        }

        private async Task BroadcastTypingStatus(string userId, string chatRoomId, bool isTyping)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }
            if (string.IsNullOrEmpty(chatRoomId))
            {
                throw new ArgumentException("Chat room ID cannot be null or empty", nameof(chatRoomId));
            }

            try
            {
                var chatRoom = await _context.ChatRooms
                    .Include(c => c.Participants)
                    .FirstOrDefaultAsync(c => c.Id == chatRoomId);

                if (chatRoom == null)
                {
                    return;
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return;
                }

                var statusData = new
                {
                    type = isTyping ? "typing" : "stop_typing",
                    chatRoomId = chatRoomId,
                    userId = userId,
                    userName = user.UserName
                };

                var statusJson = JsonSerializer.Serialize(statusData);
                var statusBytes = Encoding.UTF8.GetBytes(statusJson);

                foreach (var participant in chatRoom.Participants)
                {
                    if (participant.Id != userId)
                    {
                        var socket = _connectionManager.GetClient(participant.Id);
                        if (socket != null && socket.State == WebSocketState.Open)
                        {
                            await socket.SendAsync(
                                new ArraySegment<byte>(statusBytes),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting typing status: UserId={UserId}, ChatRoomId={ChatRoomId}", userId, chatRoomId);
                throw;
            }
        }

        private async Task SendErrorMessage(string userId, string errorMessage)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }
            if (string.IsNullOrEmpty(errorMessage))
            {
                throw new ArgumentException("Error message cannot be null or empty", nameof(errorMessage));
            }

            try
            {
                var errorData = new
                {
                    type = "error",
                    message = errorMessage
                };

                var errorJson = JsonSerializer.Serialize(errorData);
                var errorBytes = Encoding.UTF8.GetBytes(errorJson);

                var socket = _connectionManager.GetClient(userId);
                if (socket != null && socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(
                        new ArraySegment<byte>(errorBytes),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending error message to user {UserId}", userId);
                throw;
            }
        }

        public async Task<ChatRoom> CreateChatRoomAsync(string name, string description, string creatorId)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name cannot be null or empty", nameof(name));
            }
            if (string.IsNullOrEmpty(creatorId))
            {
                throw new ArgumentException("Creator ID cannot be null or empty", nameof(creatorId));
            }

            try
            {
                var creator = await _userRepository.GetByIdAsync(creatorId);
                if (creator == null)
                {
                    throw new ArgumentException($"Creator with ID {creatorId} not found");
                }

                var chatRoom = new ChatRoom
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Description = description,
                    CreatedAt = DateTime.UtcNow,
                    AdminId = creatorId,
                    Admin = creator,
                    Participants = new List<User> { creator }
                };

                _context.ChatRooms.Add(chatRoom);
                await _context.SaveChangesAsync();

                return chatRoom;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat room: Name={Name}, CreatorId={CreatorId}", name, creatorId);
                throw;
            }
        }

        public async Task<ChatRoom?> GetChatRoomAsync(string chatRoomId)
        {
            if (string.IsNullOrEmpty(chatRoomId))
            {
                throw new ArgumentException("Chat room ID cannot be null or empty", nameof(chatRoomId));
            }

            try
            {
                return await _context.ChatRooms
                    .Include(c => c.Participants)
                    .FirstOrDefaultAsync(c => c.Id == chatRoomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat room: ChatRoomId={ChatRoomId}", chatRoomId);
                throw;
            }
        }

        public async Task<IEnumerable<ChatRoom>> GetUserChatRoomsAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }

            try
            {
                return await _context.ChatRooms
                    .Include(c => c.Participants)
                    .Where(c => c.Participants.Any(p => p.Id == userId))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user chat rooms: UserId={UserId}", userId);
                throw;
            }
        }

        public async Task<bool> AddUserToChatRoomAsync(string userId, string chatRoomId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }
            if (string.IsNullOrEmpty(chatRoomId))
            {
                throw new ArgumentException("Chat room ID cannot be null or empty", nameof(chatRoomId));
            }

            try
            {
                var user = await _context.Users.FindAsync(userId);
                var chatRoom = await _context.ChatRooms
                    .Include(c => c.Participants)
                    .FirstOrDefaultAsync(c => c.Id == chatRoomId);

                if (user == null || chatRoom == null)
                {
                    return false;
                }

                if (!chatRoom.Participants.Contains(user))
                {
                    chatRoom.Participants.Add(user);
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user to chat room: UserId={UserId}, ChatRoomId={ChatRoomId}", userId, chatRoomId);
                throw;
            }
        }

        public async Task<bool> RemoveUserFromChatRoomAsync(string userId, string chatRoomId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }
            if (string.IsNullOrEmpty(chatRoomId))
            {
                throw new ArgumentException("Chat room ID cannot be null or empty", nameof(chatRoomId));
            }

            try
            {
                var user = await _context.Users.FindAsync(userId);
                var chatRoom = await _context.ChatRooms
                    .Include(c => c.Participants)
                    .FirstOrDefaultAsync(c => c.Id == chatRoomId);

                if (user == null || chatRoom == null)
                {
                    return false;
                }

                if (chatRoom.Participants.Contains(user))
                {
                    chatRoom.Participants.Remove(user);
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user from chat room: UserId={UserId}, ChatRoomId={ChatRoomId}", userId, chatRoomId);
                throw;
            }
        }

        public async Task<IEnumerable<Message>> GetChatRoomMessagesAsync(string chatRoomId, int skip = 0, int take = 50)
        {
            if (string.IsNullOrEmpty(chatRoomId))
            {
                throw new ArgumentException("Chat room ID cannot be null or empty", nameof(chatRoomId));
            }
            if (skip < 0)
            {
                throw new ArgumentException("Skip count cannot be negative", nameof(skip));
            }
            if (take <= 0)
            {
                throw new ArgumentException("Take count must be positive", nameof(take));
            }

            try
            {
                return await _context.Messages
                    .Include(m => m.Sender)
                    .Where(m => m.ChatRoomId == chatRoomId)
                    .OrderByDescending(m => m.Timestamp)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat room messages: ChatRoomId={ChatRoomId}", chatRoomId);
                throw;
            }
        }

        public async Task<bool> MarkMessageAsReadAsync(string messageId)
        {
            if (string.IsNullOrEmpty(messageId))
            {
                throw new ArgumentException("Message ID cannot be null or empty", nameof(messageId));
            }

            try
            {
                var message = await _context.Messages.FindAsync(messageId);
                if (message == null)
                {
                    return false;
                }

                message.IsRead = true;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message as read: MessageId={MessageId}", messageId);
                throw;
            }
        }

        public async Task<bool> UpdateMessageAsync(string messageId, string content, string userId)
        {
            if (string.IsNullOrEmpty(messageId))
            {
                throw new ArgumentException("Message ID cannot be null or empty", nameof(messageId));
            }
            if (string.IsNullOrEmpty(content))
            {
                throw new ArgumentException("Content cannot be null or empty", nameof(content));
            }
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }

            try
            {
                var message = await _context.Messages.FindAsync(messageId);
                if (message == null || message.SenderId != userId)
                {
                    return false;
                }

                message.Content = content;
                message.IsEdited = true;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating message: MessageId={MessageId}, UserId={UserId}", messageId, userId);
                throw;
            }
        }
    }

    public class WebSocketMessage
    {
        public string Type { get; set; } = string.Empty;
        public string ChatRoomId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
} 