using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using WebApplication1.Models.Tokens;
using WebApplication1.Models.Requests;
using WebApplication1.Models.Responses;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TokenController : ControllerBase
    {
        private readonly ITokenStorageService _tokenStorageService;
        private readonly IAuthService _authService;
        private readonly ILogger<TokenController> _logger;

        public TokenController(
            ITokenStorageService tokenStorageService,
            IAuthService authService,
            ILogger<TokenController> logger)
        {
            _tokenStorageService = tokenStorageService;
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("store")]
        public async Task<IActionResult> StoreToken([FromBody] StoreTokenRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _tokenStorageService.StoreTokenAsync(
                    userId,
                    request.Token,
                    request.TokenType,
                    request.ExpirationMinutes);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing token");
                return StatusCode(500, "Error storing token");
            }
        }

        [HttpGet("validate/{token}")]
        public async Task<IActionResult> ValidateToken(string token)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var isValid = await _tokenStorageService.ValidateTokenAsync(token, userId);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return StatusCode(500, "Error validating token");
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RefreshToken))
                {
                    return BadRequest(new { error = "Refresh token is required" });
                }

                var (accessToken, refreshToken) = await _authService.RefreshTokenAsync(request.RefreshToken);

                return Ok(new TokenResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(15)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return Unauthorized(new { error = "Invalid refresh token" });
            }
        }

        [Authorize]
        [HttpPost("revoke")]
        public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RefreshToken))
                {
                    return BadRequest(new { error = "Refresh token is required" });
                }

                await _tokenStorageService.RevokeTokenAsync(request.RefreshToken, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                return Ok(new { message = "Token revoked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                return BadRequest(new { error = "Error revoking token" });
            }
        }

        [Authorize]
        [HttpPost("revoke-all")]
        public async Task<IActionResult> RevokeAllTokens()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                await _tokenStorageService.RevokeAllTokensAsync(userId);
                return Ok(new { message = "All tokens revoked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all tokens");
                return BadRequest(new { error = "Error revoking all tokens" });
            }
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveTokens()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var tokens = await _tokenStorageService.GetActiveTokensAsync(userId);
                return Ok(tokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active tokens");
                return StatusCode(500, "Error getting active tokens");
            }
        }

        [HttpGet("info/{token}")]
        public async Task<IActionResult> GetTokenInfo(string token)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var tokenInfo = await _tokenStorageService.GetTokenInfoAsync(token, userId);
                return Ok(tokenInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token info");
                return StatusCode(500, "Error getting token info");
            }
        }

        [HttpPost("extend")]
        public async Task<IActionResult> ExtendTokenExpiration([FromBody] ExtendTokenRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _tokenStorageService.ExtendTokenExpirationAsync(
                    request.Token,
                    userId,
                    request.AdditionalMinutes);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extending token expiration");
                return StatusCode(500, "Error extending token expiration");
            }
        }

        [HttpGet("stats")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetTokenStats()
        {
            try
            {
                var stats = await _tokenStorageService.GetTokenStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token stats");
                return StatusCode(500, "Error getting token stats");
            }
        }

        [HttpPost("cleanup")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CleanupExpiredTokens()
        {
            try
            {
                var result = await _tokenStorageService.CleanupExpiredTokensAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired tokens");
                return StatusCode(500, "Error cleaning up expired tokens");
            }
        }

        [Authorize]
        [HttpGet("validate")]
        public async Task<IActionResult> ValidateToken()
        {
            try
            {
                var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                if (string.IsNullOrEmpty(token))
                {
                    return BadRequest(new { error = "Token is required" });
                }

                var isValid = await _authService.ValidateTokenAsync(token);
                return Ok(new { isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return BadRequest(new { error = "Error validating token" });
            }
        }
    }

    public class StoreTokenRequest
    {
        public required string Token { get; set; }
        public string TokenType { get; set; } = "Access";
        public int ExpirationMinutes { get; set; } = 60;
    }

    public class RefreshTokenRequest
    {
        public required string RefreshToken { get; set; }
    }

    public class ExtendTokenRequest
    {
        public required string Token { get; set; }
        public int AdditionalMinutes { get; set; } = 30;
    }
} 