using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using WebApplication1.Models.Messages;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FileController : ControllerBase
    {
        private readonly IFileService _fileService;
        private readonly ILogger<FileController> _logger;

        public FileController(IFileService fileService, ILogger<FileController> logger)
        {
            _fileService = fileService;
            _logger = logger;
        }

        [HttpPost("upload")]
        public async Task<ActionResult<Attachment>> UploadFile(IFormFile file, string messageId)
        {
            try
            {
                if (file == null)
                    return BadRequest("Dosya boş olamaz");

                var userId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var attachment = await _fileService.UploadFileAsync(file, userId, messageId);
                return Ok(attachment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya yükleme hatası");
                return StatusCode(500, "Dosya yüklenirken bir hata oluştu");
            }
        }

        [HttpDelete("{fileId}")]
        public async Task<ActionResult> DeleteFile(string fileId)
        {
            try
            {
                var userId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _fileService.DeleteFileAsync(fileId, userId);
                if (result)
                    return Ok();
                else
                    return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya silme hatası");
                return StatusCode(500, "Dosya silinirken bir hata oluştu");
            }
        }

        [HttpGet("{fileId}")]
        public async Task<ActionResult<Attachment>> GetFile(string fileId)
        {
            try
            {
                var attachment = await _fileService.GetFileAsync(fileId);
                if (attachment == null)
                    return NotFound();

                return Ok(attachment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya getirme hatası");
                return StatusCode(500, "Dosya bilgileri alınırken bir hata oluştu");
            }
        }

        [HttpGet("{fileId}/url")]
        public async Task<ActionResult<string>> GetFileUrl(string fileId)
        {
            try
            {
                var url = await _fileService.GetFileUrlAsync(fileId);
                if (string.IsNullOrEmpty(url))
                    return NotFound();

                return Ok(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya URL getirme hatası");
                return StatusCode(500, "Dosya URL'si alınırken bir hata oluştu");
            }
        }

        [HttpPut("{fileId}/metadata")]
        public async Task<ActionResult> UpdateFileMetadata(string fileId, [FromBody] Dictionary<string, string> metadata)
        {
            try
            {
                var result = await _fileService.UpdateFileMetadataAsync(fileId, metadata);
                if (result)
                    return Ok();
                else
                    return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya metadata güncelleme hatası");
                return StatusCode(500, "Dosya metadata'sı güncellenirken bir hata oluştu");
            }
        }

        [HttpPost("{fileId}/thumbnail")]
        public async Task<ActionResult> GenerateThumbnail(string fileId)
        {
            try
            {
                var result = await _fileService.GenerateThumbnailAsync(fileId);
                if (result)
                    return Ok();
                else
                    return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Thumbnail oluşturma hatası");
                return StatusCode(500, "Thumbnail oluşturulurken bir hata oluştu");
            }
        }

        [HttpPost("validate")]
        public async Task<ActionResult<bool>> ValidateFile(IFormFile file)
        {
            try
            {
                if (file == null)
                    return BadRequest("Dosya boş olamaz");

                var isValid = await _fileService.ValidateFileAsync(file);
                return Ok(isValid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya doğrulama hatası");
                return StatusCode(500, "Dosya doğrulanırken bir hata oluştu");
            }
        }
    }
} 