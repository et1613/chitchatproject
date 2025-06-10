using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using WebApplication1.Models.Requests;
using WebApplication1.Models.Responses;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TokenController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly IAuthService _authService;
        private readonly ILogger<TokenController> _logger;
        private readonly ApplicationDbContext _context;

        public TokenController(
            ITokenService tokenService,
            IAuthService authService,
            ILogger<TokenController> logger,
            ApplicationDbContext context)
        {
            _tokenService = tokenService;
            _authService = authService;
            _logger = logger;
            _context = context;
        }

        [HttpPost("store")]
        public async Task<IActionResult> StoreToken([FromBody] StoreTokenRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var refreshToken = await _tokenService.GenerateRefreshTokenAsync(userId);
                return Ok(new { RefreshToken = refreshToken });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating refresh token");
                return StatusCode(500, "Error generating refresh token");
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

                var isValid = await _tokenService.ValidateRefreshTokenAsync(token, userId);
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
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                if (string.IsNullOrEmpty(request.Token))
                {
                    return BadRequest(new { error = "Token is required" });
                }

                await _tokenService.RevokeRefreshTokenAsync(request.Token);
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

                await _tokenService.RevokeAllRefreshTokensAsync(userId);
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

                // Get all refresh tokens for the user
                var refreshTokens = await _context.RefreshTokens
                    .Where(rt => rt.UserId == userId && rt.IsValid)
                    .Select(rt => new { rt.Token, rt.ExpiryDate, rt.CreatedAt })
                    .ToListAsync();

                return Ok(refreshTokens);
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

                var isValid = await _tokenService.ValidateRefreshTokenAsync(token, userId);
                if (!isValid)
                    return NotFound("Token not found or invalid");

                var refreshToken = await _context.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == token && rt.UserId == userId);

                if (refreshToken == null)
                    return NotFound("Token not found");

                return Ok(new
                {
                    Token = refreshToken.Token,
                    ExpiryDate = refreshToken.ExpiryDate,
                    CreatedAt = refreshToken.CreatedAt,
                    IsValid = refreshToken.IsValid
                });
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

                var isValid = await _tokenService.ValidateRefreshTokenAsync(request.Token, userId);
                if (!isValid)
                    return NotFound("Token not found or invalid");

                var refreshToken = await _context.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == request.Token && rt.UserId == userId);

                if (refreshToken == null)
                    return NotFound("Token not found");

                refreshToken.ExpiryDate = refreshToken.ExpiryDate.AddMinutes(request.AdditionalMinutes);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Token = refreshToken.Token,
                    NewExpiryDate = refreshToken.ExpiryDate
                });
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
                var stats = await _context.RefreshTokens
                    .GroupBy(rt => rt.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        TotalTokens = g.Count(),
                        ValidTokens = g.Count(rt => rt.IsValid),
                        ExpiredTokens = g.Count(rt => rt.ExpiryDate < DateTime.UtcNow)
                    })
                    .ToListAsync();

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
                var expiredTokens = await _context.RefreshTokens
                    .Where(rt => rt.ExpiryDate < DateTime.UtcNow || !rt.IsValid)
                    .ToListAsync();

                foreach (var token in expiredTokens)
                {
                    token.IsValid = false;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    CleanedTokens = expiredTokens.Count,
                    Message = "Expired tokens cleaned up successfully"
                });
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