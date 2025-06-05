using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Security.Claims;
using WebApplication1.Models.Hashing;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class HashingController : ControllerBase
    {
        private readonly IHashingService _hashingService;
        private readonly ILogger<HashingController> _logger;

        public HashingController(
            IHashingService hashingService,
            ILogger<HashingController> logger)
        {
            _hashingService = hashingService;
            _logger = logger;
        }

        [HttpPost("hash")]
        public async Task<IActionResult> HashData([FromBody] HashDataRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var hashResult = await _hashingService.HashDataAsync(request.Data, request.Algorithm);
                return Ok(hashResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing data");
                return StatusCode(500, "Error hashing data");
            }
        }

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyHash([FromBody] VerifyHashRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var isValid = await _hashingService.VerifyHashAsync(request.Data, request.Hash, request.Algorithm);
                return Ok(new { IsValid = isValid });
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

                var salt = await _hashingService.GenerateSaltAsync(length);
                return Ok(new { Salt = salt });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating salt");
                return StatusCode(500, "Error generating salt");
            }
        }

        [HttpPost("hash-with-salt")]
        public async Task<IActionResult> HashWithSalt([FromBody] HashWithSaltRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var hashResult = await _hashingService.HashWithSaltAsync(request.Data, request.Salt, request.Algorithm);
                return Ok(hashResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing with salt");
                return StatusCode(500, "Error hashing with salt");
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

                var result = await _hashingService.StoreSecureDataAsync(request.Key, request.Data, request.ExpirationMinutes);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing secure data");
                return StatusCode(500, "Error storing secure data");
            }
        }

        [HttpGet("secure-storage/{key}")]
        public async Task<IActionResult> GetSecureData(string key)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var data = await _hashingService.GetSecureDataAsync(key);
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving secure data");
                return StatusCode(500, "Error retrieving secure data");
            }
        }

        [HttpDelete("secure-storage/{key}")]
        public async Task<IActionResult> DeleteSecureData(string key)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _hashingService.DeleteSecureDataAsync(key);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting secure data");
                return StatusCode(500, "Error deleting secure data");
            }
        }

        [HttpPost("hash-file")]
        public async Task<IActionResult> HashFile(IFormFile file, [FromQuery] string algorithm = "SHA256")
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                using var stream = file.OpenReadStream();
                var hashResult = await _hashingService.HashFileAsync(stream, algorithm);
                return Ok(hashResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing file");
                return StatusCode(500, "Error hashing file");
            }
        }

        [HttpPost("verify-file")]
        public async Task<IActionResult> VerifyFileHash(IFormFile file, [FromQuery] string hash, [FromQuery] string algorithm = "SHA256")
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                using var stream = file.OpenReadStream();
                var isValid = await _hashingService.VerifyFileHashAsync(stream, hash, algorithm);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying file hash");
                return StatusCode(500, "Error verifying file hash");
            }
        }
    }

    public class HashDataRequest
    {
        public required string Data { get; set; }
        public string Algorithm { get; set; } = "SHA256";
    }

    public class VerifyHashRequest
    {
        public required string Data { get; set; }
        public required string Hash { get; set; }
        public string Algorithm { get; set; } = "SHA256";
    }

    public class HashWithSaltRequest
    {
        public required string Data { get; set; }
        public required string Salt { get; set; }
        public string Algorithm { get; set; } = "SHA256";
    }

    public class StoreSecureDataRequest
    {
        public required string Key { get; set; }
        public required string Data { get; set; }
        public int ExpirationMinutes { get; set; } = 60;
    }
} 