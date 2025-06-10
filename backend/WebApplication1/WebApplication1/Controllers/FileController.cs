using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Security.Claims;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FileController : ControllerBase
    {
        private readonly IFileService _fileService;
        private readonly ILogger<FileController> _logger;
        private readonly IStorageService _storageService;

        public FileController(
            IFileService fileService,
            ILogger<FileController> logger,
            IStorageService storageService)
        {
            _fileService = fileService;
            _logger = logger;
            _storageService = storageService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file, [FromQuery] string messageId)
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

        [HttpPost("upload/multiple")]
        public async Task<IActionResult> UploadMultipleFiles(List<IFormFile> files, [FromQuery] string messageId)
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

        [HttpGet("{fileId}")]
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

        [HttpGet("{fileId}/download")]
        public async Task<IActionResult> DownloadFile(string fileId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var attachment = await _fileService.GetFileAsync(fileId);
                if (attachment == null)
                    return NotFound();

                var fileUrl = await _fileService.GetFileUrlAsync(fileId);
                if (string.IsNullOrEmpty(fileUrl))
                    return NotFound();

                var fileBytes = await _storageService.GetFileBytesAsync(fileUrl);
                return File(fileBytes, attachment.MimeType, attachment.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file");
                return StatusCode(500, "Error downloading file");
            }
        }

        [HttpDelete("{fileId}")]
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

        [HttpGet("{fileId}/preview")]
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

        [HttpPost("{fileId}/compress")]
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

        [HttpPost("{fileId}/optimize")]
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

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserFiles(string userId)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                var files = await _fileService.SearchFilesAsync(userId, "", null);
                return Ok(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user files");
                return StatusCode(500, "Error getting user files");
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchFiles([FromQuery] string query, [FromQuery] string? fileType = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                if (string.IsNullOrEmpty(query))
                    return BadRequest("Search query cannot be empty");

                var files = await _fileService.SearchFilesAsync(userId, query, fileType);
                return Ok(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching files");
                return StatusCode(500, "Error searching files");
            }
        }

        [HttpPost("{fileId}/share")]
        public async Task<IActionResult> ShareFile(string fileId, [FromBody] ShareFileRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var permissions = new Dictionary<string, string>
                {
                    { request.SharedWithUserId, request.Permissions }
                };

                var result = await _fileService.SetFilePermissionsAsync(fileId, permissions);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sharing file");
                return StatusCode(500, "Error sharing file");
            }
        }

        [HttpGet("shared")]
        public async Task<IActionResult> GetSharedFiles()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Get all files and filter those that have permissions for this user
                var allFiles = await _fileService.SearchFilesAsync(userId, "", null);
                var sharedFiles = allFiles.Where(f => 
                    f.Metadata.ContainsKey($"Permission_{userId}") && 
                    f.Metadata[$"Permission_{userId}"] == "Read").ToList();

                return Ok(sharedFiles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shared files");
                return StatusCode(500, "Error getting shared files");
            }
        }

        [HttpPost("{fileId}/unshare")]
        public async Task<IActionResult> UnshareFile(string fileId, [FromBody] UnshareFileRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var permissions = new Dictionary<string, string>
                {
                    { request.SharedWithUserId, "None" }
                };

                var result = await _fileService.SetFilePermissionsAsync(fileId, permissions);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsharing file");
                return StatusCode(500, "Error unsharing file");
            }
        }
    }

    public class ShareFileRequest
    {
        public required string SharedWithUserId { get; set; }
        public required string Permissions { get; set; }
    }

    public class UnshareFileRequest
    {
        public required string SharedWithUserId { get; set; }
    }
} 