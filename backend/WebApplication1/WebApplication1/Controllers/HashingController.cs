using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Security.Claims;
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
        private readonly HashingService _hashingService;
        private readonly ILogger<HashingController> _logger;

        public HashingController(
            HashingService hashingService,
            ILogger<HashingController> logger)
        {
            _hashingService = hashingService;
            _logger = logger;
        }

        [HttpPost("hash")]
        public IActionResult HashData([FromBody] HashDataRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var hashResult = _hashingService.HashData(request.Data);
                return Ok(hashResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing data");
                return StatusCode(500, "Error hashing data");
            }
        }

        [HttpPost("verify")]
        public IActionResult VerifyHash([FromBody] VerifyHashRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var isValid = _hashingService.VerifyHash(request.Data, request.Hash);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying hash");
                return StatusCode(500, "Error verifying hash");
            }
        }

        [HttpPost("generate-salt")]
        public IActionResult GenerateSalt([FromQuery] int length = 16)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var salt = _hashingService.GenerateSalt();
                return Ok(new { Salt = salt });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating salt");
                return StatusCode(500, "Error generating salt");
            }
        }

        [HttpPost("hash-with-salt")]
        public IActionResult HashWithSalt([FromBody] HashWithSaltRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var hashResult = _hashingService.HashWithSalt(request.Data, request.Salt);
                return Ok(hashResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing with salt");
                return StatusCode(500, "Error hashing with salt");
            }
        }

        [HttpPost("hash-file")]
        public async Task<IActionResult> HashFile(IFormFile file)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                using var stream = file.OpenReadStream();
                var hashResult = await _hashingService.HashFileAsync(stream);
                return Ok(hashResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing file");
                return StatusCode(500, "Error hashing file");
            }
        }

        [HttpPost("verify-file")]
        public async Task<IActionResult> VerifyFileHash(IFormFile file, [FromQuery] string hash)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                using var stream = file.OpenReadStream();
                var fileHash = await _hashingService.HashFileAsync(stream);
                var isValid = fileHash == hash;
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
    }

    public class VerifyHashRequest
    {
        public required string Data { get; set; }
        public required string Hash { get; set; }
    }

    public class HashWithSaltRequest
    {
        public required string Data { get; set; }
        public required string Salt { get; set; }
    }
} 