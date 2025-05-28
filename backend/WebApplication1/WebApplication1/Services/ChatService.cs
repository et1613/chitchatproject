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
    public interface IChatService
    {
        Task<Message> SendMessageAsync(string senderId, string chatRoomId, string content);
        Task<List<Message>> GetChatHistoryAsync(string chatRoomId, string userId, int skip = 0, int take = 50);
        Task<bool> DeleteMessageAsync(string messageId, string userId);
        Task<bool> EditMessageAsync(string messageId, string userId, string newContent);
        Task HandleWebSocketConnection(WebSocket webSocket, string userId);
        Task BroadcastMessageToRoom(string chatRoomId, string message, string senderId);
        Task<ChatRoom> CreateChatRoomAsync(string name, string description, string creatorId);
        Task<ChatRoom> GetChatRoomAsync(string chatRoomId);
        Task<IEnumerable<ChatRoom>> GetUserChatRoomsAsync(string userId);
        Task<bool> AddUserToChatRoomAsync(string userId, string chatRoomId);
        Task<bool> RemoveUserFromChatRoomAsync(string userId, string chatRoomId);
        Task<IEnumerable<Message>> GetChatRoomMessagesAsync(string chatRoomId, int skip = 0, int take = 50);
        Task<bool> MarkMessageAsReadAsync(string messageId);
        Task<bool> UpdateMessageAsync(string messageId, string content, string userId);
    }

    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<ChatService> _logger;
        private readonly IEmailService _emailService;
        private readonly ConnectionManager _connectionManager;

        public ChatService(
            ApplicationDbContext context,
            IUserRepository userRepository,
            ILogger<ChatService> logger,
            IEmailService emailService,
            ConnectionManager connectionManager)
        {
            _context = context;
            _userRepository = userRepository;
            _logger = logger;
            _emailService = emailService;
            _connectionManager = connectionManager;
        }

        public static List<ChatRoom> ChatRooms = new();

        public static ChatRoom GetChatRoomById(string id) => ChatRooms.FirstOrDefault(c => c.Id == id);

        public static void SendDirectMessage(User sender, string receiverId, string content)
        {
            var receiver = UserService.GetUserById(receiverId);
            if (receiver == null || sender.BlockedUsers.Any(b => b.BlockedUserId.ToString() == receiverId)) return;

            var message = new Message
            {
                SenderId = sender.Id,
                Sender = sender,
                ChatRoomId = "direct",
                ChatRoom = new ChatRoom { Id = "direct", Name = "Direct Message" },
                Content = content,
                Timestamp = System.DateTime.UtcNow
            };

            // Not storing, notifying only (in-memory example)
            NotificationService.NotifyUser(receiverId, $"New message from {sender.UserName}: {content}");
        }

        public async Task<Message> SendMessageAsync(string senderId, string chatRoomId, string content)
        {
            try
            {
                _logger.LogInformation("Mesaj gönderiliyor: SenderId={SenderId}, ChatRoomId={ChatRoomId}", senderId, chatRoomId);

                // Gönderen kullanıcıyı kontrol et
                var sender = await _userRepository.GetByIdAsync(senderId);
                if (sender == null)
                {
                    _logger.LogWarning("Gönderen kullanıcı bulunamadı: {SenderId}", senderId);
                    throw new ArgumentException("Gönderen kullanıcı bulunamadı");
                }

                // Chat room'u kontrol et
                var chatRoom = await _context.ChatRooms.FindAsync(chatRoomId);
                if (chatRoom == null)
                {
                    _logger.LogWarning("Chat room bulunamadı: {ChatRoomId}", chatRoomId);
                    throw new ArgumentException("Chat room bulunamadı");
                }

                // Kullanıcının chat room'a erişim yetkisi var mı kontrol et
                if (!chatRoom.Participants.Any(p => p.Id == senderId))
                {
                    _logger.LogWarning("Kullanıcının chat room'a erişim yetkisi yok: UserId={UserId}, ChatRoomId={ChatRoomId}", 
                        senderId, chatRoomId);
                    throw new UnauthorizedAccessException("Bu chat room'a mesaj gönderme yetkiniz yok");
                }

                // Mesaj içeriğini doğrula
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("Boş mesaj içeriği: SenderId={SenderId}", senderId);
                    throw new ArgumentException("Mesaj içeriği boş olamaz");
                }

                // Yeni mesaj oluştur
                var message = new Message
                {
                    SenderId = senderId,
                    ChatRoomId = chatRoomId,
                    Content = content,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                // Mesajı kaydet
                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Mesaj başarıyla gönderildi: MessageId={MessageId}", message.Id);

                // Katılımcılara bildirim gönder
                foreach (var participant in chatRoom.Participants.Where(p => p.Id != senderId))
                {
                    try
                    {
                        await _emailService.SendEmailAsync(
                            participant.Email,
                            "Yeni Mesaj",
                            $"{sender.UserName} size yeni bir mesaj gönderdi: {content}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Bildirim emaili gönderilemedi: UserId={UserId}", participant.Id);
                    }
                }

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mesaj gönderilirken hata oluştu: SenderId={SenderId}, ChatRoomId={ChatRoomId}", 
                    senderId, chatRoomId);
                throw;
            }
        }

        public async Task<List<Message>> GetChatHistoryAsync(string chatRoomId, string userId, int skip = 0, int take = 50)
        {
            try
            {
                _logger.LogInformation("Chat geçmişi alınıyor: ChatRoomId={ChatRoomId}, UserId={UserId}", 
                    chatRoomId, userId);

                // Kullanıcının chat room'a erişim yetkisi var mı kontrol et
                var chatRoom = await _context.ChatRooms
                    .Include(cr => cr.Participants)
                    .FirstOrDefaultAsync(cr => cr.Id == chatRoomId);

                if (chatRoom == null)
                {
                    _logger.LogWarning("Chat room bulunamadı: {ChatRoomId}", chatRoomId);
                    throw new ArgumentException("Chat room bulunamadı");
                }

                if (!chatRoom.Participants.Any(p => p.Id == userId))
                {
                    _logger.LogWarning("Kullanıcının chat room'a erişim yetkisi yok: UserId={UserId}, ChatRoomId={ChatRoomId}", 
                        userId, chatRoomId);
                    throw new UnauthorizedAccessException("Bu chat room'un geçmişini görüntüleme yetkiniz yok");
                }

                // Mesajları getir
                var messages = await _context.Messages
                    .Include(m => m.Sender)
                    .Where(m => m.ChatRoomId == chatRoomId && !m.IsDeleted)
                    .OrderByDescending(m => m.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();

                _logger.LogInformation("Chat geçmişi başarıyla alındı: {Count} mesaj", messages.Count);
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat geçmişi alınırken hata oluştu: ChatRoomId={ChatRoomId}, UserId={UserId}", 
                    chatRoomId, userId);
                throw;
            }
        }

        public async Task<bool> DeleteMessageAsync(string messageId, string userId)
        {
            try
            {
                _logger.LogInformation("Mesaj siliniyor: MessageId={MessageId}, UserId={UserId}", messageId, userId);

                var message = await _context.Messages
                    .Include(m => m.Sender)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                {
                    _logger.LogWarning("Silinecek mesaj bulunamadı: {MessageId}", messageId);
                    return false;
                }

                // Kullanıcının mesajı silme yetkisi var mı kontrol et
                if (message.SenderId != userId)
                {
                    _logger.LogWarning("Kullanıcının mesajı silme yetkisi yok: UserId={UserId}, MessageId={MessageId}", 
                        userId, messageId);
                    throw new UnauthorizedAccessException("Bu mesajı silme yetkiniz yok");
                }

                message.IsDeleted = true;
                message.DeletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Mesaj başarıyla silindi: {MessageId}", messageId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mesaj silinirken hata oluştu: MessageId={MessageId}, UserId={UserId}", 
                    messageId, userId);
                throw;
            }
        }

        public async Task<bool> EditMessageAsync(string messageId, string userId, string newContent)
        {
            try
            {
                _logger.LogInformation("Mesaj düzenleniyor: MessageId={MessageId}, UserId={UserId}", messageId, userId);

                var message = await _context.Messages
                    .Include(m => m.Sender)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                {
                    _logger.LogWarning("Düzenlenecek mesaj bulunamadı: {MessageId}", messageId);
                    return false;
                }

                // Kullanıcının mesajı düzenleme yetkisi var mı kontrol et
                if (message.SenderId != userId)
                {
                    _logger.LogWarning("Kullanıcının mesajı düzenleme yetkisi yok: UserId={UserId}, MessageId={MessageId}", 
                        userId, messageId);
                    throw new UnauthorizedAccessException("Bu mesajı düzenleme yetkiniz yok");
                }

                // Mesajın düzenlenebilir olup olmadığını kontrol et (örn: 24 saat içinde)
                if (DateTime.UtcNow - message.CreatedAt > TimeSpan.FromHours(24))
                {
                    _logger.LogWarning("Mesaj düzenleme süresi dolmuş: MessageId={MessageId}", messageId);
                    throw new InvalidOperationException("Mesaj düzenleme süresi dolmuş");
                }

                // Yeni içeriği doğrula
                if (string.IsNullOrWhiteSpace(newContent))
                {
                    _logger.LogWarning("Boş mesaj içeriği: MessageId={MessageId}", messageId);
                    throw new ArgumentException("Mesaj içeriği boş olamaz");
                }

                // Mesajı düzenle
                message.Content = newContent;
                message.EditedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Mesaj başarıyla düzenlendi: {MessageId}", messageId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mesaj düzenlenirken hata oluştu: MessageId={MessageId}, UserId={UserId}", 
                    messageId, userId);
                throw;
            }
        }

        public async Task HandleWebSocketConnection(WebSocket webSocket, string userId)
        {
            try
            {
                _logger.LogInformation("WebSocket bağlantısı başlatıldı: UserId={UserId}", userId);

                // Kullanıcıyı bağlantı yöneticisine ekle
                _connectionManager.AddClient(userId, webSocket);

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
                _logger.LogError(ex, "WebSocket bağlantısı sırasında hata oluştu: UserId={UserId}", userId);
            }
            finally
            {
                _connectionManager.RemoveClient(userId);
                _logger.LogInformation("WebSocket bağlantısı sonlandırıldı: UserId={UserId}", userId);
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
                _logger.LogError(ex, "Gelen mesaj işlenirken hata oluştu: UserId={UserId}", userId);
                await SendErrorMessage(userId, "Mesaj gönderilemedi. Lütfen tekrar deneyin.");
            }
        }

        public async Task BroadcastMessageToRoom(string chatRoomId, string message, string senderId)
        {
            try
            {
                var chatRoom = await _context.ChatRooms
                    .Include(cr => cr.Participants)
                    .FirstOrDefaultAsync(cr => cr.Id == chatRoomId);

                if (chatRoom == null) return;

                var sender = await _userRepository.GetByIdAsync(senderId);
                if (sender == null) return;

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
                _logger.LogError(ex, "Mesaj yayınlanırken hata oluştu: ChatRoomId={ChatRoomId}", chatRoomId);
            }
        }

        private async Task BroadcastTypingStatus(string userId, string chatRoomId, bool isTyping)
        {
            try
            {
                var chatRoom = await _context.ChatRooms
                    .Include(cr => cr.Participants)
                    .FirstOrDefaultAsync(cr => cr.Id == chatRoomId);

                if (chatRoom == null) return;

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null) return;

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
                _logger.LogError(ex, "Yazma durumu yayınlanırken hata oluştu: UserId={UserId}, ChatRoomId={ChatRoomId}", 
                    userId, chatRoomId);
            }
        }

        private async Task SendErrorMessage(string userId, string errorMessage)
        {
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
                _logger.LogError(ex, "Hata mesajı gönderilirken hata oluştu: UserId={UserId}", userId);
            }
        }

        public async Task<ChatRoom> CreateChatRoomAsync(string name, string description, string creatorId)
        {
            try
            {
                var chatRoom = new ChatRoom
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Description = description,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.ChatRooms.Add(chatRoom);
                await _context.SaveChangesAsync();

                // Add creator as participant
                await AddUserToChatRoomAsync(creatorId, chatRoom.Id);

                return chatRoom;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat room");
                throw;
            }
        }

        public async Task<ChatRoom> GetChatRoomAsync(string chatRoomId)
        {
            return await _context.ChatRooms
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.Id == chatRoomId);
        }

        public async Task<IEnumerable<ChatRoom>> GetUserChatRoomsAsync(string userId)
        {
            return await _context.ChatRooms
                .Include(c => c.Participants)
                .Where(c => c.Participants.Any(p => p.Id == userId))
                .ToListAsync();
        }

        public async Task<bool> AddUserToChatRoomAsync(string userId, string chatRoomId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                var chatRoom = await _context.ChatRooms
                    .Include(c => c.Participants)
                    .FirstOrDefaultAsync(c => c.Id == chatRoomId);

                if (user == null || chatRoom == null)
                    return false;

                if (!chatRoom.Participants.Contains(user))
                {
                    chatRoom.Participants.Add(user);
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user to chat room");
                return false;
            }
        }

        public async Task<bool> RemoveUserFromChatRoomAsync(string userId, string chatRoomId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                var chatRoom = await _context.ChatRooms
                    .Include(c => c.Participants)
                    .FirstOrDefaultAsync(c => c.Id == chatRoomId);

                if (user == null || chatRoom == null)
                    return false;

                if (chatRoom.Participants.Contains(user))
                {
                    chatRoom.Participants.Remove(user);
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user from chat room");
                return false;
            }
        }

        public async Task<IEnumerable<Message>> GetChatRoomMessagesAsync(string chatRoomId, int skip = 0, int take = 50)
        {
            return await _context.Messages
                .Include(m => m.Sender)
                .Where(m => m.ChatRoomId == chatRoomId)
                .OrderByDescending(m => m.Timestamp)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<bool> MarkMessageAsReadAsync(string messageId)
        {
            try
            {
                var message = await _context.Messages.FindAsync(messageId);
                if (message == null)
                    return false;

                message.IsRead = true;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message as read");
                return false;
            }
        }

        public async Task<bool> UpdateMessageAsync(string messageId, string content, string userId)
        {
            try
            {
                var message = await _context.Messages.FindAsync(messageId);
                if (message == null || message.SenderId != userId)
                    return false;

                message.Content = content;
                message.IsEdited = true;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating message");
                return false;
            }
        }
    }

    public class WebSocketMessage
    {
        public string Type { get; set; }
        public string ChatRoomId { get; set; }
        public string Content { get; set; }
    }
} 