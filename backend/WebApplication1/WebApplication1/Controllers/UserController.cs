using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using WebApplication1.Models.Users;
using System.Security.Claims;
using System.Linq;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly ITokenService _tokenService;
        private readonly ISecurityService _securityService;
        private readonly ILogger<UserController> _logger;

        public UserController(
            IUserService userService,
            IAuthService authService,
            IEmailService emailService,
            ITokenService tokenService,
            ISecurityService securityService,
            ILogger<UserController> logger)
        {
            _userService = userService;
            _authService = authService;
            _emailService = emailService;
            _tokenService = tokenService;
            _securityService = securityService;
            _logger = logger;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var userDto = new UserCreateDto
                {
                    UserName = request.Username,
                    Email = request.Email,
                    Password = request.Password,
                    DisplayName = $"{request.FirstName} {request.LastName}"
                };

                var result = await _userService.CreateUserAsync(userDto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user");
                return StatusCode(500, "Error registering user");
            }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var isValid = await _userService.ValidateUserCredentialsAsync(request.Email, request.Password);
                if (!isValid)
                    return Unauthorized(new { error = "Invalid email or password" });

                var user = await _userService.GetUserByEmailAsync(request.Email);
                if (user == null)
                    return Unauthorized(new { error = "User not found" });

                return Ok(new
                {
                    UserId = user.Id,
                    Username = user.UserName,
                    Email = user.Email,
                    DisplayName = user.DisplayName,
                    Role = user.Role
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging in");
                return StatusCode(500, "Error logging in");
            }
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var profile = await _userService.GetUserByIdAsync(userId);
                return Ok(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile");
                return StatusCode(500, "Error getting user profile");
            }
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var userDto = new UserUpdateDto
                {
                    DisplayName = $"{request.FirstName} {request.LastName}".Trim(),
                    Bio = null,
                    ProfilePictureUrl = null
                };

                var result = await _userService.UpdateUserAsync(userId, userDto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                return StatusCode(500, "Error updating profile");
            }
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _userService.ChangePasswordAsync(
                    userId,
                    request.CurrentPassword,
                    request.NewPassword);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                return StatusCode(500, "Error changing password");
            }
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var result = await _authService.ResetPasswordAsync(request.Email);
                return Ok(new { message = "Password reset instructions have been sent to your email" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                return StatusCode(500, "Error resetting password");
            }
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                var result = await _authService.ResetPasswordAsync(request.Email);
                return Ok(new { message = "Password reset instructions have been sent to your email" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing forgot password request");
                return StatusCode(500, "Error processing forgot password request");
            }
        }

        [HttpPost("verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            try
            {
                var user = await _userService.GetUserByEmailAsync(request.Email);
                if (user == null)
                    return NotFound(new { error = "User not found" });

                var result = await _authService.VerifyEmailAsync(user.Id, request.Token);
                return Ok(new { message = "Email verified successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying email");
                return StatusCode(500, "Error verifying email");
            }
        }

        [HttpPost("resend-verification")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
        {
            try
            {
                var user = await _userService.GetUserByEmailAsync(request.Email);
                if (user == null)
                    return NotFound(new { error = "User not found" });

                if (user.IsVerified)
                    return BadRequest(new { error = "Email is already verified" });

                // Generate a verification token
                var token = await _tokenService.GenerateRefreshTokenAsync(user.Id);
                if (string.IsNullOrEmpty(token))
                    return StatusCode(500, "Error generating verification token");

                var subject = "Verify Your Email";
                var body = $@"
                    <h2>Email Verification</h2>
                    <p>Hello {user.DisplayName},</p>
                    <p>Please click the link below to verify your email address:</p>
                    <p><a href='{Request.Scheme}://{Request.Host}/api/user/verify-email?email={user.Email}&token={token}'>Verify Email</a></p>
                    <p>If you did not request this verification, please ignore this email.</p>
                    <p>Best regards,<br>Your App Team</p>";

                await _emailService.SendEmailAsync(user.Email, subject, body);
                return Ok(new { message = "Verification email has been sent" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending verification email");
                return StatusCode(500, "Error resending verification email");
            }
        }

        [HttpGet("search")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SearchUsers([FromQuery] UserSearchRequest request)
        {
            try
            {
                var users = await _userService.SearchUsersAsync(request.Query ?? string.Empty);
                
                // Apply pagination manually
                var totalCount = users.Count();
                var pageSize = request.PageSize > 0 ? request.PageSize : 10;
                var page = request.Page > 0 ? request.Page : 1;
                var skip = (page - 1) * pageSize;

                var paginatedUsers = users
                    .Skip(skip)
                    .Take(pageSize)
                    .Select(u => new
                    {
                        u.Id,
                        u.UserName,
                        u.Email,
                        u.DisplayName,
                        u.Role,
                        u.IsVerified,
                        u.IsActive,
                        u.CreatedAt
                    });

                return Ok(new
                {
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    Users = paginatedUsers
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users");
                return StatusCode(500, "Error searching users");
            }
        }

        [HttpPut("{userId}/roles")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateUserRoles(string userId, [FromBody] UpdateRolesRequest request)
        {
            try
            {
                foreach (var role in request.Roles)
                {
                    var success = await _securityService.AssignRoleToUserAsync(userId, role);
                    if (!success)
                    {
                        _logger.LogWarning("Failed to assign role {Role} to user {UserId}", role, userId);
                    }
                }

                return Ok(new { message = "User roles updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user roles");
                return StatusCode(500, "Error updating user roles");
            }
        }

        [HttpDelete("{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            try
            {
                var result = await _userService.DeleteUserAsync(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                return StatusCode(500, "Error deleting user");
            }
        }

        [HttpPost("{userId}/block")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BlockUser(string userId)
        {
            try
            {
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminId))
                    return Unauthorized();

                var result = await _userService.BlockUserAsync(adminId, userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking user");
                return StatusCode(500, "Error blocking user");
            }
        }

        [HttpPost("{userId}/unblock")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UnblockUser(string userId)
        {
            try
            {
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminId))
                    return Unauthorized();

                var result = await _userService.UnblockUserAsync(adminId, userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unblocking user");
                return StatusCode(500, "Error unblocking user");
            }
        }

        [HttpGet("activity")]
        public async Task<IActionResult> GetUserActivity()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var activities = await _userService.GetUserActivitiesAsync(userId, 10); // Get last 10 activities
                return Ok(new
                {
                    UserId = userId,
                    Activities = activities.Select(a => new
                    {
                        a.ActivityType,
                        a.Description,
                        a.Timestamp,
                        a.IpAddress,
                        a.UserAgent,
                        a.IsSuccessful,
                        a.ErrorMessage,
                        a.RelatedEntityId,
                        a.RelatedEntityType
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user activity");
                return StatusCode(500, "Error getting user activity");
            }
        }

        [HttpGet("preferences")]
        public async Task<IActionResult> GetUserPreferences()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var preferences = await _userService.GetUserPreferencesAsync(userId);
                return Ok(preferences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user preferences");
                return StatusCode(500, "Error getting user preferences");
            }
        }

        [HttpPut("preferences")]
        public async Task<IActionResult> UpdateUserPreferences([FromBody] UpdatePreferencesRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null)
                    return NotFound(new { error = "User not found" });

                var preferences = new UserPreferences
                {
                    UserId = userId,
                    User = user,
                    DisplayName = user.DisplayName ?? user.UserName,
                    ProfilePicture = user.ProfilePictureUrl,
                    Bio = user.Bio,
                    PhoneNumber = user.PhoneNumber,
                    IsPhoneNumberPublic = false,
                    IsEmailPublic = false,
                    IsOnlineStatusPublic = true,
                    IsLastSeenPublic = true,
                    IsReadReceiptsPublic = true,
                    IsProfilePicturePublic = true,
                    IsBioPublic = true,
                    IsFriendListPublic = true,
                    IsMessageHistoryPublic = true
                };

                var result = await _userService.UpdateUserPreferencesAsync(userId, preferences);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user preferences");
                return StatusCode(500, "Error updating user preferences");
            }
        }
    }

    public class RegisterRequest
    {
        public required string Username { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
    }

    public class LoginRequest
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }

    public class UpdateProfileRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
    }

    public class ChangePasswordRequest
    {
        public required string CurrentPassword { get; set; }
        public required string NewPassword { get; set; }
    }

    public class ResetPasswordRequest
    {
        public required string Email { get; set; }
    }

    public class ForgotPasswordRequest
    {
        public required string Email { get; set; }
    }

    public class VerifyEmailRequest
    {
        public required string Email { get; set; }
        public required string Token { get; set; }
    }

    public class ResendVerificationRequest
    {
        public required string Email { get; set; }
    }

    public class UserSearchRequest
    {
        public string? Query { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class UpdateRolesRequest
    {
        public required List<string> Roles { get; set; }
    }

    public class UpdatePreferencesRequest
    {
        public required Dictionary<string, object> Preferences { get; set; }
    }
} 