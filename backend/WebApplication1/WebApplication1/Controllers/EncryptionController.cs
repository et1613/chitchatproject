using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Security.Claims;

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

                var encryptedData = await _encryptionService.EncryptDataAsync(
                    request.Data,
                    request.Algorithm,
                    request.KeySize);

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

                var decryptedData = await _encryptionService.DecryptDataAsync(
                    request.EncryptedData,
                    request.Algorithm,
                    request.Key);

                return Ok(decryptedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting data");
                return StatusCode(500, "Error decrypting data");
            }
        }

        [HttpPost("encrypt-file")]
        public async Task<IActionResult> EncryptFile(IFormFile file, [FromQuery] string algorithm = "AES")
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                using var inputStream = file.OpenReadStream();
                using var outputStream = new MemoryStream();

                await _encryptionService.EncryptFileWithAes(inputStream, outputStream, algorithm, algorithm);
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
        public async Task<IActionResult> DecryptFile(IFormFile file, [FromQuery] string algorithm = "AES")
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                using var inputStream = file.OpenReadStream();
                using var outputStream = new MemoryStream();

                await _encryptionService.DecryptFileWithAes(inputStream, outputStream, algorithm, algorithm);
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

        [HttpPost("generate-key")]
        public async Task<IActionResult> GenerateKey([FromBody] GenerateKeyRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var key = await _encryptionService.GenerateKeyAsync(
                    request.Algorithm,
                    request.KeySize);

                return Ok(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating key");
                return StatusCode(500, "Error generating key");
            }
        }

        [HttpPost("hash")]
        public async Task<IActionResult> HashData([FromBody] HashDataRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var hash = await _encryptionService.HashDataAsync(
                    request.Data,
                    request.Algorithm,
                    request.Salt);

                return Ok(hash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing data");
                return StatusCode(500, "Error hashing data");
            }
        }

        [HttpPost("verify-hash")]
        public async Task<IActionResult> VerifyHash([FromBody] VerifyHashRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var isValid = await _encryptionService.VerifyHashAsync(
                    request.Data,
                    request.Hash,
                    request.Algorithm);

                return Ok(new { isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying hash");
                return StatusCode(500, "Error verifying hash");
            }
        }

        [HttpPost("generate-salt")]
        public async Task<IActionResult> GenerateSalt([FromQuery] int length = 16)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var salt = await _encryptionService.GenerateSaltAsync(length);
                return Ok(salt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating salt");
                return StatusCode(500, "Error generating salt");
            }
        }

        [HttpPost("secure-storage")]
        public async Task<IActionResult> StoreSecureData([FromBody] StoreSecureDataRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _encryptionService.StoreSecureDataAsync(
                    request.Key,
                    request.Data,
                    request.Expiration);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing secure data");
                return StatusCode(500, "Error storing secure data");
            }
        }

        [HttpGet("secure-storage/{key}")]
        public async Task<IActionResult> RetrieveSecureData(string key)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var data = await _encryptionService.RetrieveSecureDataAsync(key);
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving secure data");
                return StatusCode(500, "Error retrieving secure data");
            }
        }
    }

    public class EncryptDataRequest
    {
        public required string Data { get; set; }
        public string? Algorithm { get; set; }
        public int? KeySize { get; set; }
    }

    public class DecryptDataRequest
    {
        public required string EncryptedData { get; set; }
        public required string Algorithm { get; set; }
        public required string Key { get; set; }
    }

    public class GenerateKeyRequest
    {
        public required string Algorithm { get; set; }
        public int? KeySize { get; set; }
    }

    public class HashDataRequest
    {
        public required string Data { get; set; }
        public string? Algorithm { get; set; }
        public string? Salt { get; set; }
    }

    public class VerifyHashRequest
    {
        public required string Data { get; set; }
        public required string Hash { get; set; }
        public string? Algorithm { get; set; }
    }

    public class StoreSecureDataRequest
    {
        public required string Key { get; set; }
        public required string Data { get; set; }
        public DateTime? Expiration { get; set; }
    }
} 