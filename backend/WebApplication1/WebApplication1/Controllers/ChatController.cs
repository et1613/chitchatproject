using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Messages;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using WebApplication1.Data;
using WebApplication1.Models.Requests;
using WebApplication1.Models.Users;
using WebApplication1.Repositories;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IFileService _fileService;
        private readonly ILogger<ChatController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IUserRepository _userRepository;

        public ChatController(
            IChatService chatService,
            IFileService fileService,
            ILogger<ChatController> logger,
            ApplicationDbContext context,
            IUserRepository userRepository)
        {
            _chatService = chatService;
            _fileService = fileService;
            _logger = logger;
            _context = context;
            _userRepository = userRepository;
        }

        [HttpPost("messages")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var message = await _chatService.SendMessageAsync(userId, request.ChatRoomId, request.Content);
                return Ok(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return StatusCode(500, "Error sending message");
            }
        }

        [HttpPost("direct-messages")]
        public async Task<IActionResult> SendDirectMessage([FromBody] SendDirectMessageRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return NotFound("User not found");

                await _chatService.SendDirectMessage(user, request.ReceiverId, request.Content);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending direct message");
                return StatusCode(500, "Error sending direct message");
            }
        }

        [HttpGet("rooms/{roomId}/messages")]
        public async Task<IActionResult> GetChatHistory(string roomId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var messages = await _chatService.GetChatHistoryAsync(roomId, userId, skip, take);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat history");
                return StatusCode(500, "Error getting chat history");
            }
        }

        [HttpDelete("messages/{messageId}")]
        public async Task<IActionResult> DeleteMessage(string messageId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _chatService.DeleteMessageAsync(messageId, userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message");
                return StatusCode(500, "Error deleting message");
            }
        }

        [HttpPut("messages/{messageId}")]
        public async Task<IActionResult> EditMessage(string messageId, [FromBody] EditMessageRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _chatService.EditMessageAsync(messageId, userId, request.NewContent);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message");
                return StatusCode(500, "Error editing message");
            }
        }

        [HttpPost("rooms")]
        public async Task<IActionResult> CreateChatRoom([FromBody] CreateChatRoomRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var chatRoom = await _chatService.CreateChatRoomAsync(request.Name, request.Description, userId);
                return Ok(chatRoom);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat room");
                return StatusCode(500, "Error creating chat room");
            }
        }

        [HttpGet("rooms")]
        public async Task<IActionResult> GetUserChatRooms()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var chatRooms = await _chatService.GetUserChatRoomsAsync(userId);
                return Ok(chatRooms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user chat rooms");
                return StatusCode(500, "Error getting user chat rooms");
            }
        }

        [HttpPost("rooms/{roomId}/users/{userId}")]
        public async Task<IActionResult> AddUserToChatRoom(string roomId, string userId)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                var result = await _chatService.AddUserToChatRoomAsync(userId, roomId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user to chat room");
                return StatusCode(500, "Error adding user to chat room");
            }
        }

        [HttpDelete("rooms/{roomId}/users/{userId}")]
        public async Task<IActionResult> RemoveUserFromChatRoom(string roomId, string userId)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                var result = await _chatService.RemoveUserFromChatRoomAsync(userId, roomId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user from chat room");
                return StatusCode(500, "Error removing user from chat room");
            }
        }

        [HttpPost("messages/{messageId}/files")]
        public async Task<IActionResult> UploadFile(string messageId, IFormFile file)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var attachment = await _fileService.UploadFileAsync(file, userId, messageId);
                return Ok(attachment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return StatusCode(500, "Error uploading file");
            }
        }

        [HttpPost("messages/{messageId}/files/multiple")]
        public async Task<IActionResult> UploadMultipleFiles(string messageId, List<IFormFile> files)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var attachments = await _fileService.UploadMultipleFilesAsync(files, userId, messageId);
                return Ok(attachments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading multiple files");
                return StatusCode(500, "Error uploading multiple files");
            }
        }

        [HttpGet("files/{fileId}")]
        public async Task<IActionResult> GetFile(string fileId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var file = await _fileService.GetFileAsync(fileId);
                return Ok(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file");
                return StatusCode(500, "Error getting file");
            }
        }

        [HttpDelete("files/{fileId}")]
        public async Task<IActionResult> DeleteFile(string fileId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _fileService.DeleteFileAsync(fileId, userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file");
                return StatusCode(500, "Error deleting file");
            }
        }

        [HttpGet("files/{fileId}/preview")]
        public async Task<IActionResult> GetFilePreview(string fileId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var previewUrl = await _fileService.GenerateFilePreviewAsync(fileId);
                return Ok(new { PreviewUrl = previewUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating file preview");
                return StatusCode(500, "Error generating file preview");
            }
        }

        [HttpPost("files/{fileId}/compress")]
        public async Task<IActionResult> CompressFile(string fileId, [FromQuery] int quality = 80)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var compressedFile = await _fileService.CompressFileAsync(fileId, quality);
                return Ok(compressedFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compressing file");
                return StatusCode(500, "Error compressing file");
            }
        }

        [HttpPost("files/{fileId}/optimize")]
        public async Task<IActionResult> OptimizeImage(string fileId, [FromQuery] int maxWidth = 1920, [FromQuery] int maxHeight = 1080)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var optimizedFile = await _fileService.OptimizeImageAsync(fileId, maxWidth, maxHeight);
                return Ok(optimizedFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing image");
                return StatusCode(500, "Error optimizing image");
            }
        }
    }

    public class SendMessageRequest
    {
        public required string ChatRoomId { get; set; }
        public required string Content { get; set; }
    }

    public class SendDirectMessageRequest
    {
        public required string ReceiverId { get; set; }
        public required string Content { get; set; }
    }

    public class EditMessageRequest
    {
        public required string NewContent { get; set; }
    }

    public class CreateChatRoomRequest
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
    }
} 