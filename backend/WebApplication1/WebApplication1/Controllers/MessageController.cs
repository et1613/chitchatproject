using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using WebApplication1.Models.Messages;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using WebApplication1.Models.Requests;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessageController : ControllerBase
    {
        private readonly IMessageService _messageService;
        private readonly ILogger<MessageController> _logger;

        public MessageController(
            IMessageService messageService,
            ILogger<MessageController> logger)
        {
            _messageService = messageService;
            _logger = logger;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var message = await _messageService.SendMessageAsync(
                    senderId: userId,
                    chatRoomId: request.ChatRoomId,
                    content: request.Content,
                    attachmentUrls: request.AttachmentUrls
                );

                return Ok(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return StatusCode(500, "Error sending message");
            }
        }

        [HttpPost("edit/{messageId}")]
        public async Task<IActionResult> EditMessage(string messageId, [FromBody] EditMessageRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var message = await _messageService.EditMessageAsync(
                    messageId: messageId,
                    content: request.Content,
                    userId: userId
                );

                return Ok(message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message {MessageId}", messageId);
                return StatusCode(500, "Error editing message");
            }
        }

        [HttpDelete("{messageId}")]
        public async Task<IActionResult> DeleteMessage(string messageId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _messageService.DeleteMessageAsync(messageId, userId);
                return Ok(new { Success = result });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId}", messageId);
                return StatusCode(500, "Error deleting message");
            }
        }

        [HttpGet("{messageId}")]
        public async Task<IActionResult> GetMessage(string messageId)
        {
            try
            {
                var message = await _messageService.GetMessageAsync(messageId);
                if (message == null)
                    return NotFound();

                return Ok(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving message {MessageId}", messageId);
                return StatusCode(500, "Error retrieving message");
            }
        }

        [HttpGet("chat/{chatRoomId}")]
        public async Task<IActionResult> GetChatHistory(string chatRoomId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            try
            {
                var messages = await _messageService.GetChatHistoryAsync(chatRoomId, skip, take);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat history for room {ChatRoomId}", chatRoomId);
                return StatusCode(500, "Error retrieving chat history");
            }
        }

        [HttpPost("{messageId}/read")]
        public async Task<IActionResult> MarkAsRead(string messageId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _messageService.MarkMessageAsReadAsync(messageId, userId);
                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message {MessageId} as read", messageId);
                return StatusCode(500, "Error marking message as read");
            }
        }

        [HttpPost("{messageId}/hide")]
        public async Task<IActionResult> HideMessage(string messageId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _messageService.HideMessageForUserAsync(messageId, userId);
                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hiding message {MessageId}", messageId);
                return StatusCode(500, "Error hiding message");
            }
        }

        [HttpGet("unread/{chatRoomId}")]
        public async Task<IActionResult> GetUnreadMessages(string chatRoomId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var messages = await _messageService.GetUnreadMessagesAsync(userId, chatRoomId);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unread messages for room {ChatRoomId}", chatRoomId);
                return StatusCode(500, "Error retrieving unread messages");
            }
        }

        [HttpPost("{messageId}/pin")]
        public async Task<IActionResult> PinMessage(string messageId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _messageService.PinMessageAsync(messageId, userId);
                return Ok(new { Success = result });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinning message {MessageId}", messageId);
                return StatusCode(500, "Error pinning message");
            }
        }

        [HttpPost("{messageId}/unpin")]
        public async Task<IActionResult> UnpinMessage(string messageId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _messageService.UnpinMessageAsync(messageId, userId);
                return Ok(new { Success = result });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpinning message {MessageId}", messageId);
                return StatusCode(500, "Error unpinning message");
            }
        }

        [HttpGet("pinned/{chatRoomId}")]
        public async Task<IActionResult> GetPinnedMessages(string chatRoomId)
        {
            try
            {
                var messages = await _messageService.GetPinnedMessagesAsync(chatRoomId);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pinned messages for room {ChatRoomId}", chatRoomId);
                return StatusCode(500, "Error retrieving pinned messages");
            }
        }

        [HttpPost("{messageId}/forward")]
        public async Task<IActionResult> ForwardMessage(string messageId, [FromBody] ForwardMessageRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _messageService.ForwardMessageAsync(
                    messageId: messageId,
                    targetChatRoomId: request.TargetChatRoomId,
                    userId: userId
                );

                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding message {MessageId}", messageId);
                return StatusCode(500, "Error forwarding message");
            }
        }

        [HttpPost("{messageId}/react")]
        public async Task<IActionResult> ReactToMessage(string messageId, [FromBody] ReactToMessageRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _messageService.ReactToMessageAsync(
                    messageId: messageId,
                    userId: userId,
                    reaction: request.Reaction
                );

                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding reaction to message {MessageId}", messageId);
                return StatusCode(500, "Error adding reaction");
            }
        }

        [HttpDelete("{messageId}/reactions/{reaction}")]
        public async Task<IActionResult> RemoveReaction(string messageId, string reaction)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _messageService.RemoveReactionAsync(
                    messageId: messageId,
                    userId: userId,
                    reaction: reaction
                );

                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing reaction from message {MessageId}", messageId);
                return StatusCode(500, "Error removing reaction");
            }
        }

        [HttpGet("{messageId}/reactions")]
        public async Task<IActionResult> GetMessageReactions(string messageId)
        {
            try
            {
                var reactions = await _messageService.GetMessageReactionsAsync(messageId);
                return Ok(reactions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reactions for message {MessageId}", messageId);
                return StatusCode(500, "Error getting reactions");
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchMessages([FromQuery] SearchMessagesRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var messages = await _messageService.SearchMessagesAsync(
                    query: request.Query,
                    userId: userId,
                    chatRoomId: request.ChatRoomId,
                    startDate: request.StartDate,
                    endDate: request.EndDate
                );

                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching messages");
                return StatusCode(500, "Error searching messages");
            }
        }

        [HttpGet("filter/type")]
        public async Task<IActionResult> FilterMessagesByType([FromQuery] FilterByTypeRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var messages = await _messageService.FilterMessagesByTypeAsync(
                    userId: userId,
                    chatRoomId: request.ChatRoomId,
                    type: request.Type
                );

                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering messages by type");
                return StatusCode(500, "Error filtering messages");
            }
        }

        [HttpGet("filter/user")]
        public async Task<IActionResult> FilterMessagesByUser([FromQuery] FilterByUserRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var messages = await _messageService.FilterMessagesByUserAsync(
                    userId: userId,
                    chatRoomId: request.ChatRoomId,
                    targetUserId: request.TargetUserId
                );

                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering messages by user");
                return StatusCode(500, "Error filtering messages");
            }
        }

        [HttpGet("filter/content")]
        public async Task<IActionResult> FilterMessagesByContent([FromQuery] FilterByContentRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var messages = await _messageService.FilterMessagesByContentAsync(
                    userId: userId,
                    chatRoomId: request.ChatRoomId,
                    content: request.Content
                );

                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering messages by content");
                return StatusCode(500, "Error filtering messages");
            }
        }

        [HttpGet("date-range")]
        public async Task<IActionResult> GetMessagesByDateRange([FromQuery] DateRangeRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var messages = await _messageService.GetMessagesByDateRangeAsync(
                    userId: userId,
                    chatRoomId: request.ChatRoomId,
                    startDate: request.StartDate,
                    endDate: request.EndDate
                );

                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving messages by date range");
                return StatusCode(500, "Error retrieving messages");
            }
        }

        [HttpPost("backup")]
        public async Task<IActionResult> BackupMessages([FromBody] BackupMessagesRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _messageService.BackupMessagesAsync(
                    userId: userId,
                    chatRoomId: request.ChatRoomId
                );

                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error backing up messages");
                return StatusCode(500, "Error backing up messages");
            }
        }

        [HttpPost("restore")]
        public async Task<IActionResult> RestoreMessages([FromBody] RestoreMessagesRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _messageService.RestoreMessagesFromBackupAsync(
                    userId: userId,
                    chatRoomId: request.ChatRoomId,
                    backupId: request.BackupId
                );

                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring messages");
                return StatusCode(500, "Error restoring messages");
            }
        }

        [HttpGet("backups")]
        public async Task<IActionResult> GetMessageBackups([FromQuery] string chatRoomId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var backups = await _messageService.GetMessageBackupsAsync(
                    userId: userId,
                    chatRoomId: chatRoomId
                );

                return Ok(backups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving message backups");
                return StatusCode(500, "Error retrieving backups");
            }
        }

        [HttpGet("backups/{backupId}")]
        public async Task<IActionResult> GetBackupDetails(string backupId)
        {
            try
            {
                var backup = await _messageService.GetBackupDetailsAsync(backupId);
                if (backup == null)
                    return NotFound();

                return Ok(backup);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving backup details");
                return StatusCode(500, "Error retrieving backup details");
            }
        }

        [HttpDelete("backups/{backupId}")]
        public async Task<IActionResult> DeleteMessageBackup(string backupId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _messageService.DeleteMessageBackupAsync(
                    backupId: backupId,
                    userId: userId
                );

                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message backup");
                return StatusCode(500, "Error deleting backup");
            }
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportMessages([FromQuery] ExportMessagesRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var data = await _messageService.ExportMessagesAsync(
                    userId: userId,
                    chatRoomId: request.ChatRoomId,
                    format: request.Format
                );

                if (data == null)
                    return NotFound();

                return File(data, GetContentType(request.Format), $"messages_{DateTime.UtcNow:yyyyMMddHHmmss}.{GetFileExtension(request.Format)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting messages");
                return StatusCode(500, "Error exporting messages");
            }
        }

        [HttpPost("import")]
        public async Task<IActionResult> ImportMessages([FromForm] ImportMessagesRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                if (request.File == null || request.File.Length == 0)
                    return BadRequest("No file uploaded");

                using var memoryStream = new MemoryStream();
                await request.File.CopyToAsync(memoryStream);
                var data = memoryStream.ToArray();

                var result = await _messageService.ImportMessagesAsync(
                    userId: userId,
                    chatRoomId: request.ChatRoomId,
                    data: data,
                    format: request.Format
                );

                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing messages");
                return StatusCode(500, "Error importing messages");
            }
        }

        [HttpPost("import/validate")]
        public async Task<IActionResult> ValidateImportData([FromForm] ValidateImportRequest request)
        {
            try
            {
                if (request.File == null || request.File.Length == 0)
                    return BadRequest("No file uploaded");

                using var memoryStream = new MemoryStream();
                await request.File.CopyToAsync(memoryStream);
                var data = memoryStream.ToArray();

                var isValid = await _messageService.ValidateImportDataAsync(
                    data: data,
                    format: request.Format
                );

                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating import data");
                return StatusCode(500, "Error validating import data");
            }
        }

        [HttpGet("import/progress/{importId}")]
        public async Task<IActionResult> GetImportProgress(string importId)
        {
            try
            {
                var progress = await _messageService.GetImportProgressAsync(importId);
                return Ok(progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving import progress");
                return StatusCode(500, "Error retrieving import progress");
            }
        }

        [HttpGet("export/progress/{exportId}")]
        public async Task<IActionResult> GetExportProgress(string exportId)
        {
            try
            {
                var progress = await _messageService.GetExportProgressAsync(exportId);
                return Ok(progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving export progress");
                return StatusCode(500, "Error retrieving export progress");
            }
        }

        private string GetContentType(ExportFormat format)
        {
            return format switch
            {
                ExportFormat.Json => "application/json",
                ExportFormat.Csv => "text/csv",
                ExportFormat.Xml => "application/xml",
                ExportFormat.Pdf => "application/pdf",
                _ => "application/octet-stream"
            };
        }

        private string GetFileExtension(ExportFormat format)
        {
            return format switch
            {
                ExportFormat.Json => "json",
                ExportFormat.Csv => "csv",
                ExportFormat.Xml => "xml",
                ExportFormat.Pdf => "pdf",
                _ => "bin"
            };
        }
    }
} 