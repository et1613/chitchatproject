using System.ComponentModel.DataAnnotations;
using WebApplication1.Models.Messages;
using Microsoft.AspNetCore.Http;
using WebApplication1.Models.Enums;
using WebApplication1.Services;

namespace WebApplication1.Models.Requests
{
    public class SendMessageRequest
    {
        [Required]
        public required string ChatRoomId { get; set; }

        [Required]
        public required string Content { get; set; }

        public List<string>? AttachmentUrls { get; set; }
    }

    public class SendDirectMessageRequest
    {
        [Required]
        public required string ReceiverId { get; set; }

        [Required]
        public required string Content { get; set; }
    }

    public class EditMessageRequest
    {
        [Required]
        public required string NewContent { get; set; }
    }

    public class CreateChatRoomRequest
    {
        [Required]
        public required string Name { get; set; }

        public string? Description { get; set; }
    }

    public class ForwardMessageRequest
    {
        [Required]
        public required string TargetChatRoomId { get; set; }
    }

    public class ReactToMessageRequest
    {
        [Required]
        public required string Reaction { get; set; }
    }

    public class SearchMessagesRequest
    {
        public string? Query { get; set; }
        public string? ChatRoomId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class FilterByTypeRequest
    {
        public required string ChatRoomId { get; set; }
        public required MessageType Type { get; set; }
    }

    public class FilterByUserRequest
    {
        public required string ChatRoomId { get; set; }
        public required string TargetUserId { get; set; }
    }

    public class FilterByContentRequest
    {
        public required string ChatRoomId { get; set; }
        public required string Content { get; set; }
    }

    public class DateRangeRequest
    {
        public required string ChatRoomId { get; set; }
        public required DateTime StartDate { get; set; }
        public required DateTime EndDate { get; set; }
    }

    public class BackupMessagesRequest
    {
        public required string ChatRoomId { get; set; }
    }

    public class RestoreMessagesRequest
    {
        public required string ChatRoomId { get; set; }
        public required string BackupId { get; set; }
    }

    public class ExportMessagesRequest
    {
        public required string ChatRoomId { get; set; }
        public required ExportFormat Format { get; set; }
    }

    public class ImportMessagesRequest
    {
        public required string ChatRoomId { get; set; }
        public required ImportFormat Format { get; set; }
        public required IFormFile File { get; set; }
    }

    public class ValidateImportRequest
    {
        public required ImportFormat Format { get; set; }
        public required IFormFile File { get; set; }
    }
} 