using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Security.Claims;
using WebApplication1.Models.Files;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FileController : ControllerBase
    {
        private readonly IFileService _fileService;
        private readonly ILogger<FileController> _logger;

        public FileController(
            IFileService fileService,
            ILogger<FileController> logger)
        {
            _fileService = fileService;
            _logger = logger;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var attachment = await _fileService.UploadFileAsync(file, userId);
                return Ok(attachment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return StatusCode(500, "Error uploading file");
            }
        }

        [HttpPost("upload/multiple")]
        public async Task<IActionResult> UploadMultipleFiles(List<IFormFile> files)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var attachments = await _fileService.UploadMultipleFilesAsync(files, userId);
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

                var (stream, fileName, contentType) = await _fileService.DownloadFileAsync(fileId);
                return File(stream, contentType, fileName);
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

                var files = await _fileService.GetUserFilesAsync(userId);
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

                var files = await _fileService.SearchFilesAsync(query, fileType);
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

                var result = await _fileService.ShareFileAsync(fileId, request.SharedWithUserId, request.Permissions);
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

                var files = await _fileService.GetSharedFilesAsync(userId);
                return Ok(files);
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

                var result = await _fileService.UnshareFileAsync(fileId, request.SharedWithUserId);
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
} 