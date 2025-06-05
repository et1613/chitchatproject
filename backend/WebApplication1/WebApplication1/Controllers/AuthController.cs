using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using WebApplication1.Models.Users;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                request.DeviceInfo = HttpContext.Request.Headers["User-Agent"].ToString();

                var response = await _authService.LoginAsync(request);
                return Ok(response);
            }
            catch (AuthException ex)
            {
                _logger.LogWarning(ex, "Login failed: {Message}", ex.Message);
                return StatusCode(400, new { error = ex.Message, errorType = ex.ErrorType });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error");
                return StatusCode(500, "An error occurred during login");
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var response = await _authService.RegisterAsync(request);
                return Ok(response);
            }
            catch (AuthException ex)
            {
                _logger.LogWarning(ex, "Registration failed: {Message}", ex.Message);
                return StatusCode(400, new { error = ex.Message, errorType = ex.ErrorType });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration error");
                return StatusCode(500, "An error occurred during registration");
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var response = await _authService.RefreshTokenAsync(request.RefreshToken);
                return Ok(response);
            }
            catch (AuthException ex)
            {
                _logger.LogWarning(ex, "Token refresh failed: {Message}", ex.Message);
                return StatusCode(400, new { error = ex.Message, errorType = ex.ErrorType });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh error");
                return StatusCode(500, "An error occurred during token refresh");
            }
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var refreshToken = Request.Headers["Refresh-Token"].ToString();

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(refreshToken))
                    return BadRequest("Invalid request");

                await _authService.LogoutAsync(userId, refreshToken);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout error");
                return StatusCode(500, "An error occurred during logout");
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var result = await _authService.ResetPasswordAsync(request.Email);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password reset error");
                return StatusCode(500, "An error occurred during password reset");
            }
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var result = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
                return Ok(new { success = result });
            }
            catch (AuthException ex)
            {
                _logger.LogWarning(ex, "Password change failed: {Message}", ex.Message);
                return StatusCode(400, new { error = ex.Message, errorType = ex.ErrorType });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password change error");
                return StatusCode(500, "An error occurred during password change");
            }
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            try
            {
                var result = await _authService.VerifyEmailAsync(request.UserId, request.Token);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email verification error");
                return StatusCode(500, "An error occurred during email verification");
            }
        }

        [Authorize]
        [HttpPost("enable-2fa")]
        public async Task<IActionResult> EnableTwoFactor()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var result = await _authService.EnableTwoFactorAsync(userId);
                return Ok(new { success = result });
            }
            catch (AuthException ex)
            {
                _logger.LogWarning(ex, "2FA enable failed: {Message}", ex.Message);
                return StatusCode(400, new { error = ex.Message, errorType = ex.ErrorType });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "2FA enable error");
                return StatusCode(500, "An error occurred while enabling 2FA");
            }
        }

        [Authorize]
        [HttpPost("disable-2fa")]
        public async Task<IActionResult> DisableTwoFactor([FromBody] TwoFactorRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var result = await _authService.DisableTwoFactorAsync(userId, request.Code);
                return Ok(new { success = result });
            }
            catch (AuthException ex)
            {
                _logger.LogWarning(ex, "2FA disable failed: {Message}", ex.Message);
                return StatusCode(400, new { error = ex.Message, errorType = ex.ErrorType });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "2FA disable error");
                return StatusCode(500, "An error occurred while disabling 2FA");
            }
        }

        [Authorize]
        [HttpGet("sessions")]
        public async Task<IActionResult> GetActiveSessions()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                var sessions = await _authService.GetActiveSessionsAsync(userId);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Get sessions error");
                return StatusCode(500, "An error occurred while retrieving sessions");
            }
        }

        [Authorize]
        [HttpPost("sessions/revoke")]
        public async Task<IActionResult> RevokeSession([FromBody] RevokeSessionRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid request");

                await _authService.RevokeSessionAsync(userId, request.SessionId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session revocation error");
                return StatusCode(500, "An error occurred while revoking session");
            }
        }
    }

    public class RefreshTokenRequest
    {
        [Required]
        public required string RefreshToken { get; set; }
    }

    public class ResetPasswordRequest
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
    }

    public class ChangePasswordRequest
    {
        [Required]
        public required string CurrentPassword { get; set; }

        [Required]
        [MinLength(8)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
            ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number and one special character")]
        public required string NewPassword { get; set; }
    }

    public class VerifyEmailRequest
    {
        [Required]
        public required string UserId { get; set; }

        [Required]
        public required string Token { get; set; }
    }

    public class TwoFactorRequest
    {
        [Required]
        [StringLength(6, MinimumLength = 6)]
        public required string Code { get; set; }
    }

    public class RevokeSessionRequest
    {
        [Required]
        public required string SessionId { get; set; }
    }
} 