using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using WebApplication1.Models.Users;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using WebApplication1.Repositories;
using WebApplication1.Models.Enums;
using WebApplication1.Models.Auth;
using WebApplication1.Models.Requests;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IStorageService _storageService;

        public AuthController(IAuthService authService, ILogger<AuthController> logger, IUserRepository userRepository, IStorageService storageService)
        {
            _authService = authService;
            _logger = logger;
            _userRepository = userRepository;
            _storageService = storageService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var response = await _authService.LoginAsync(request.Email, request.Password);
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
        public async Task<IActionResult> Register([FromBody] WebApplication1.Services.RegisterRequest request)
        {
            try
            {
                // Validate the request
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest("Email and password are required");
                }

                bool isAdminCreated = request.IsAdminCreated;

                // Register user and get tokens
                (string accessToken, string refreshToken) = await _authService.RegisterAsync(
                    request.Username,
                    request.Email,
                    request.Password,
                    request.DisplayName,
                    isAdminCreated
                );

                // Create response
                var response = new
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    User = new
                    {
                        Username = request.Username,
                        Email = request.Email,
                        DisplayName = request.DisplayName ?? request.Username,
                        Role = UserRole.Member,
                        IsVerified = isAdminCreated,
                        Status = UserStatus.Offline,
                        CreatedAt = DateTime.UtcNow
                    }
                };

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
        public async Task<IActionResult> RefreshToken([FromBody] WebApplication1.Models.Requests.RefreshTokenRequest request)
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

                if (string.IsNullOrEmpty(refreshToken))
                    return BadRequest("Refresh token is required");

                await _authService.LogoutAsync(refreshToken);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout error");
                return StatusCode(500, "An error occurred during logout");
            }
        }

        [HttpPost("reset-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ResetPassword([FromBody] WebApplication1.Models.Requests.ResetPasswordRequest request)
        {
            try
            {
                var result = await _authService.ResetPasswordAsync(request.Email);
                if (!result)
                {
                    return BadRequest(new { message = "Failed to process password reset request" });
                }
                return Ok(new { message = "Password reset instructions have been sent to your email" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset");
                return StatusCode(500, new { message = "An error occurred while processing your request" });
            }
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] WebApplication1.Models.Requests.ChangePasswordRequest request)
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
        public async Task<IActionResult> VerifyEmail([FromBody] WebApplication1.Models.Requests.VerifyEmailRequest request)
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
        [HttpGet("sessions")]
        public async Task<IActionResult> GetActiveSessions()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return BadRequest("Invalid request");

            var currentRefreshToken = Request.Headers["Refresh-Token"].ToString();
            var sessions = await _authService.GetActiveSessionsAsync(userId, currentRefreshToken);
            return Ok(sessions);
        }

        [Authorize]
        [HttpPost("sessions/revoke")]
        public async Task<IActionResult> RevokeSession([FromBody] WebApplication1.Models.Requests.RevokeSessionRequest request)
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
} 