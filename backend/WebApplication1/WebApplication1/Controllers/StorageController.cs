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
using WebApplication1.Models.Storage;

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

        // Temel dosya işlemleri
        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var fileUrl = await _storageService.UploadFileAsync(file.OpenReadStream(), file.FileName, file.ContentType);
                return Ok(new { FileUrl = fileUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return StatusCode(500, "Error uploading file");
            }
        }

        [HttpPost("upload/chunk")]
        public async Task<IActionResult> UploadFileChunk(IFormFile file, [FromQuery] string fileName, [FromQuery] string contentType)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var fileUrl = await _storageService.UploadFileInChunksAsync(file, fileName, contentType);
                return Ok(new { FileUrl = fileUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file chunk");
                return StatusCode(500, "Error uploading file chunk");
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

                var thumbnailStream = await _storageService.GetThumbnailAsync(fileUrl);
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

        [HttpGet("metadata/{*fileUrl}")]
        public async Task<IActionResult> GetFileMetadata(string fileUrl)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var metadata = await _storageService.GetFileMetadataAsync(fileUrl);
                return Ok(metadata);
            }
            catch (FileNotFoundException)
            {
                return NotFound("File metadata not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file metadata");
                return StatusCode(500, "Error getting file metadata");
            }
        }

        [HttpPost("compress/{*fileUrl}")]
        public async Task<IActionResult> CompressFile(string fileUrl, [FromQuery] int quality = 80)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var compressedFileUrl = await _storageService.CompressFileAsync(fileUrl, quality);
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

        [HttpPost("convert/{*fileUrl}")]
        public async Task<IActionResult> ConvertFileFormat(string fileUrl, [FromQuery] string targetFormat)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var convertedFileUrl = await _storageService.ConvertFileFormatAsync(fileUrl, targetFormat);
                return Ok(new { ConvertedFileUrl = convertedFileUrl });
            }
            catch (FileNotFoundException)
            {
                return NotFound("File not found");
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting file format");
                return StatusCode(500, "Error converting file format");
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

        [HttpGet("exists/{*fileUrl}")]
        public async Task<IActionResult> FileExists(string fileUrl)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var exists = await _storageService.FileExistsAsync(fileUrl);
                return Ok(new { Exists = exists });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking file existence");
                return StatusCode(500, "Error checking file existence");
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

                var usage = await _storageService.GetStorageUsageAsync(userId);
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
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? fileType = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var files = await _storageService.GetUserFilesAsync(userId, page, pageSize, fileType);
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
                var result = await _storageService.CleanupStorageAsync(daysOld);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up storage");
                return StatusCode(500, "Error cleaning up storage");
            }
        }

        [HttpGet("size/{fileUrl}")]
        public async Task<IActionResult> GetFileSize(string fileUrl)
        {
            try
            {
                var size = await _storageService.GetFileSizeAsync(fileUrl);
                return Ok(new { size });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("mime/{fileUrl}")]
        public async Task<IActionResult> GetFileMimeType(string fileUrl)
        {
            try
            {
                var mimeType = await _storageService.GetFileMimeTypeAsync(fileUrl);
                return Ok(new { mimeType });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("hash/{fileUrl}")]
        public async Task<IActionResult> GetFileHash(string fileUrl)
        {
            try
            {
                var hash = await _storageService.GetFileHashAsync(fileUrl);
                return Ok(new { hash });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Thumbnail işlemleri
        [HttpPost("thumbnail")]
        public async Task<IActionResult> GenerateThumbnail(IFormFile file)
        {
            try
            {
                var thumbnailUrl = await _storageService.GenerateThumbnailAsync(file);
                return Ok(new { thumbnailUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("thumbnail/{fileUrl}")]
        public async Task<IActionResult> GenerateThumbnailFromUrl(string fileUrl)
        {
            try
            {
                var thumbnailUrl = await _storageService.GenerateThumbnailAsync(fileUrl);
                return Ok(new { thumbnailUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Dosya optimizasyonu
        [HttpPost("optimize/image/{fileUrl}")]
        public async Task<IActionResult> OptimizeImage(string fileUrl, [FromQuery] int maxWidth = 1920, [FromQuery] int maxHeight = 1080)
        {
            try
            {
                var optimizedUrl = await _storageService.OptimizeImageAsync(fileUrl, maxWidth, maxHeight);
                return Ok(new { optimizedUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Dosya şifreleme/şifre çözme
        [HttpPost("encrypt/{fileUrl}")]
        public async Task<IActionResult> EncryptFile(string fileUrl, [FromBody] string encryptionKey)
        {
            try
            {
                var encryptedUrl = await _storageService.EncryptFileAsync(fileUrl, encryptionKey);
                return Ok(new { encryptedUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("decrypt/{fileUrl}")]
        public async Task<IActionResult> DecryptFile(string fileUrl, [FromBody] string encryptionKey)
        {
            try
            {
                var decryptedUrl = await _storageService.DecryptFileAsync(fileUrl, encryptionKey);
                return Ok(new { decryptedUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Dosya versiyonlama
        [HttpPost("version/{fileUrl}")]
        public async Task<IActionResult> CreateFileVersion(string fileUrl)
        {
            try
            {
                var versionId = await _storageService.CreateFileVersionAsync(fileUrl);
                return Ok(new { versionId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("versions/{fileUrl}")]
        public async Task<IActionResult> GetFileVersions(string fileUrl)
        {
            try
            {
                var versions = await _storageService.GetFileVersionsAsync(fileUrl);
                return Ok(new { versions });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Dosya metadata
        [HttpPut("metadata/{fileUrl}")]
        public async Task<IActionResult> UpdateFileMetadata(string fileUrl, [FromBody] Dictionary<string, string> metadata)
        {
            try
            {
                var result = await _storageService.UpdateFileMetadataAsync(fileUrl, metadata);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Dosya izinleri
        [HttpPut("permissions/{fileUrl}")]
        public async Task<IActionResult> SetFilePermissions(string fileUrl, [FromBody] Dictionary<string, string> permissions)
        {
            try
            {
                var result = await _storageService.SetFilePermissionsAsync(fileUrl, permissions);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("permissions/{fileUrl}")]
        public async Task<IActionResult> GetFilePermissions(string fileUrl)
        {
            try
            {
                var permissions = await _storageService.GetFilePermissionsAsync(fileUrl);
                return Ok(permissions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Dosya paylaşımı
        [HttpPost("share/{fileUrl}")]
        public async Task<IActionResult> GenerateShareableLink(string fileUrl, [FromQuery] TimeSpan? expiration = null)
        {
            try
            {
                var shareableLink = await _storageService.GenerateShareableLinkAsync(fileUrl, expiration);
                return Ok(new { shareableLink });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("share/{fileUrl}")]
        public async Task<IActionResult> RevokeShareableLink(string fileUrl)
        {
            try
            {
                var result = await _storageService.RevokeShareableLinkAsync(fileUrl);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Toplu işlemler
        [HttpPost("upload/multiple")]
        public async Task<IActionResult> UploadMultipleFiles(List<IFormFile> files)
        {
            try
            {
                var results = await _storageService.UploadMultipleFilesAsync(files);
                return Ok(results);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("delete/multiple")]
        public async Task<IActionResult> DeleteMultipleFiles([FromBody] List<string> fileUrls)
        {
            try
            {
                var result = await _storageService.DeleteMultipleFilesAsync(fileUrls);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Dosya arama
        [HttpGet("search")]
        public async Task<IActionResult> SearchFiles([FromQuery] string directory, [FromQuery] string searchTerm, [FromQuery] bool recursive = false)
        {
            try
            {
                var results = await _storageService.SearchFilesAsync(directory, searchTerm, recursive);
                return Ok(new { results });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Dosya istatistikleri
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStorageStatistics([FromQuery] string directory = null)
        {
            try
            {
                var statistics = await _storageService.GetStorageStatisticsAsync(directory);
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Dosya performans metrikleri
        [HttpGet("performance/{fileUrl}")]
        public async Task<IActionResult> GetFilePerformanceMetrics(string fileUrl)
        {
            try
            {
                var metrics = await _storageService.GetFilePerformanceMetricsAsync(fileUrl);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Dosya etiketleme
        [HttpPost("tag/{fileUrl}")]
        public async Task<IActionResult> AddFileTag(string fileUrl, [FromBody] string tag)
        {
            try
            {
                var result = await _storageService.AddFileTagAsync(fileUrl, tag);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("tags/{fileUrl}")]
        public async Task<IActionResult> GetFileTags(string fileUrl)
        {
            try
            {
                var tags = await _storageService.GetFileTagsAsync(fileUrl);
                return Ok(new { tags });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
} 