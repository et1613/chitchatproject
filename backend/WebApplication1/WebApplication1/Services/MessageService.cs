using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebApplication1.Data;
using WebApplication1.Models.Messages;
using WebApplication1.Models.Chat;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using WebApplication1.Models.Enums;
using WebApplication1.Models.Notifications;
using WebApplication1.Repositories;

namespace WebApplication1.Services
{
    public interface IMessageService
    {
        Task<Message> SendMessageAsync(string senderId, string chatRoomId, string content, List<string> attachmentUrls = null);
        Task<Message> EditMessageAsync(string messageId, string content, string userId);
        Task<bool> DeleteMessageAsync(string messageId, string userId);
        Task<Message> GetMessageAsync(string messageId);
        Task<IEnumerable<Message>> GetChatHistoryAsync(string chatRoomId, int skip = 0, int take = 50);
        Task<bool> MarkMessageAsReadAsync(string messageId, string userId);
        Task<bool> HideMessageForUserAsync(string messageId, string userId);
        Task<IEnumerable<Message>> GetUnreadMessagesAsync(string userId, string chatRoomId);
        Task<bool> PinMessageAsync(string messageId, string userId);
        Task<bool> UnpinMessageAsync(string messageId, string userId);
        Task<IEnumerable<Message>> GetPinnedMessagesAsync(string chatRoomId);
        Task<bool> ForwardMessageAsync(string messageId, string targetChatRoomId, string userId);
        Task<bool> ReactToMessageAsync(string messageId, string userId, string reaction);
        Task<bool> RemoveReactionAsync(string messageId, string userId, string reaction);
        Task<Dictionary<string, int>> GetMessageReactionsAsync(string messageId);
        Task<IEnumerable<Message>> SearchMessagesAsync(string query, string userId, string? chatRoomId = null, DateTime? startDate = null, DateTime? endDate = null);
        Task<IEnumerable<Message>> FilterMessagesByTypeAsync(string userId, string chatRoomId, MessageType type);
        Task<IEnumerable<Message>> FilterMessagesByUserAsync(string userId, string chatRoomId, string targetUserId);
        Task<IEnumerable<Message>> FilterMessagesByContentAsync(string userId, string chatRoomId, string content);
        Task<IEnumerable<Message>> GetMessagesByDateRangeAsync(string userId, string chatRoomId, DateTime startDate, DateTime endDate);
        Task<bool> BackupMessagesAsync(string userId, string chatRoomId);
        Task<bool> RestoreMessagesFromBackupAsync(string userId, string chatRoomId, string backupId);
        Task<IEnumerable<MessageBackup>> GetMessageBackupsAsync(string userId, string chatRoomId);
        Task<MessageBackup> GetBackupDetailsAsync(string backupId);
        Task<bool> DeleteMessageBackupAsync(string backupId, string userId);
        Task<byte[]> ExportMessagesAsync(string userId, string chatRoomId, ExportFormat format);
        Task<bool> ImportMessagesAsync(string userId, string chatRoomId, byte[] data, ImportFormat format);
        Task<bool> ValidateImportDataAsync(byte[] data, ImportFormat format);
        Task<ImportProgress> GetImportProgressAsync(string importId);
        Task<ExportProgress> GetExportProgressAsync(string exportId);
    }

    public enum MessageType
    {
        Text,
        Image,
        File,
        Voice,
        Video,
        Location,
        Contact,
        System
    }

    public enum ExportFormat
    {
        Json,
        Csv,
        Xml,
        Pdf
    }

    public enum ImportFormat
    {
        Json,
        Csv,
        Xml
    }

    public class MessageBackup
    {
        public string Id { get; set; }
        public string ChatRoomId { get; set; }
        public string UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public int MessageCount { get; set; }
        public long BackupSize { get; set; }
        public string BackupPath { get; set; }
    }

    public class ImportProgress
    {
        public string ImportId { get; set; }
        public int TotalMessages { get; set; }
        public int ProcessedMessages { get; set; }
        public ImportStatus Status { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ExportProgress
    {
        public string ExportId { get; set; }
        public int TotalMessages { get; set; }
        public int ProcessedMessages { get; set; }
        public ExportStatus Status { get; set; }
        public string ErrorMessage { get; set; }
    }

    public enum ImportStatus
    {
        Pending,
        Processing,
        Completed,
        Failed
    }

    public enum ExportStatus
    {
        Pending,
        Processing,
        Completed,
        Failed
    }

    public class MessageService : IMessageService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MessageService> _logger;
        private readonly HashingService _hashingService;
        private readonly IStorageService _storageService;
        private readonly IUserRepository _userRepository;

        public MessageService(
            ApplicationDbContext context,
            ILogger<MessageService> logger,
            HashingService hashingService,
            IStorageService storageService,
            IUserRepository userRepository)
        {
            _context = context;
            _logger = logger;
            _hashingService = hashingService;
            _storageService = storageService;
            _userRepository = userRepository;
        }

        public async Task<Message> SendMessageAsync(string senderId, string chatRoomId, string content, List<string> attachmentUrls = null)
        {
            try
            {
                // Verify chat room exists and user is a participant
                var chatRoom = await _context.ChatRooms
                    .Include(c => c.Participants)
                    .FirstOrDefaultAsync(c => c.Id == chatRoomId);

                if (chatRoom == null)
                    throw new ArgumentException("Chat room not found");

                if (!chatRoom.Participants.Any(p => p.Id == senderId))
                    throw new UnauthorizedAccessException("User is not a participant in this chat room");

                // Create message
                var message = new Message
                {
                    Id = Guid.NewGuid().ToString(),
                    SenderId = senderId,
                    ChatRoomId = chatRoomId,
                    Content = content,
                    Timestamp = DateTime.UtcNow,
                    IsEdited = false,
                    IsDeleted = false,
                    Attachments = attachmentUrls?.Select(url => new Attachment
                    {
                        Id = Guid.NewGuid().ToString(),
                        MessageId = Guid.NewGuid().ToString(),
                        Url = url,
                        UploadedAt = DateTime.UtcNow
                    }).ToList()
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                // Log message history
                var history = new MessageHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    MessageId = message.Id,
                    Content = content,
                    Timestamp = DateTime.UtcNow,
                    Action = "Created"
                };

                _context.MessageHistories.Add(history);
                await _context.SaveChangesAsync();

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message from user {SenderId} to chat room {ChatRoomId}", senderId, chatRoomId);
                throw;
            }
        }

        public async Task<Message> EditMessageAsync(string messageId, string content, string userId)
        {
            try
            {
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    throw new ArgumentException("Message not found");

                if (message.SenderId != userId)
                    throw new UnauthorizedAccessException("User is not authorized to edit this message");

                if (message.IsDeleted)
                    throw new InvalidOperationException("Cannot edit a deleted message");

                // Store original content in history
                var history = new MessageHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    MessageId = message.Id,
                    Content = message.Content,
                    Timestamp = DateTime.UtcNow,
                    Action = "Edited"
                };

                _context.MessageHistories.Add(history);

                // Update message
                message.Content = content;
                message.IsEdited = true;
                message.EditedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message {MessageId} by user {UserId}", messageId, userId);
                throw;
            }
        }

        public async Task<bool> DeleteMessageAsync(string messageId, string userId)
        {
            try
            {
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    throw new ArgumentException("Message not found");

                if (message.SenderId != userId)
                    throw new UnauthorizedAccessException("User is not authorized to delete this message");

                // Store message in history before deletion
                var history = new MessageHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    MessageId = message.Id,
                    Content = message.Content,
                    Timestamp = DateTime.UtcNow,
                    Action = "Deleted"
                };

                _context.MessageHistories.Add(history);

                // Soft delete the message
                message.IsDeleted = true;
                message.DeletedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId} by user {UserId}", messageId, userId);
                throw;
            }
        }

        public async Task<Message> GetMessageAsync(string messageId)
        {
            try
            {
                return await _context.Messages
                    .Include(m => m.Attachments)
                    .FirstOrDefaultAsync(m => m.Id == messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving message {MessageId}", messageId);
                throw;
            }
        }

        public async Task<IEnumerable<Message>> GetChatHistoryAsync(string chatRoomId, int skip = 0, int take = 50)
        {
            try
            {
                return await _context.Messages
                    .Include(m => m.Attachments)
                    .Where(m => m.ChatRoomId == chatRoomId && !m.IsDeleted)
                    .OrderByDescending(m => m.Timestamp)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat history for room {ChatRoomId}", chatRoomId);
                throw;
            }
        }

        public async Task<bool> MarkMessageAsReadAsync(string messageId, string userId)
        {
            try
            {
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    throw new ArgumentException("Message not found");

                if (!message.ReadBy.Contains(userId))
                {
                    message.ReadBy.Add(userId);
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message {MessageId} as read by user {UserId}", messageId, userId);
                throw;
            }
        }

        public async Task<bool> HideMessageForUserAsync(string messageId, string userId)
        {
            try
            {
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    throw new ArgumentException("Message not found");

                if (!message.HiddenBy.Contains(userId))
                {
                    message.HiddenBy.Add(userId);
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hiding message {MessageId} for user {UserId}", messageId, userId);
                throw;
            }
        }

        public async Task<IEnumerable<Message>> GetUnreadMessagesAsync(string userId, string chatRoomId)
        {
            try
            {
                return await _context.Messages
                    .Where(m => m.ChatRoomId == chatRoomId && 
                               !m.IsDeleted && 
                               !m.ReadBy.Contains(userId))
                    .OrderByDescending(m => m.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unread messages for user {UserId} in room {ChatRoomId}", userId, chatRoomId);
                throw;
            }
        }

        public async Task<bool> PinMessageAsync(string messageId, string userId)
        {
            try
            {
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    throw new ArgumentException("Message not found");

                // Check if user has permission to pin messages
                var chatRoom = await _context.ChatRooms
                    .FirstOrDefaultAsync(c => c.Id == message.ChatRoomId);

                if (chatRoom.AdminId != userId)
                    throw new UnauthorizedAccessException("User is not authorized to pin messages");

                message.IsPinned = true;
                message.PinnedAt = DateTime.UtcNow;
                message.PinnedBy = userId;

                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinning message {MessageId} by user {UserId}", messageId, userId);
                throw;
            }
        }

        public async Task<bool> UnpinMessageAsync(string messageId, string userId)
        {
            try
            {
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    throw new ArgumentException("Message not found");

                // Check if user has permission to unpin messages
                var chatRoom = await _context.ChatRooms
                    .FirstOrDefaultAsync(c => c.Id == message.ChatRoomId);

                if (chatRoom.AdminId != userId)
                    throw new UnauthorizedAccessException("User is not authorized to unpin messages");

                message.IsPinned = false;
                message.PinnedAt = null;
                message.PinnedBy = null;

                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpinning message {MessageId} by user {UserId}", messageId, userId);
                throw;
            }
        }

        public async Task<IEnumerable<Message>> GetPinnedMessagesAsync(string chatRoomId)
        {
            try
            {
                return await _context.Messages
                    .Where(m => m.ChatRoomId == chatRoomId && 
                               m.IsPinned && 
                               !m.IsDeleted)
                    .OrderByDescending(m => m.PinnedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pinned messages for room {ChatRoomId}", chatRoomId);
                throw;
            }
        }

        public async Task<bool> ForwardMessageAsync(string messageId, string targetChatRoomId, string userId)
        {
            try
            {
                var originalMessage = await _context.Messages
                    .Include(m => m.Attachments)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (originalMessage == null)
                    throw new ArgumentException("Original message not found");

                // Verify target chat room exists and user is a participant
                var targetChatRoom = await _context.ChatRooms
                    .Include(c => c.Participants)
                    .FirstOrDefaultAsync(c => c.Id == targetChatRoomId);

                if (targetChatRoom == null)
                    throw new ArgumentException("Target chat room not found");

                if (!targetChatRoom.Participants.Any(p => p.Id == userId))
                    throw new UnauthorizedAccessException("User is not a participant in target chat room");

                // Create forwarded message
                var forwardedMessage = new Message
                {
                    Id = Guid.NewGuid().ToString(),
                    SenderId = userId,
                    ChatRoomId = targetChatRoomId,
                    Content = $"Forwarded: {originalMessage.Content}",
                    Timestamp = DateTime.UtcNow,
                    IsForwarded = true,
                    OriginalMessageId = messageId,
                    Attachments = originalMessage.Attachments?.Select(a => new Attachment
                    {
                        Id = Guid.NewGuid().ToString(),
                        MessageId = Guid.NewGuid().ToString(),
                        Url = a.Url,
                        UploadedAt = DateTime.UtcNow
                    }).ToList()
                };

                _context.Messages.Add(forwardedMessage);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding message {MessageId} to room {TargetChatRoomId} by user {UserId}", 
                    messageId, targetChatRoomId, userId);
                throw;
            }
        }

        public async Task<bool> ReactToMessageAsync(string messageId, string userId, string reaction)
        {
            try
            {
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    throw new ArgumentException("Message not found");

                if (message.Reactions.ContainsKey(userId))
                {
                    message.Reactions[userId] = reaction;
                }
                else
                {
                    message.Reactions.Add(userId, reaction);
                }

                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding reaction to message {MessageId} by user {UserId}", messageId, userId);
                throw;
            }
        }

        public async Task<bool> RemoveReactionAsync(string messageId, string userId, string reaction)
        {
            try
            {
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    throw new ArgumentException("Message not found");

                if (message.Reactions.ContainsKey(userId) && message.Reactions[userId] == reaction)
                {
                    message.Reactions.Remove(userId);
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing reaction from message {MessageId} by user {UserId}", messageId, userId);
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetMessageReactionsAsync(string messageId)
        {
            try
            {
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    throw new ArgumentException("Message not found");

                return message.Reactions
                    .GroupBy(r => r.Value)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reactions for message {MessageId}", messageId);
                throw;
            }
        }

        public async Task EditMessageAsync(Message message, string newContent, string userId, EditType editType, string? editReason = null)
        {
            if (message.IsDeleted)
                throw new InvalidOperationException("Silinmiş mesaj düzenlenemez");

            if (string.IsNullOrEmpty(newContent))
                throw new ArgumentException("Mesaj içeriği boş olamaz");

            if (message.SenderId != userId)
                throw new InvalidOperationException("Bu mesajı düzenleme yetkiniz yok");

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new InvalidOperationException("Kullanıcı bulunamadı");

            // Save old version to history
            message.EditHistory.Add(new MessageEdit
            {
                EditType = editType,
                EditReason = editReason,
                EditedAt = DateTime.UtcNow,
                Description = Message.GetEditDescription(editType, editReason)
            });

            // Update message
            message.Content = newContent;
            message.IsEdited = true;
            message.EditedAt = DateTime.UtcNow;
            message.Timestamp = DateTime.UtcNow;

            // Notify participants about edit
            if (message.ChatRoom != null)
            {
                foreach (var participant in message.ChatRoom.Participants.Where(p => p.Id != userId))
                {
                    var notification = new Notification
                    {
                        UserId = participant.Id,
                        User = participant,
                        MessageId = message.Id,
                        Type = NotificationType.MessageEdited,
                        Status = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    message.Notifications.Add(notification);
                }
            }
        }

        public async Task DeleteForUserAsync(Message message, string userId)
        {
            if (!message.HiddenForUsers.Contains(userId))
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    throw new InvalidOperationException("Kullanıcı bulunamadı");

                message.HiddenForUsers.Add(userId);

                var notification = new Notification
                {
                    UserId = userId,
                    User = user,
                    MessageId = message.Id,
                    Type = NotificationType.MessageDeleted,
                    Status = false,
                    CreatedAt = DateTime.UtcNow
                };
                message.Notifications.Add(notification);
            }
        }

        public async Task<IEnumerable<Message>> SearchMessagesAsync(string query, string userId, string? chatRoomId = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var messages = _context.Messages
                    .Include(m => m.Attachments)
                    .Where(m => !m.IsDeleted && m.Content.Contains(query));

                if (chatRoomId != null)
                    messages = messages.Where(m => m.ChatRoomId == chatRoomId);

                if (startDate != null)
                    messages = messages.Where(m => m.Timestamp >= startDate);

                if (endDate != null)
                    messages = messages.Where(m => m.Timestamp <= endDate);

                return await messages.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching messages");
                throw;
            }
        }

        public async Task<IEnumerable<Message>> FilterMessagesByTypeAsync(string userId, string chatRoomId, MessageType type)
        {
            try
            {
                var messages = _context.Messages
                    .Include(m => m.Attachments)
                    .Where(m => !m.IsDeleted && m.Type == type);

                if (chatRoomId != null)
                    messages = messages.Where(m => m.ChatRoomId == chatRoomId);

                return await messages.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering messages by type");
                throw;
            }
        }

        public async Task<IEnumerable<Message>> FilterMessagesByUserAsync(string userId, string chatRoomId, string targetUserId)
        {
            try
            {
                var messages = _context.Messages
                    .Include(m => m.Attachments)
                    .Where(m => !m.IsDeleted && m.SenderId == targetUserId);

                if (chatRoomId != null)
                    messages = messages.Where(m => m.ChatRoomId == chatRoomId);

                return await messages.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering messages by user");
                throw;
            }
        }

        public async Task<IEnumerable<Message>> FilterMessagesByContentAsync(string userId, string chatRoomId, string content)
        {
            try
            {
                var messages = _context.Messages
                    .Include(m => m.Attachments)
                    .Where(m => !m.IsDeleted && m.Content.Contains(content));

                if (chatRoomId != null)
                    messages = messages.Where(m => m.ChatRoomId == chatRoomId);

                return await messages.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering messages by content");
                throw;
            }
        }

        public async Task<IEnumerable<Message>> GetMessagesByDateRangeAsync(string userId, string chatRoomId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var messages = _context.Messages
                    .Include(m => m.Attachments)
                    .Where(m => !m.IsDeleted && m.Timestamp >= startDate && m.Timestamp <= endDate);

                if (chatRoomId != null)
                    messages = messages.Where(m => m.ChatRoomId == chatRoomId);

                return await messages.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving messages by date range");
                throw;
            }
        }

        public async Task<bool> BackupMessagesAsync(string userId, string chatRoomId)
        {
            try
            {
                var messages = await _context.Messages
                    .Where(m => !m.IsDeleted && m.SenderId == userId && m.ChatRoomId == chatRoomId)
                    .ToListAsync();

                if (messages.Count == 0)
                    return false;

                var backup = new MessageBackup
                {
                    Id = Guid.NewGuid().ToString(),
                    ChatRoomId = chatRoomId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    MessageCount = messages.Count,
                    BackupSize = messages.Sum(m => m.Attachments.Sum(a => a.Url.Length)),
                    BackupPath = await _storageService.SaveMessagesAsync(messages)
                };

                _context.MessageBackups.Add(backup);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error backing up messages");
                throw;
            }
        }

        public async Task<bool> RestoreMessagesFromBackupAsync(string userId, string chatRoomId, string backupId)
        {
            try
            {
                var backup = await _context.MessageBackups
                    .FirstOrDefaultAsync(b => b.Id == backupId && b.UserId == userId && b.ChatRoomId == chatRoomId);

                if (backup == null)
                    return false;

                var messages = await _storageService.LoadMessagesAsync(backup.BackupPath);

                _context.Messages.AddRange(messages);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring messages from backup");
                throw;
            }
        }

        public async Task<IEnumerable<MessageBackup>> GetMessageBackupsAsync(string userId, string chatRoomId)
        {
            try
            {
                return await _context.MessageBackups
                    .Where(b => b.UserId == userId && b.ChatRoomId == chatRoomId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving message backups");
                throw;
            }
        }

        public async Task<MessageBackup> GetBackupDetailsAsync(string backupId)
        {
            try
            {
                return await _context.MessageBackups
                    .FirstOrDefaultAsync(b => b.Id == backupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving backup details");
                throw;
            }
        }

        public async Task<bool> DeleteMessageBackupAsync(string backupId, string userId)
        {
            try
            {
                var backup = await _context.MessageBackups
                    .FirstOrDefaultAsync(b => b.Id == backupId && b.UserId == userId);

                if (backup == null)
                    return false;

                await _storageService.DeleteMessagesAsync(backup.BackupPath);

                _context.MessageBackups.Remove(backup);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message backup");
                throw;
            }
        }

        public async Task<byte[]> ExportMessagesAsync(string userId, string chatRoomId, ExportFormat format)
        {
            try
            {
                var messages = await _context.Messages
                    .Where(m => !m.IsDeleted && m.SenderId == userId && m.ChatRoomId == chatRoomId)
                    .ToListAsync();

                if (messages.Count == 0)
                    return null;

                return await _storageService.ExportMessagesAsync(messages, format);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting messages");
                throw;
            }
        }

        public async Task<bool> ImportMessagesAsync(string userId, string chatRoomId, byte[] data, ImportFormat format)
        {
            try
            {
                var messages = await _storageService.ImportMessagesAsync(data, format);

                _context.Messages.AddRange(messages);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing messages");
                throw;
            }
        }

        public async Task<bool> ValidateImportDataAsync(byte[] data, ImportFormat format)
        {
            try
            {
                return await _storageService.ValidateImportDataAsync(data, format);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating import data");
                throw;
            }
        }

        public async Task<ImportProgress> GetImportProgressAsync(string importId)
        {
            try
            {
                return await _storageService.GetImportProgressAsync(importId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving import progress");
                throw;
            }
        }

        public async Task<ExportProgress> GetExportProgressAsync(string exportId)
        {
            try
            {
                return await _storageService.GetExportProgressAsync(exportId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving export progress");
                throw;
            }
        }
    }
} 