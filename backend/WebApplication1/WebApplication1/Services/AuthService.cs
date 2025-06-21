using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WebApplication1.Models.Users;
using WebApplication1.Repositories;
using WebApplication1.Services;
using WebApplication1.Models.Enums;
using BCrypt.Net;
using System.ComponentModel.DataAnnotations;
using WebApplication1.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebApplication1.Models.Auth;

namespace WebApplication1.Services
{
    public class AuthException : Exception
    {
        public AuthErrorType ErrorType { get; }

        public AuthException(string message, AuthErrorType errorType) : base(message)
        {
            ErrorType = errorType;
        }
    }

    public enum AuthErrorType
    {
        InvalidCredentials,
        AccountLocked,
        EmailNotVerified,
        InvalidToken,
        SessionExpired,
        RefreshTokenInvalid,
        PasswordExpired,
        AccountDisabled,
        TooManyAttempts
    }

    public class LoginRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        public required string Password { get; set; }

        public string? DeviceInfo { get; set; }
        public string? IpAddress { get; set; }
    }

    public class RegisterRequest
    {
        [Required(ErrorMessage = "Username is required")]
        [MinLength(3, ErrorMessage = "Username must be at least 3 characters")]
        [MaxLength(50, ErrorMessage = "Username cannot exceed 50 characters")]
        public required string Username { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
            ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number and one special character")]
        public required string Password { get; set; }

        [Required(ErrorMessage = "Display name is required")]
        [MinLength(2, ErrorMessage = "Display name must be at least 2 characters")]
        [MaxLength(50, ErrorMessage = "Display name cannot exceed 50 characters")]
        public required string DisplayName { get; set; }

        public string? DeviceInfo { get; set; }
        public string? IpAddress { get; set; }
    }

    public class AuthResponse
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public required DateTime ExpiresAt { get; set; }
        public required UserDto User { get; set; }
    }

    public class UserDto
    {
        public required string Id { get; set; }
        public required string Username { get; set; }
        public required string Email { get; set; }
        public required string DisplayName { get; set; }
        public required UserRole Role { get; set; }
        public required bool IsVerified { get; set; }
        public required UserStatus Status { get; set; }
        public required DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public interface IAuthService
    {
        Task<AuthResponse> LoginAsync(string email, string password);
        Task<(string accessToken, string refreshToken)> RegisterAsync(string username, string email, string password, string? displayName = null);
        Task<(string accessToken, string refreshToken)> RefreshTokenAsync(string refreshToken);
        Task LogoutAsync(string refreshToken);
        Task<bool> ValidateTokenAsync(string token);
        Task<bool> IsTokenBlacklistedAsync(string token);
        Task<int> GetFailedLoginAttemptsAsync(string userId);
        Task ResetFailedLoginAttemptsAsync(string userId);
        Task<bool> IsAccountLockedAsync(string userId);
        Task LockAccountAsync(string userId);
        Task UnlockAccountAsync(string userId);
        Task<bool> ResetPasswordAsync(string email);
        Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
        Task<bool> VerifyEmailAsync(string userId, string token);
        Task<IEnumerable<ActiveSession>> GetActiveSessionsAsync(string userId, string? currentRefreshToken);
        Task RevokeSessionAsync(string userId, string sessionId);
    }

    public class ActiveSession
    {
        public required string SessionId { get; set; }
        public required string DeviceInfo { get; set; }
        public required string IpAddress { get; set; }
        public required DateTime LastActivity { get; set; }
        public required DateTime CreatedAt { get; set; }
        public required bool IsCurrentSession { get; set; }
    }

    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ITokenService _tokenService;
        private readonly HashingService _hashingService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<AuthService> _logger;
        private readonly IEmailService _emailService;

        private const int MaxFailedLoginAttempts = 5;
        private const int LockoutDurationMinutes = 30;
        private const int AccessTokenExpiryMinutes = 15;
        private const int RefreshTokenExpiryDays = 7;

        public AuthService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ITokenService tokenService,
            HashingService hashingService,
            IUserRepository userRepository,
            ILogger<AuthService> logger,
            IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _tokenService = tokenService;
            _hashingService = hashingService;
            _userRepository = userRepository;
            _logger = logger;
            _emailService = emailService;
        }

        public async Task<AuthResponse> LoginAsync(string email, string password)
        {
            try
            {
                var user = await _userRepository.GetByEmailAsync(email);
                if (user == null)
                {
                    _logger.LogWarning($"Login attempt failed: User not found with email {email}");
                    throw new Exception("Invalid email or password");
                }

                // Check if account is locked
                if (await IsAccountLockedAsync(user.Id))
                {
                    _logger.LogWarning($"Login attempt failed: Account locked for user {user.Id}");
                    throw new Exception("Account is locked. Please try again later.");
                }

                // Verify password
                if (user.PasswordHash == null)
                {
                    _logger.LogError($"User {user.Id} has no password hash set");
                    throw new AuthException("Account security error", AuthErrorType.InvalidCredentials);
                }

                if (!_hashingService.VerifyHash(password, user.PasswordHash))
                {
                    await IncrementFailedLoginAttemptsAsync(user.Id);
                    _logger.LogWarning($"Login attempt failed: Invalid password for user {user.Id}");
                    throw new AuthException("Invalid email or password", AuthErrorType.InvalidCredentials);
                }

                // Check if account is active
                if (!user.IsActive)
                {
                    _logger.LogWarning($"Login attempt failed: Account deactivated for user {user.Id}");
                    throw new Exception("Account is deactivated");
                }

                // Reset failed login attempts on successful login
                await ResetFailedLoginAttemptsAsync(user.Id);

                // Update user status
                user.Status = UserStatus.Online;
                user.LastLoginAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);

                // Generate tokens
                var accessToken = GenerateJwtToken(user);
                var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id);

                _logger.LogInformation($"User {user.Id} logged in successfully");

                return new AuthResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(AccessTokenExpiryMinutes),
                    User = new UserDto
                    {
                        Id = user.Id,
                        Username = user.UserName,
                        Email = user.Email,
                        DisplayName = user.DisplayName,
                        Role = user.Role,
                        IsVerified = user.IsVerified,
                        Status = user.Status,
                        CreatedAt = user.CreatedAt,
                        LastLoginAt = user.LastLoginAt
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during login for email {email}");
                throw;
            }
        }

        public async Task<(string accessToken, string refreshToken)> RegisterAsync(string username, string email, string password, string? displayName = null)
        {
            try
            {
                // Check if email is already in use
                var existingUser = await _userRepository.GetByEmailAsync(email);
                if (existingUser != null)
                {
                    throw new AuthException("Email is already in use", AuthErrorType.InvalidCredentials);
                }

                // Check if username is already in use
                var existingUsername = await _userRepository.GetByUsernameAsync(username);
                if (existingUsername != null)
                {
                    throw new AuthException("Username is already in use", AuthErrorType.InvalidCredentials);
                }

                // Create user
                var newUser = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = username,
                    Email = email,
                    PasswordHash = _hashingService.HashPassword(password),
                    DisplayName = displayName ?? username,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Role = UserRole.Member
                };

                // Save user
                await _userRepository.AddAsync(newUser);

                // Generate tokens
                var accessToken = GenerateJwtToken(newUser);
                var refreshToken = await _tokenService.GenerateRefreshTokenAsync(newUser.Id);

                _logger.LogInformation($"User {newUser.Id} registered successfully");

                return (accessToken, refreshToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during registration for email {email}");
                throw;
            }
        }

        public async Task<(string accessToken, string refreshToken)> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                // Validate refresh token
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == refreshToken));

                if (user == null)
                {
                    _logger.LogWarning($"Token refresh failed: Invalid refresh token");
                    throw new Exception("Invalid refresh token");
                }

                if (!await _tokenService.ValidateRefreshTokenAsync(refreshToken, user.Id))
                {
                    _logger.LogWarning($"Token refresh failed: Token invalid or expired for user {user.Id}");
                    throw new Exception("Refresh token is invalid or expired");
                }

                // Check if user is still active
                if (!user.IsActive)
                {
                    _logger.LogWarning($"Token refresh failed: Account deactivated for user {user.Id}");
                    throw new Exception("Account is deactivated");
                }

                // Revoke the used refresh token
                await _tokenService.RevokeRefreshTokenAsync(refreshToken);

                // Generate new tokens
                var newAccessToken = GenerateJwtToken(user);
                var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id);

                _logger.LogInformation($"Tokens refreshed successfully for user {user.Id}");

                return (newAccessToken, newRefreshToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                throw;
            }
        }

        public async Task LogoutAsync(string refreshToken)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == refreshToken));

                if (user != null)
                {
                    // Update user status
                    user.Status = UserStatus.Offline;
                    await _userRepository.UpdateAsync(user);

                    // Revoke refresh token
                    await _tokenService.RevokeRefreshTokenAsync(refreshToken);

                    _logger.LogInformation($"User {user.Id} logged out successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                throw;
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                if (await IsTokenBlacklistedAsync(token))
                    return false;

                var jwtKey = _configuration["Jwt:Key"];
                if (string.IsNullOrEmpty(jwtKey))
                {
                    _logger.LogError("JWT key is not configured");
                    throw new InvalidOperationException("JWT key is not configured in application settings");
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(jwtKey);
                
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidAudience = _configuration["Jwt:Audience"],
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return false;
            }
        }

        public async Task<bool> IsTokenBlacklistedAsync(string token)
        {
            return await _context.TokenBlacklist
                .AnyAsync(t => t.Token == token && t.ExpiresAt > DateTime.UtcNow);
        }

        public async Task<int> GetFailedLoginAttemptsAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user?.FailedLoginAttempts ?? 0;
        }

        public async Task ResetFailedLoginAttemptsAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
            {
                user.FailedLoginAttempts = 0;
                user.LockoutEnd = null;
                await _userRepository.UpdateAsync(user);
            }
        }

        public async Task<bool> IsAccountLockedAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user?.LockoutEnd > DateTime.UtcNow;
        }

        public async Task LockAccountAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(LockoutDurationMinutes);
                await _userRepository.UpdateAsync(user);

                // Send notification email
                await _emailService.SendAccountLockedNotificationAsync(user.Email);
            }
        }

        public async Task UnlockAccountAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
            {
                user.LockoutEnd = null;
                user.FailedLoginAttempts = 0;
                await _userRepository.UpdateAsync(user);

                // Send notification email
                await _emailService.SendAccountUnlockedNotificationAsync(user.Email);
            }
        }

        private async Task IncrementFailedLoginAttemptsAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
            {
                user.FailedLoginAttempts++;

                if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
                {
                    await LockAccountAsync(userId);
                }
                else
                {
                    await _userRepository.UpdateAsync(user);
                }
            }
        }

        private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                _logger.LogError("JWT key is not configured");
                throw new InvalidOperationException("JWT key is not configured in application settings");
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(jwtKey);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.Role, user.Role.ToString()),
                    new Claim("DisplayName", user.DisplayName ?? user.UserName),
                    new Claim("IsVerified", user.IsVerified.ToString())
                }),
                Expires = DateTime.UtcNow.AddMinutes(AccessTokenExpiryMinutes),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public async Task<bool> ResetPasswordAsync(string email)
        {
            try
            {
                var user = await _userRepository.GetByEmailAsync(email);
                if (user == null)
                {
                    _logger.LogWarning($"Password reset requested for non-existent email: {email}");
                    return false;
                }

                // Generate a password reset token
                var resetToken = await _tokenService.GenerateRefreshTokenAsync(user.Id);
                
                // Create reset link
                var resetLink = $"{_configuration["FrontendUrl"]}/reset-password?token={resetToken}&email={Uri.EscapeDataString(email)}";
                
                // Send reset email
                await _emailService.SendPasswordResetEmailAsync(email, resetLink);
                
                _logger.LogInformation($"Password reset email sent to {email}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during password reset for email {email}");
                throw;
            }
        }

        public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning($"Password change failed: User not found with ID {userId}");
                    return false;
                }

                // Verify current password
                if (user.PasswordHash == null)
                {
                    _logger.LogError($"User {user.Id} has no password hash set");
                    throw new AuthException("Account security error", AuthErrorType.InvalidCredentials);
                }

                if (!_hashingService.VerifyHash(currentPassword, user.PasswordHash))
                {
                    await IncrementFailedLoginAttemptsAsync(user.Id);
                    _logger.LogWarning($"Login attempt failed: Invalid current password for user {userId}");
                    throw new AuthException("Invalid email or password", AuthErrorType.InvalidCredentials);
                }

                // Hash and update new password
                user.PasswordHash = _hashingService.HashPassword(newPassword);
                await _userRepository.UpdateAsync(user);

                // Send notification email
                await _emailService.SendPasswordChangeNotificationAsync(user.Email, user.UserName);

                _logger.LogInformation($"Password changed successfully for user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error changing password for user {userId}");
                throw;
            }
        }

        public async Task<bool> VerifyEmailAsync(string userId, string token)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning($"Email verification failed: User not found with ID {userId}");
                    return false;
                }

                // Validate token
                if (!await _tokenService.ValidateRefreshTokenAsync(token, userId))
                {
                    _logger.LogWarning($"Email verification failed: Invalid token for user {userId}");
                    return false;
                }

                // Update user verification status
                user.IsVerified = true;
                await _userRepository.UpdateAsync(user);

                // Revoke the verification token
                await _tokenService.RevokeRefreshTokenAsync(token);

                _logger.LogInformation($"Email verified successfully for user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying email for user {userId}");
                throw;
            }
        }

        public async Task<IEnumerable<ActiveSession>> GetActiveSessionsAsync(string userId, string? currentRefreshToken)
        {
            try
            {
                var sessions = await _context.RefreshTokens
                    .Where(rt => rt.UserId == userId && rt.IsValid)
                    .Select(rt => new ActiveSession
                    {
                        SessionId = rt.Id,
                        DeviceInfo = "Unknown",
                        IpAddress = "Unknown",
                        LastActivity = rt.CreatedAt,
                        CreatedAt = rt.CreatedAt,
                        IsCurrentSession = currentRefreshToken != null && rt.Token == currentRefreshToken
                    })
                    .ToListAsync();

                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting active sessions for user {userId}");
                throw;
            }
        }

        public async Task RevokeSessionAsync(string userId, string sessionId)
        {
            try
            {
                var session = await _context.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Id == sessionId && rt.UserId == userId);

                if (session != null)
                {
                    session.IsValid = false;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Session {sessionId} revoked for user {userId}");
                }
                else
                {
                    _logger.LogWarning($"Session {sessionId} not found for user {userId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error revoking session {sessionId} for user {userId}");
                throw;
            }
        }
    }
} 