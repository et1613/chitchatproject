using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EncryptionController : ControllerBase
    {
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<EncryptionController> _logger;

        public EncryptionController(
            IEncryptionService encryptionService,
            ILogger<EncryptionController> logger)
        {
            _encryptionService = encryptionService;
            _logger = logger;
        }

        [HttpPost("encrypt")]
        public async Task<IActionResult> EncryptData([FromBody] EncryptDataRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var encryptedData = await Task.Run(() =>
                {
                    using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(request.PlainText));
                    using var outputStream = new MemoryStream();

                    _encryptionService.EncryptFileWithAes(inputStream, outputStream, "default-key", "default-iv");
                    outputStream.Position = 0;

                    return Convert.ToBase64String(outputStream.ToArray());
                });

                return Ok(encryptedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting data");
                return StatusCode(500, "Error encrypting data");
            }
        }

        [HttpPost("decrypt")]
        public async Task<IActionResult> DecryptData([FromBody] DecryptDataRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var decryptedData = await Task.Run(() =>
                {
                    using var inputStream = new MemoryStream(Convert.FromBase64String(request.CipherText));
                    using var outputStream = new MemoryStream();

                    _encryptionService.DecryptFileWithAes(inputStream, outputStream, "default-key", "default-iv");
                    outputStream.Position = 0;

                    return Encoding.UTF8.GetString(outputStream.ToArray());
                });

                return Ok(decryptedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting data");
                return StatusCode(500, "Error decrypting data");
            }
        }

        [HttpPost("encrypt-file")]
        public IActionResult EncryptFile(IFormFile file, [FromQuery] string algorithm = "AES")
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                using var inputStream = file.OpenReadStream();
                using var outputStream = new MemoryStream();

                _encryptionService.EncryptFileWithAes(inputStream, outputStream, algorithm, algorithm);
                outputStream.Position = 0;

                return File(outputStream, "application/octet-stream", $"{file.FileName}.encrypted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting file");
                return StatusCode(500, "Error encrypting file");
            }
        }

        [HttpPost("decrypt-file")]
        public IActionResult DecryptFile(IFormFile file, [FromQuery] string algorithm = "AES")
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                using var inputStream = file.OpenReadStream();
                using var outputStream = new MemoryStream();

                _encryptionService.DecryptFileWithAes(inputStream, outputStream, algorithm, algorithm);
                outputStream.Position = 0;

                var fileName = file.FileName.EndsWith(".encrypted") 
                    ? file.FileName[..^10] 
                    : file.FileName;

                return File(outputStream, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting file");
                return StatusCode(500, "Error decrypting file");
            }
        }
    }

    public class EncryptDataRequest
    {
        [Required]
        [JsonPropertyName("data")]
        public string PlainText { get; set; } = string.Empty;
    }

    public class DecryptDataRequest
    {
        [Required]
        [JsonPropertyName("encryptedData")]
        public string CipherText { get; set; } = string.Empty;
    }
} 