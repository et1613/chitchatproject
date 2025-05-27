using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
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

        [HttpPost("hash-password")]
        public IActionResult HashPassword([FromBody] HashPasswordRequest request)
        {
            try
            {
                var hashedPassword = _hashingService.HashPassword(request.Password);
                return Ok(new { HashedPassword = hashedPassword });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing password");
                return StatusCode(500, "Error hashing password");
            }
        }

        [HttpPost("verify-password")]
        public IActionResult VerifyPassword([FromBody] VerifyPasswordRequest request)
        {
            try
            {
                var isValid = _hashingService.VerifyHash(request.Password, request.HashedPassword);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying password");
                return StatusCode(500, "Error verifying password");
            }
        }

        [HttpPost("hash-file")]
        public async Task<IActionResult> HashFile(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("No file uploaded");

                using var stream = file.OpenReadStream();
                var fileHash = await _hashingService.HashFileAsync(stream);
                return Ok(new { FileHash = fileHash });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing file");
                return StatusCode(500, "Error hashing file");
            }
        }

        [HttpPost("generate-secure-password")]
        public IActionResult GenerateSecurePassword([FromBody] GeneratePasswordRequest request)
        {
            try
            {
                var password = _hashingService.GenerateSecurePassword(request.Length);
                return Ok(new { Password = password });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating secure password");
                return StatusCode(500, "Error generating secure password");
            }
        }

        [HttpPost("generate-secure-filename")]
        public IActionResult GenerateSecureFileName([FromBody] GenerateFileNameRequest request)
        {
            try
            {
                var secureFileName = _hashingService.GenerateSecureFileName(request.OriginalFileName);
                return Ok(new { SecureFileName = secureFileName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating secure file name");
                return StatusCode(500, "Error generating secure file name");
            }
        }

        [HttpPost("generate-url-token")]
        public IActionResult GenerateUrlToken([FromBody] GenerateUrlTokenRequest request)
        {
            try
            {
                var token = _hashingService.GenerateSecureUrlToken(request.Url, request.Expiration);
                return Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating URL token");
                return StatusCode(500, "Error generating URL token");
            }
        }

        [HttpPost("verify-url-token")]
        public IActionResult VerifyUrlToken([FromBody] VerifyUrlTokenRequest request)
        {
            try
            {
                var isValid = _hashingService.ValidateSecureUrlToken(request.Token, request.Url);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying URL token");
                return StatusCode(500, "Error verifying URL token");
            }
        }

        [HttpPost("generate-access-token")]
        public IActionResult GenerateAccessToken([FromBody] GenerateAccessTokenRequest request)
        {
            try
            {
                var token = _hashingService.GenerateTemporaryAccessToken(request.UserId, request.Expiration);
                return Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating access token");
                return StatusCode(500, "Error generating access token");
            }
        }

        [HttpPost("verify-access-token")]
        public IActionResult VerifyAccessToken([FromBody] VerifyAccessTokenRequest request)
        {
            try
            {
                var isValid = _hashingService.ValidateTemporaryAccessToken(request.Token, out string userId);
                return Ok(new { IsValid = isValid, UserId = userId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying access token");
                return StatusCode(500, "Error verifying access token");
            }
        }
    }

    public class HashPasswordRequest
    {
        [Required]
        public string Password { get; set; }
    }

    public class VerifyPasswordRequest
    {
        [Required]
        public string Password { get; set; }

        [Required]
        public string HashedPassword { get; set; }
    }

    public class GeneratePasswordRequest
    {
        public int Length { get; set; } = 16;
    }

    public class GenerateFileNameRequest
    {
        [Required]
        public string OriginalFileName { get; set; }
    }

    public class GenerateUrlTokenRequest
    {
        [Required]
        public string Url { get; set; }
        public TimeSpan? Expiration { get; set; }
    }

    public class VerifyUrlTokenRequest
    {
        [Required]
        public string Token { get; set; }

        [Required]
        public string Url { get; set; }
    }

    public class GenerateAccessTokenRequest
    {
        [Required]
        public string UserId { get; set; }
        public TimeSpan? Expiration { get; set; }
    }

    public class VerifyAccessTokenRequest
    {
        [Required]
        public string Token { get; set; }
    }
} 