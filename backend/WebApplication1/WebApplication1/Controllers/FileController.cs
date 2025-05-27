using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using System;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FileController : ControllerBase
    {
        private readonly IStorageService _storageService;
        private readonly ILogger<FileController> _logger;

        public FileController(IStorageService storageService, ILogger<FileController> logger)
        {
            _storageService = storageService;
            _logger = logger;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("Dosya boş olamaz");

                var fileUrl = await _storageService.UploadFileAsync(file.OpenReadStream(), file.FileName, file.ContentType);
                return Ok(new { fileUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya yüklenirken hata oluştu");
                return StatusCode(500, "Dosya yüklenirken bir hata oluştu");
            }
        }

        [HttpPost("upload/chunk")]
        public async Task<IActionResult> UploadFileChunk(IFormFile file, [FromQuery] string fileName, [FromQuery] string contentType)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("Dosya boş olamaz");

                var fileUrl = await _storageService.UploadFileInChunksAsync(file, fileName, contentType);
                return Ok(new { fileUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya chunk yüklenirken hata oluştu");
                return StatusCode(500, "Dosya yüklenirken bir hata oluştu");
            }
        }

        [HttpGet("download/{*fileUrl}")]
        public async Task<IActionResult> DownloadFile(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    return BadRequest("Dosya URL'i boş olamaz");

                var fileStream = await _storageService.DownloadFileAsync(fileUrl);
                var fileName = Path.GetFileName(fileUrl);

                return File(fileStream, "application/octet-stream", fileName);
            }
            catch (FileNotFoundException)
            {
                return NotFound("Dosya bulunamadı");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya indirilirken hata oluştu");
                return StatusCode(500, "Dosya indirilirken bir hata oluştu");
            }
        }

        [HttpGet("thumbnail/{*fileUrl}")]
        public async Task<IActionResult> GetThumbnail(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    return BadRequest("Dosya URL'i boş olamaz");

                var thumbnailStream = await _storageService.GetThumbnailAsync(fileUrl);
                return File(thumbnailStream, "image/jpeg");
            }
            catch (FileNotFoundException)
            {
                return NotFound("Thumbnail bulunamadı");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Thumbnail alınırken hata oluştu");
                return StatusCode(500, "Thumbnail alınırken bir hata oluştu");
            }
        }

        [HttpGet("metadata/{*fileUrl}")]
        public async Task<IActionResult> GetFileMetadata(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    return BadRequest("Dosya URL'i boş olamaz");

                var metadata = await _storageService.GetFileMetadataAsync(fileUrl);
                return Ok(metadata);
            }
            catch (FileNotFoundException)
            {
                return NotFound("Dosya metadata'sı bulunamadı");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya metadata'sı alınırken hata oluştu");
                return StatusCode(500, "Dosya metadata'sı alınırken bir hata oluştu");
            }
        }

        [HttpPost("compress/{*fileUrl}")]
        public async Task<IActionResult> CompressFile(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    return BadRequest("Dosya URL'i boş olamaz");

                var compressedFileUrl = await _storageService.CompressFileAsync(fileUrl);
                return Ok(new { compressedFileUrl });
            }
            catch (FileNotFoundException)
            {
                return NotFound("Dosya bulunamadı");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya sıkıştırılırken hata oluştu");
                return StatusCode(500, "Dosya sıkıştırılırken bir hata oluştu");
            }
        }

        [HttpPost("convert/{*fileUrl}")]
        public async Task<IActionResult> ConvertFileFormat(string fileUrl, [FromQuery] string targetFormat)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    return BadRequest("Dosya URL'i boş olamaz");

                if (string.IsNullOrEmpty(targetFormat))
                    return BadRequest("Hedef format belirtilmedi");

                var convertedFileUrl = await _storageService.ConvertFileFormatAsync(fileUrl, targetFormat);
                return Ok(new { convertedFileUrl });
            }
            catch (FileNotFoundException)
            {
                return NotFound("Dosya bulunamadı");
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya formatı dönüştürülürken hata oluştu");
                return StatusCode(500, "Dosya formatı dönüştürülürken bir hata oluştu");
            }
        }

        [HttpDelete("{*fileUrl}")]
        public async Task<IActionResult> DeleteFile(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    return BadRequest("Dosya URL'i boş olamaz");

                await _storageService.DeleteFileAsync(fileUrl);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya silinirken hata oluştu");
                return StatusCode(500, "Dosya silinirken bir hata oluştu");
            }
        }

        [HttpGet("exists/{*fileUrl}")]
        public async Task<IActionResult> FileExists(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    return BadRequest("Dosya URL'i boş olamaz");

                var exists = await _storageService.FileExistsAsync(fileUrl);
                return Ok(new { exists });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya kontrolü yapılırken hata oluştu");
                return StatusCode(500, "Dosya kontrolü yapılırken bir hata oluştu");
            }
        }
    }
} 