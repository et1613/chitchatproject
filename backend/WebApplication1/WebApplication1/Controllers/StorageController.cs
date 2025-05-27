using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.IO;
using System.Threading;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StorageController : ControllerBase
    {
        private readonly IStorageService _storageService;

        public StorageController(IStorageService storageService)
        {
            _storageService = storageService;
        }

        // Temel dosya işlemleri
        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                var fileUrl = await _storageService.UploadFileAsync(file);
                return Ok(new { fileUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("upload/stream")]
        public async Task<IActionResult> UploadFileStream([FromBody] Stream fileStream, [FromQuery] string fileName)
        {
            try
            {
                var fileUrl = await _storageService.UploadFileAsync(fileStream, fileName);
                return Ok(new { fileUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{fileUrl}")]
        public async Task<IActionResult> DeleteFile(string fileUrl)
        {
            try
            {
                var result = await _storageService.DeleteFileAsync(fileUrl);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("exists/{fileUrl}")]
        public async Task<IActionResult> FileExists(string fileUrl)
        {
            try
            {
                var exists = await _storageService.FileExistsAsync(fileUrl);
                return Ok(new { exists });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
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

        [HttpGet("download/{fileUrl}")]
        public async Task<IActionResult> DownloadFile(string fileUrl)
        {
            try
            {
                var fileBytes = await _storageService.GetFileBytesAsync(fileUrl);
                var mimeType = await _storageService.GetFileMimeTypeAsync(fileUrl);
                return File(fileBytes, mimeType);
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
        [HttpPost("compress/{fileUrl}")]
        public async Task<IActionResult> CompressFile(string fileUrl, [FromQuery] int quality = 80)
        {
            try
            {
                var compressedUrl = await _storageService.CompressFileAsync(fileUrl, quality);
                return Ok(new { compressedUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

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
        [HttpGet("metadata/{fileUrl}")]
        public async Task<IActionResult> GetFileMetadata(string fileUrl)
        {
            try
            {
                var metadata = await _storageService.GetFileMetadataAsync(fileUrl);
                return Ok(metadata);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

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