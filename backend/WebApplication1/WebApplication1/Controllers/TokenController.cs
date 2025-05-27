using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TokenController : ControllerBase
    {
        private readonly TokenStorageService _tokenService;
        private readonly ILogger<TokenController> _logger;

        public TokenController(TokenStorageService tokenService, ILogger<TokenController> logger)
        {
            _tokenService = tokenService;
            _logger = logger;
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<TokenResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _tokenService.RefreshTokenAsync(request.RefreshToken, userId);
                if (result == null)
                    return BadRequest("Invalid refresh token");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("revoke")]
        public async Task<ActionResult> RevokeToken([FromBody] RevokeTokenRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _tokenService.RevokeTokenAsync(request.Token, userId);
                if (!result)
                    return BadRequest("Invalid token");

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("revoke-all")]
        public async Task<ActionResult> RevokeAllTokens()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                await _tokenService.RevokeAllTokensAsync(userId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all tokens");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("validate")]
        public async Task<ActionResult<TokenValidationResponse>> ValidateToken([FromQuery] string token)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _tokenService.ValidateTokenAsync(token, userId);
                if (!result.IsValid)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("active")]
        public async Task<ActionResult<TokenInfoResponse>> GetActiveTokens()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var tokens = await _tokenService.GetActiveTokensAsync(userId);
                return Ok(tokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active tokens");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("stats")]
        public async Task<ActionResult<TokenStatsResponse>> GetTokenStats()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var stats = await _tokenService.GetTokenStatsAsync(userId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token stats");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("blacklist")]
        public async Task<ActionResult> BlacklistToken([FromBody] BlacklistTokenRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _tokenService.BlacklistTokenAsync(request.Token, userId, request.Reason);
                if (!result)
                    return BadRequest("Could not blacklist token");

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blacklisting token");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("blacklist")]
        public async Task<ActionResult<BlacklistedTokenResponse>> GetBlacklistedTokens()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var tokens = await _tokenService.GetBlacklistedTokensAsync(userId);
                return Ok(tokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blacklisted tokens");
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; }
    }

    public class RevokeTokenRequest
    {
        public string Token { get; set; }
    }

    public class BlacklistTokenRequest
    {
        public string Token { get; set; }
        public string Reason { get; set; }
    }

    public class TokenResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class TokenValidationResponse
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class TokenInfoResponse
    {
        public string Token { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string DeviceInfo { get; set; }
        public string IpAddress { get; set; }
    }

    public class TokenStatsResponse
    {
        public int TotalTokens { get; set; }
        public int ActiveTokens { get; set; }
        public int BlacklistedTokens { get; set; }
        public DateTime LastTokenCreated { get; set; }
        public DateTime LastTokenRevoked { get; set; }
    }

    public class BlacklistedTokenResponse
    {
        public string Token { get; set; }
        public DateTime BlacklistedAt { get; set; }
        public string Reason { get; set; }
        public string DeviceInfo { get; set; }
        public string IpAddress { get; set; }
    }
} 