using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.IO;
using System.Threading;
using WebApplication1.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using System.IO.Compression;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StorageController : ControllerBase
    {
        private readonly IStorageService _storageService;
        private readonly ILogger<StorageController> _logger;

        public StorageController(
            IStorageService storageService,
            ILogger<StorageController> logger)
        {
            _storageService = storageService;
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

                var fileUrl = await _storageService.UploadFileAsync(file);
                return Ok(new { FileUrl = fileUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return StatusCode(500, "Error uploading file");
            }
        }

        [HttpGet("download/{*fileUrl}")]
        public async Task<IActionResult> DownloadFile(string fileUrl)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var fileStream = await _storageService.DownloadFileAsync(fileUrl);
                var fileName = Path.GetFileName(fileUrl);

                return File(fileStream, "application/octet-stream", fileName);
            }
            catch (FileNotFoundException)
            {
                return NotFound("File not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file");
                return StatusCode(500, "Error downloading file");
            }
        }

        [HttpGet("thumbnail/{*fileUrl}")]
        public async Task<IActionResult> GetThumbnail(string fileUrl)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var thumbnailUrl = await _storageService.GenerateThumbnailAsync(fileUrl);
                var thumbnailStream = await _storageService.DownloadFileAsync(thumbnailUrl);
                return File(thumbnailStream, "image/jpeg");
            }
            catch (FileNotFoundException)
            {
                return NotFound("Thumbnail not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting thumbnail");
                return StatusCode(500, "Error getting thumbnail");
            }
        }

        [HttpPost("compress/{*fileUrl}")]
        public async Task<IActionResult> CompressFile(string fileUrl)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var compressedFileUrl = await _storageService.CompressFileAsync(fileUrl, CompressionLevel.Optimal);
                return Ok(new { CompressedFileUrl = compressedFileUrl });
            }
            catch (FileNotFoundException)
            {
                return NotFound("File not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compressing file");
                return StatusCode(500, "Error compressing file");
            }
        }

        [HttpDelete("{*fileUrl}")]
        public async Task<IActionResult> DeleteFile(string fileUrl)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                await _storageService.DeleteFileAsync(fileUrl);
                return Ok();
            }
            catch (FileNotFoundException)
            {
                return NotFound("File not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file");
                return StatusCode(500, "Error deleting file");
            }
        }

        [HttpGet("usage")]
        public async Task<IActionResult> GetStorageUsage()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var usage = await _storageService.GetStorageUsageByUserAsync(userId);
                return Ok(usage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage usage");
                return StatusCode(500, "Error getting storage usage");
            }
        }

        [HttpGet("files")]
        public async Task<IActionResult> GetUserFiles(
            [FromQuery] string? fileType = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var files = await _storageService.SearchFilesAsync(userId, "", false);
                return Ok(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user files");
                return StatusCode(500, "Error getting user files");
            }
        }

        [HttpPost("cleanup")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CleanupStorage([FromQuery] int daysOld = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
                var files = await _storageService.GetFilesByDateRangeAsync("", cutoffDate, DateTime.UtcNow);
                
                var deletedFiles = new List<string>();
                foreach (var fileUrl in files)
                {
                    try
                    {
                        await _storageService.DeleteFileAsync(fileUrl);
                        deletedFiles.Add(fileUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting file {FileUrl}", fileUrl);
                    }
                }

                return Ok(new { DeletedFiles = deletedFiles, Count = deletedFiles.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up storage");
                return StatusCode(500, "Error cleaning up storage");
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchFiles([FromQuery] string directory, [FromQuery] string searchTerm, [FromQuery] bool recursive = false)
        {
            try
            {
                var results = await _storageService.SearchFilesAsync(directory, searchTerm, recursive);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching files");
                return StatusCode(500, "Error searching files");
            }
        }
    }
} 