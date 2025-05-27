using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using WebApplication1.Models.Chat;
using WebApplication1.Models.Messages;
using System.Security.Claims;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;
        private readonly ApplicationDbContext _context;

        public ChatController(
            IChatService chatService,
            ILogger<ChatController> logger,
            ApplicationDbContext context)
        {
            _chatService = chatService;
            _logger = logger;
            _context = context;
        }

        [HttpPost("messages")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var message = await _chatService.SendMessageAsync(userId, request.ChatRoomId, request.Content);
                return Ok(message);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Message sending failed: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Message sending error");
                return StatusCode(500, "An error occurred while sending the message");
            }
        }

        [HttpGet("rooms/{chatRoomId}/messages")]
        public async Task<IActionResult> GetChatHistory(
            string chatRoomId,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var messages = await _chatService.GetChatHistoryAsync(chatRoomId, userId, skip, take);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat history");
                return StatusCode(500, "An error occurred while retrieving chat history");
            }
        }

        [HttpDelete("messages/{messageId}")]
        public async Task<IActionResult> DeleteMessage(string messageId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var result = await _chatService.DeleteMessageAsync(messageId, userId);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message");
                return StatusCode(500, "An error occurred while deleting the message");
            }
        }

        [HttpPut("messages/{messageId}")]
        public async Task<IActionResult> EditMessage(string messageId, [FromBody] EditMessageRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var result = await _chatService.EditMessageAsync(messageId, userId, request.NewContent);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message");
                return StatusCode(500, "An error occurred while editing the message");
            }
        }

        [HttpGet("rooms")]
        public async Task<IActionResult> GetChatRooms()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var chatRooms = await _context.ChatRooms
                    .Include(cr => cr.Participants)
                    .Where(cr => cr.Participants.Any(p => p.Id == userId))
                    .Select(cr => new
                    {
                        cr.Id,
                        cr.Name,
                        cr.Description,
                        cr.Picture,
                        cr.IsPrivate,
                        cr.CreatedAt,
                        ParticipantCount = cr.Participants.Count,
                        LastMessage = cr.Messages
                            .OrderByDescending(m => m.Timestamp)
                            .Select(m => new
                            {
                                m.Id,
                                m.Content,
                                m.Timestamp,
                                SenderName = m.Sender.UserName
                            })
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                return Ok(chatRooms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat rooms");
                return StatusCode(500, "An error occurred while retrieving chat rooms");
            }
        }

        [HttpGet("rooms/{chatRoomId}")]
        public async Task<IActionResult> GetChatRoom(string chatRoomId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var chatRoom = await _context.ChatRooms
                    .Include(cr => cr.Participants)
                    .Include(cr => cr.Messages)
                        .ThenInclude(m => m.Sender)
                    .FirstOrDefaultAsync(cr => cr.Id == chatRoomId);

                if (chatRoom == null)
                    return NotFound("Chat room not found");

                if (!chatRoom.Participants.Any(p => p.Id == userId))
                    return Forbid();

                var response = new
                {
                    chatRoom.Id,
                    chatRoom.Name,
                    chatRoom.Description,
                    chatRoom.Picture,
                    chatRoom.IsPrivate,
                    chatRoom.CreatedAt,
                    Participants = chatRoom.Participants.Select(p => new
                    {
                        p.Id,
                        p.UserName,
                        p.Email,
                        p.ProfilePicture
                    }),
                    Messages = chatRoom.Messages
                        .OrderByDescending(m => m.Timestamp)
                        .Take(50)
                        .Select(m => new
                        {
                            m.Id,
                            m.Content,
                            m.Timestamp,
                            m.IsEdited,
                            m.EditedAt,
                            Sender = new
                            {
                                m.Sender.Id,
                                m.Sender.UserName,
                                m.Sender.ProfilePicture
                            }
                        })
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat room");
                return StatusCode(500, "An error occurred while retrieving the chat room");
            }
        }

        [HttpPost("rooms")]
        public async Task<IActionResult> CreateChatRoom([FromBody] CreateChatRoomRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return BadRequest("User not found");

                var chatRoom = new ChatRoom
                {
                    Name = request.Name,
                    Description = request.Description,
                    IsPrivate = request.IsPrivate,
                    AdminId = userId,
                    Admin = user,
                    MaxParticipants = request.MaxParticipants,
                    AllowMessageEditing = request.AllowMessageEditing,
                    MessageEditTimeLimit = request.MessageEditTimeLimit,
                    MaxPinnedMessages = request.MaxPinnedMessages
                };

                chatRoom.Participants.Add(user);
                _context.ChatRooms.Add(chatRoom);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetChatRoom), new { chatRoomId = chatRoom.Id }, chatRoom);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat room");
                return StatusCode(500, "An error occurred while creating the chat room");
            }
        }

        [HttpPut("rooms/{chatRoomId}")]
        public async Task<IActionResult> UpdateChatRoom(string chatRoomId, [FromBody] UpdateChatRoomRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var chatRoom = await _context.ChatRooms
                    .Include(cr => cr.Admin)
                    .FirstOrDefaultAsync(cr => cr.Id == chatRoomId);

                if (chatRoom == null)
                    return NotFound("Chat room not found");

                if (chatRoom.AdminId != userId)
                    return Forbid();

                chatRoom.UpdateSettings(
                    request.Name,
                    request.Description,
                    request.IsPrivate,
                    request.MaxParticipants,
                    request.AllowMessageEditing,
                    request.MessageEditTimeLimit,
                    request.MaxPinnedMessages);

                await _context.SaveChangesAsync();

                return Ok(chatRoom);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chat room");
                return StatusCode(500, "An error occurred while updating the chat room");
            }
        }

        [HttpPost("rooms/{chatRoomId}/participants")]
        public async Task<IActionResult> AddParticipant(string chatRoomId, [FromBody] AddParticipantRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var chatRoom = await _context.ChatRooms
                    .Include(cr => cr.Participants)
                    .FirstOrDefaultAsync(cr => cr.Id == chatRoomId);

                if (chatRoom == null)
                    return NotFound("Chat room not found");

                if (chatRoom.AdminId != userId)
                    return Forbid();

                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                    return BadRequest("User not found");

                if (chatRoom.Participants.Count >= chatRoom.MaxParticipants)
                    return BadRequest("Chat room has reached maximum participant limit");

                chatRoom.AddParticipant(user);
                await _context.SaveChangesAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding participant");
                return StatusCode(500, "An error occurred while adding the participant");
            }
        }

        [HttpDelete("rooms/{chatRoomId}/participants/{participantId}")]
        public async Task<IActionResult> RemoveParticipant(string chatRoomId, string participantId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var chatRoom = await _context.ChatRooms
                    .Include(cr => cr.Participants)
                    .FirstOrDefaultAsync(cr => cr.Id == chatRoomId);

                if (chatRoom == null)
                    return NotFound("Chat room not found");

                if (chatRoom.AdminId != userId && userId != participantId)
                    return Forbid();

                var user = await _context.Users.FindAsync(participantId);
                if (user == null)
                    return BadRequest("User not found");

                chatRoom.RemoveParticipant(user);
                await _context.SaveChangesAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing participant");
                return StatusCode(500, "An error occurred while removing the participant");
            }
        }
    }

    public class SendMessageRequest
    {
        public string ChatRoomId { get; set; }
        public string Content { get; set; }
    }

    public class EditMessageRequest
    {
        public string NewContent { get; set; }
    }

    public class CreateChatRoomRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsPrivate { get; set; }
        public int MaxParticipants { get; set; } = 100;
        public bool AllowMessageEditing { get; set; } = true;
        public int MessageEditTimeLimit { get; set; } = 5;
        public int MaxPinnedMessages { get; set; } = 5;
    }

    public class UpdateChatRoomRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsPrivate { get; set; }
        public int MaxParticipants { get; set; }
        public bool AllowMessageEditing { get; set; }
        public int MessageEditTimeLimit { get; set; }
        public int MaxPinnedMessages { get; set; }
    }

    public class AddParticipantRequest
    {
        public string UserId { get; set; }
    }
} 