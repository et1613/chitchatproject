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
        TwoFactorRequired,
        TwoFactorInvalid,
        SessionExpired,
        RefreshTokenInvalid
    }

    public interface IAuthService
    {
        Task<(string accessToken, string refreshToken, User user)> LoginAsync(string email, string password, string? twoFactorCode = null);
        Task<(string accessToken, string refreshToken, User user)> RegisterAsync(string username, string email, string password);
        Task<(string accessToken, string refreshToken)> RefreshTokenAsync(string refreshToken);
        Task<bool> ValidateTokenAsync(string token);
        Task LogoutAsync(string userId, string refreshToken);
        Task<bool> ResetPasswordAsync(string email);
        Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
        Task<bool> VerifyEmailAsync(string userId, string token);
        Task<bool> EnableTwoFactorAsync(string userId);
        Task<bool> DisableTwoFactorAsync(string userId, string code);
        Task<bool> VerifyTwoFactorAsync(string userId, string code);
        Task<IEnumerable<ActiveSession>> GetActiveSessionsAsync(string userId);
        Task RevokeSessionAsync(string userId, string sessionId);
        Task<bool> ValidateUserAsync(string email, string password);
    }

    public class ActiveSession
    {
        public string SessionId { get; set; }
        public string DeviceInfo { get; set; }
        public string IpAddress { get; set; }
        public DateTime LastActivity { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;
        private readonly HashingService _hashingService;
        private readonly ConnectionManager _connectionManager;
        private readonly Dictionary<string, ActiveSession> _activeSessions = new();

        public AuthService(
            IUserRepository userRepository,
            IConfiguration configuration,
            HashingService hashingService,
            ConnectionManager connectionManager)
        {
            _userRepository = userRepository;
            _configuration = configuration;
            _hashingService = hashingService;
            _connectionManager = connectionManager;
        }

        public async Task<(string accessToken, string refreshToken, User user)> LoginAsync(
            string email, string password, string? twoFactorCode = null)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
                throw new AuthException("Kullanıcı bulunamadı", AuthErrorType.InvalidCredentials);

            if (!_hashingService.VerifyHash(password, user.PasswordHash))
            {
                user.RecordFailedLogin();
                throw new AuthException("Geçersiz şifre", AuthErrorType.InvalidCredentials);
            }

            if (user.IsLockedOut)
                throw new AuthException("Hesabınız kilitlendi. Lütfen daha sonra tekrar deneyin.", AuthErrorType.AccountLocked);

            if (!user.IsVerified)
                throw new AuthException("Email adresiniz doğrulanmamış", AuthErrorType.EmailNotVerified);

            if (user.TwoFactorEnabled)
            {
                if (string.IsNullOrEmpty(twoFactorCode))
                    throw new AuthException("İki faktörlü doğrulama kodu gerekli", AuthErrorType.TwoFactorRequired);

                if (!VerifyTwoFactorCode(user, twoFactorCode))
                    throw new AuthException("Geçersiz iki faktörlü doğrulama kodu", AuthErrorType.TwoFactorInvalid);
            }

            user.ResetFailedLoginAttempts();
            user.LastLoginAt = DateTime.UtcNow;
            user.UpdateLastSeen();

            var (accessToken, refreshToken) = GenerateTokens(user);
            await CreateSessionAsync(user.Id, refreshToken);

            return (accessToken, refreshToken, user);
        }

        public async Task<(string accessToken, string refreshToken, User user)> RegisterAsync(
            string username, string email, string password)
        {
            if (await _userRepository.GetByUsernameAsync(username) != null)
                throw new AuthException("Bu kullanıcı adı zaten kullanılıyor", AuthErrorType.InvalidCredentials);

            if (await _userRepository.GetByEmailAsync(email) != null)
                throw new AuthException("Bu email adresi zaten kullanılıyor", AuthErrorType.InvalidCredentials);

            var hashedPassword = _hashingService.GenerateHash(password);
            var user = new User
            {
                UserName = username,
                Email = email,
                PasswordHash = hashedPassword,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _userRepository.AddAsync(user);

            var (accessToken, refreshToken) = GenerateTokens(user);
            await CreateSessionAsync(user.Id, refreshToken);

            return (accessToken, refreshToken, user);
        }

        public async Task<(string accessToken, string refreshToken)> RefreshTokenAsync(string refreshToken)
        {
            var session = _activeSessions.Values.FirstOrDefault(s => s.SessionId == refreshToken);
            if (session == null)
                throw new AuthException("Geçersiz refresh token", AuthErrorType.RefreshTokenInvalid);

            var user = await _userRepository.GetByIdAsync(session.SessionId);
            if (user == null)
                throw new AuthException("Kullanıcı bulunamadı", AuthErrorType.InvalidToken);

            var (accessToken, newRefreshToken) = GenerateTokens(user);
            await UpdateSessionAsync(session.SessionId, newRefreshToken);

            return (accessToken, newRefreshToken);
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);

            try
            {
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
            catch
            {
                return false;
            }
        }

        public async Task LogoutAsync(string userId, string refreshToken)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
            {
                user.SetStatus(UserStatus.Offline);
                _connectionManager.RemoveClient(userId);
                await RevokeSessionAsync(userId, refreshToken);
            }
        }

        public async Task<bool> EnableTwoFactorAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new AuthException("Kullanıcı bulunamadı", AuthErrorType.InvalidCredentials);

            // TODO: Implement 2FA setup logic
            user.TwoFactorEnabled = true;
            await _userRepository.UpdateAsync(user);

            return true;
        }

        public async Task<bool> DisableTwoFactorAsync(string userId, string code)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new AuthException("Kullanıcı bulunamadı", AuthErrorType.InvalidCredentials);

            if (!VerifyTwoFactorCode(user, code))
                throw new AuthException("Geçersiz iki faktörlü doğrulama kodu", AuthErrorType.TwoFactorInvalid);

            user.TwoFactorEnabled = false;
            await _userRepository.UpdateAsync(user);

            return true;
        }

        public async Task<bool> VerifyTwoFactorAsync(string userId, string code)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new AuthException("Kullanıcı bulunamadı", AuthErrorType.InvalidCredentials);

            return VerifyTwoFactorCode(user, code);
        }

        public async Task<IEnumerable<ActiveSession>> GetActiveSessionsAsync(string userId)
        {
            return _activeSessions.Values.Where(s => s.SessionId.StartsWith(userId));
        }

        public async Task RevokeSessionAsync(string userId, string sessionId)
        {
            if (_activeSessions.ContainsKey(sessionId))
            {
                _activeSessions.Remove(sessionId);
            }
        }

        private (string accessToken, string refreshToken) GenerateTokens(User user)
        {
            var accessToken = GenerateJwtToken(user, TimeSpan.FromMinutes(15));
            var refreshToken = GenerateJwtToken(user, TimeSpan.FromDays(7));
            return (accessToken, refreshToken);
        }

        private string GenerateJwtToken(User user, TimeSpan expiration)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role.ToString())
                }),
                Expires = DateTime.UtcNow.Add(expiration),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private async Task CreateSessionAsync(string userId, string refreshToken)
        {
            var session = new ActiveSession
            {
                SessionId = refreshToken,
                DeviceInfo = "Web Client", // TODO: Get from request
                IpAddress = "127.0.0.1", // TODO: Get from request
                LastActivity = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _activeSessions[refreshToken] = session;
        }

        private async Task UpdateSessionAsync(string oldToken, string newToken)
        {
            if (_activeSessions.TryGetValue(oldToken, out var session))
            {
                _activeSessions.Remove(oldToken);
                session.SessionId = newToken;
                session.LastActivity = DateTime.UtcNow;
                _activeSessions[newToken] = session;
            }
        }

        private bool VerifyTwoFactorCode(User user, string code)
        {
            // TODO: Implement actual 2FA verification logic
            return true;
        }

        public async Task<bool> ResetPasswordAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
                return false;

            // Generate reset token
            var resetToken = Guid.NewGuid().ToString();
            // TODO: Save reset token to database with expiration

            // Send reset email
            // TODO: Implement email sending service

            return true;
        }

        public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new InvalidOperationException("Kullanıcı bulunamadı");

            if (!_hashingService.VerifyHash(currentPassword, user.PasswordHash))
                throw new InvalidOperationException("Mevcut şifre yanlış");

            user.PasswordHash = _hashingService.GenerateHash(newPassword);
            await _userRepository.UpdateAsync(user);

            return true;
        }

        public async Task<bool> VerifyEmailAsync(string userId, string token)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return false;

            // TODO: Implement email verification logic
            user.IsVerified = true;
            await _userRepository.UpdateAsync(user);

            return true;
        }

        public async Task<bool> ValidateUserAsync(string email, string password)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null) return false;

            if (!user.IsActive || user.IsLockedOut)
                return false;

            var isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            if (isValid)
            {
                user.ResetFailedLoginAttempts();
                user.LastLoginAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);
            }
            else
            {
                user.RecordFailedLogin();
                await _userRepository.UpdateAsync(user);
            }

            return isValid;
        }
    }
} 