using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;

namespace WebApplication1.Services
{
    public interface ISecurityService
    {
        // Kimlik Doğrulama ve Yetkilendirme
        Task<bool> ValidateUserCredentialsAsync(string username, string password);
        Task<string> GenerateJwtTokenAsync(string userId, string username, IEnumerable<string> roles);
        Task<bool> ValidateTokenAsync(string token);
        Task<bool> RefreshTokenAsync(string refreshToken);
        Task<bool> RevokeTokenAsync(string token);
        Task<IEnumerable<string>> GetUserRolesAsync(string userId);
        Task<bool> AssignRoleToUserAsync(string userId, string role);
        Task<bool> RemoveRoleFromUserAsync(string userId, string role);

        // Güvenlik İzleme ve Loglama
        Task LogSecurityEventAsync(string userId, string eventType, string description);
        Task<IEnumerable<SecurityEvent>> GetSecurityEventsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
        Task<bool> IsSuspiciousActivityAsync(string userId, string ipAddress);
        Task<IEnumerable<SessionInfo>> GetActiveSessionsAsync(string userId);
        Task<bool> TerminateSessionAsync(string sessionId);

        // Şifreleme ve Veri Güvenliği
        Task<string> EncryptDataAsync(string data);
        Task<string> DecryptDataAsync(string encryptedData);
        Task<string> HashPasswordAsync(string password);
        Task<bool> ValidatePasswordAsync(string password, string hashedPassword);
        Task<string> GenerateSecureKeyAsync();
        Task<string> MaskSensitiveDataAsync(string data, string dataType);

        // Güvenlik Duvarı ve Korumalar
        Task<bool> IsRateLimitExceededAsync(string userId, string action);
        Task<bool> ValidateInputAsync(string input, string inputType);
        Task<bool> IsRequestValidAsync(HttpContext context);
        Task<bool> ValidateFileUploadAsync(IFormFile file);

        // Güvenlik Raporlama ve Analiz
        Task<SecurityReport> GenerateSecurityReportAsync(string userId, DateTime startDate, DateTime endDate);
        Task<SecurityMetrics> GetSecurityMetricsAsync();
        Task<IEnumerable<SecurityVulnerability>> ScanForVulnerabilitiesAsync();
        Task<ComplianceReport> GenerateComplianceReportAsync();
    }

    public class SecurityOptions
    {
        public required string JwtSecret { get; set; }
        public required string JwtIssuer { get; set; }
        public required string JwtAudience { get; set; }
        public int JwtExpirationMinutes { get; set; } = 60;
        public int RefreshTokenExpirationDays { get; set; } = 7;
        public int MaxFailedLoginAttempts { get; set; } = 5;
        public int AccountLockoutMinutes { get; set; } = 30;
        public bool RequireEmailVerification { get; set; } = true;
        public bool RequireTwoFactor { get; set; } = false;
        public int PasswordMinLength { get; set; } = 8;
        public bool RequireSpecialCharacters { get; set; } = true;
        public bool RequireNumbers { get; set; } = true;
        public bool RequireUppercase { get; set; } = true;
        public bool RequireLowercase { get; set; } = true;
    }

    public class SecurityService : ISecurityService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SecurityService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly SecurityOptions _options;
        private readonly HashingService _hashingService;
        private readonly TokenStorageService _tokenStorageService;
        private readonly IEmailService _emailService;

        public SecurityService(
            ApplicationDbContext context,
            ILogger<SecurityService> logger,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            IOptions<SecurityOptions> options,
            HashingService hashingService,
            TokenStorageService tokenStorageService,
            IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _options = options.Value;
            _hashingService = hashingService;
            _tokenStorageService = tokenStorageService;
            _emailService = emailService;
        }

        // Kimlik Doğrulama ve Yetkilendirme
        public async Task<bool> ValidateUserCredentialsAsync(string username, string password)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == username);

                if (user == null || string.IsNullOrEmpty(user.PasswordHash))
                    return false;

                return await ValidatePasswordAsync(password, user.PasswordHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user credentials for {Username}", username);
                throw;
            }
        }

        public async Task<string> GenerateJwtTokenAsync(string userId, string username, IEnumerable<string> roles)
        {
            try
            {
                if (string.IsNullOrEmpty(_options.JwtSecret))
                {
                    throw new InvalidOperationException("JWT secret key is not configured");
                }

                return await Task.Run(() =>
                {
                    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSecret));
                    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId),
                        new Claim(ClaimTypes.Name, username)
                    };

                    claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

                    var token = new JwtSecurityToken(
                        issuer: _options.JwtIssuer,
                        audience: _options.JwtAudience,
                        claims: claims,
                        expires: DateTime.UtcNow.AddMinutes(_options.JwtExpirationMinutes),
                        signingCredentials: credentials
                    );

                    return new JwtSecurityTokenHandler().WriteToken(token);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating JWT token for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(_options.JwtSecret))
                {
                    throw new InvalidOperationException("JWT secret key is not configured");
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_options.JwtSecret);

                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _options.JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = _options.JwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var result = await Task.Run(() =>
                {
                    try
                    {
                        tokenHandler.ValidateToken(token, tokenValidationParameters, out _);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                throw;
            }
        }

        public async Task<bool> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                var token = await _context.RefreshTokens
                    .FirstOrDefaultAsync(t => t.Token == refreshToken && t.IsValid);

                if (token == null || token.ExpiryDate < DateTime.UtcNow)
                    return false;

                var user = await _context.Users.FindAsync(token.UserId);
                if (user == null || string.IsNullOrEmpty(user.UserName))
                {
                    _logger.LogWarning("User not found or has no username for refresh token {Token}", refreshToken);
                    return false;
                }

                var roles = await GetUserRolesAsync(token.UserId);
                var newToken = await GenerateJwtTokenAsync(user.Id, user.UserName, roles);

                // Eski token'ı geçersiz kıl
                token.IsValid = false;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                throw;
            }
        }

        public async Task<bool> RevokeTokenAsync(string token)
        {
            try
            {
                var refreshToken = await _context.RefreshTokens
                    .FirstOrDefaultAsync(t => t.Token == token);

                if (refreshToken == null)
                    return false;

                refreshToken.IsValid = false;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                throw;
            }
        }

        public async Task<IEnumerable<string>> GetUserRolesAsync(string userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return Enumerable.Empty<string>();

                var userRoles = await _context.UserRoles
                    .Where(ur => ur.UserId == userId)
                    .Select(ur => ur.RoleId)
                    .ToListAsync();

                var roles = await _context.Roles
                    .Where(r => userRoles.Contains(r.Id))
                    .Select(r => r.Name)
                    .Where(name => name != null)
                    .Select(name => name!)
                    .ToListAsync();

                return roles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting roles for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> AssignRoleToUserAsync(string userId, string role)
        {
            try
            {
                var roleEntity = await _context.Roles
                    .FirstOrDefaultAsync(r => r.Name == role);

                if (roleEntity == null)
                    return false;

                var userRole = new Microsoft.AspNetCore.Identity.IdentityUserRole<string>
                {
                    UserId = userId,
                    RoleId = roleEntity.Id
                };

                _context.UserRoles.Add(userRole);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning role {Role} to user {UserId}", role, userId);
                throw;
            }
        }

        public async Task<bool> RemoveRoleFromUserAsync(string userId, string role)
        {
            try
            {
                var roleEntity = await _context.Roles
                    .FirstOrDefaultAsync(r => r.Name == role);

                if (roleEntity == null)
                    return false;

                var userRole = await _context.UserRoles
                    .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleEntity.Id);

                if (userRole == null)
                    return false;

                _context.UserRoles.Remove(userRole);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing role {Role} from user {UserId}", role, userId);
                throw;
            }
        }

        // Güvenlik İzleme ve Loglama
        public async Task LogSecurityEventAsync(string userId, string eventType, string description)
        {
            try
            {
                var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";

                var securityEvent = new SecurityEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    EventType = eventType,
                    Description = description,
                    IpAddress = ipAddress,
                    Timestamp = DateTime.UtcNow
                };

                _context.SecurityEvents.Add(securityEvent);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging security event for user {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<SecurityEvent>> GetSecurityEventsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _context.SecurityEvents
                    .Where(e => e.UserId == userId);

                if (startDate.HasValue)
                    query = query.Where(e => e.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(e => e.Timestamp <= endDate.Value);

                return await query
                    .OrderByDescending(e => e.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security events for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> IsSuspiciousActivityAsync(string userId, string ipAddress)
        {
            try
            {
                var recentEvents = await _context.SecurityEvents
                    .Where(e => e.UserId == userId && e.Timestamp >= DateTime.UtcNow.AddHours(-1))
                    .ToListAsync();

                // Şüpheli aktivite kriterleri
                var failedLoginAttempts = recentEvents.Count(e => e.EventType == "FailedLogin");
                var unusualIpAddress = !recentEvents.Any(e => e.IpAddress == ipAddress);
                var rapidRequests = recentEvents.Count >= 100;

                return failedLoginAttempts >= 5 || unusualIpAddress || rapidRequests;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking suspicious activity for user {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<SessionInfo>> GetActiveSessionsAsync(string userId)
        {
            try
            {
                // Son 24 saat içindeki login olaylarını al
                var loginEvents = await _context.SecurityEvents
                    .Where(e => e.UserId == userId && 
                           e.EventType == "Login" && 
                           e.Timestamp >= DateTime.UtcNow.AddHours(-24))
                    .OrderByDescending(e => e.Timestamp)
                    .ToListAsync();

                // Her benzersiz IP ve UserAgent kombinasyonu için bir oturum oluştur
                var sessions = loginEvents
                    .GroupBy(e => new { e.IpAddress, e.UserAgent })
                    .Select(g => new SessionInfo
                    {
                        SessionId = $"{g.Key.IpAddress}_{g.Key.UserAgent}",
                        IpAddress = g.Key.IpAddress ?? "Unknown",
                        UserAgent = g.Key.UserAgent ?? "Unknown",
                        LastActivity = g.Max(e => e.Timestamp),
                        CreatedAt = g.Min(e => e.Timestamp)
                    })
                    .ToList();

                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active sessions for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> TerminateSessionAsync(string sessionId)
        {
            try
            {
                var parts = sessionId.Split('_');
                if (parts.Length != 2)
                    return false;

                var ipAddress = parts[0];
                var userAgent = parts[1];

                // Oturum sonlandırma olayını kaydet
                await LogSecurityEventAsync(
                    "system",
                    "SessionTerminated",
                    $"Session terminated for IP: {ipAddress}, UserAgent: {userAgent}"
                );

                // İlgili IP ve UserAgent'a sahip son login olayını bul
                var lastLoginEvent = await _context.SecurityEvents
                    .Where(e => e.EventType == "Login" && 
                           e.IpAddress == ipAddress && 
                           e.UserAgent == userAgent)
                    .OrderByDescending(e => e.Timestamp)
                    .FirstOrDefaultAsync();

                if (lastLoginEvent != null)
                {
                    // Oturum sonlandırma olayını kullanıcıya bağla
                    await LogSecurityEventAsync(
                        lastLoginEvent.UserId,
                        "SessionTerminated",
                        $"User session terminated from IP: {ipAddress}, UserAgent: {userAgent}"
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating session {SessionId}", sessionId);
                throw;
            }
        }

        // Şifreleme ve Veri Güvenliği
        public async Task<string> EncryptDataAsync(string data)
        {
            try
            {
                var encryptionKey = _configuration["Encryption:Key"];
                var encryptionIV = _configuration["Encryption:IV"];

                if (string.IsNullOrEmpty(encryptionKey) || string.IsNullOrEmpty(encryptionIV))
                {
                    throw new InvalidOperationException("Encryption key or IV is not configured");
                }

                return await Task.Run(() =>
                {
                    using var aes = Aes.Create();
                    aes.Key = Encoding.UTF8.GetBytes(encryptionKey);
                    aes.IV = Encoding.UTF8.GetBytes(encryptionIV);

                    using var encryptor = aes.CreateEncryptor();
                    var plainBytes = Encoding.UTF8.GetBytes(data);
                    var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                    return Convert.ToBase64String(cipherBytes);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting data");
                throw;
            }
        }

        public async Task<string> DecryptDataAsync(string encryptedData)
        {
            try
            {
                var encryptionKey = _configuration["Encryption:Key"];
                var encryptionIV = _configuration["Encryption:IV"];

                if (string.IsNullOrEmpty(encryptionKey) || string.IsNullOrEmpty(encryptionIV))
                {
                    throw new InvalidOperationException("Encryption key or IV is not configured");
                }

                if (string.IsNullOrEmpty(encryptedData))
                {
                    throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));
                }

                return await Task.Run(() =>
                {
                    using var aes = Aes.Create();
                    aes.Key = Encoding.UTF8.GetBytes(encryptionKey);
                    aes.IV = Encoding.UTF8.GetBytes(encryptionIV);

                    using var decryptor = aes.CreateDecryptor();
                    var cipherBytes = Convert.FromBase64String(encryptedData);
                    var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

                    return Encoding.UTF8.GetString(plainBytes);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting data");
                throw;
            }
        }

        public async Task<string> HashPasswordAsync(string password)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var sha256 = SHA256.Create();
                    var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                    return Convert.ToBase64String(hashedBytes);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing password");
                throw;
            }
        }

        public async Task<bool> ValidatePasswordAsync(string password, string hashedPassword)
        {
            try
            {
                var hashedInput = await HashPasswordAsync(password);
                return hashedInput == hashedPassword;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating password");
                throw;
            }
        }

        public async Task<string> GenerateSecureKeyAsync()
        {
            try
            {
                return await Task.Run(() =>
                {
                    var keyBytes = RandomNumberGenerator.GetBytes(32);
                    return Convert.ToBase64String(keyBytes);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating secure key");
                throw;
            }
        }

        public Task<string> MaskSensitiveDataAsync(string data, string dataType)
        {
            try
            {
                var result = dataType.ToLower() switch
                {
                    "email" => MaskEmail(data),
                    "phone" => MaskPhoneNumber(data),
                    "creditcard" => MaskCreditCard(data),
                    "ssn" => MaskSSN(data),
                    _ => data
                };
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error masking sensitive data");
                throw;
            }
        }

        // Güvenlik Duvarı ve Korumalar
        public async Task<bool> IsRateLimitExceededAsync(string userId, string action)
        {
            try
            {
                var timeWindow = TimeSpan.FromMinutes(1);
                var maxRequests = 100;

                var requestCount = await _context.SecurityEvents
                    .CountAsync(e => e.UserId == userId && 
                                   e.EventType == action && 
                                   e.Timestamp >= DateTime.UtcNow.Subtract(timeWindow));

                return requestCount >= maxRequests;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking rate limit for user {UserId}", userId);
                throw;
            }
        }

        public Task<bool> ValidateInputAsync(string input, string inputType)
        {
            try
            {
                var result = inputType.ToLower() switch
                {
                    "email" => IsValidEmail(input),
                    "phone" => IsValidPhoneNumber(input),
                    "username" => IsValidUsername(input),
                    "password" => IsValidPassword(input),
                    _ => true
                };
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating input");
                throw;
            }
        }

        public Task<bool> IsRequestValidAsync(HttpContext context)
        {
            try
            {
                // CSRF token kontrolü
                var csrfToken = context.Request.Headers["X-CSRF-Token"].ToString();
                if (string.IsNullOrEmpty(csrfToken))
                    return Task.FromResult(false);

                // Origin kontrolü
                var origin = context.Request.Headers["Origin"].ToString();
                if (!IsValidOrigin(origin))
                    return Task.FromResult(false);

                // Content-Type kontrolü
                var contentType = context.Request.ContentType ?? string.Empty;
                if (!IsValidContentType(contentType))
                    return Task.FromResult(false);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating request");
                throw;
            }
        }

        public async Task<bool> ValidateFileUploadAsync(IFormFile file)
        {
            try
            {
                return await Task.Run(() =>
                {
                    // Dosya boyutu kontrolü
                    if (file.Length > 10 * 1024 * 1024) // 10MB
                        return false;

                    // Dosya uzantısı kontrolü
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx" };
                    var extension = Path.GetExtension(file.FileName).ToLower();
                    if (!allowedExtensions.Contains(extension))
                        return false;

                    // Dosya içeriği kontrolü
                    using var reader = new BinaryReader(file.OpenReadStream());
                    var signatures = new Dictionary<string, byte[]>
                    {
                        { ".png", new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
                        { ".jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
                        { ".pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 } }
                    };

                    if (signatures.ContainsKey(extension))
                    {
                        var headerBytes = reader.ReadBytes(signatures[extension].Length);
                        if (!headerBytes.SequenceEqual(signatures[extension]))
                            return false;
                    }

                    return true;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating file upload");
                throw;
            }
        }

        // Güvenlik Raporlama ve Analiz
        public async Task<SecurityReport> GenerateSecurityReportAsync(string userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var events = await _context.SecurityEvents
                    .Where(e => e.UserId == userId && e.Timestamp >= startDate && e.Timestamp <= endDate)
                    .ToListAsync();

                var report = new SecurityReport
                {
                    UserId = userId,
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalEvents = events.Count,
                    EventTypes = events.GroupBy(e => e.EventType)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    SuspiciousActivities = events.Count(e => e.EventType == "SuspiciousActivity"),
                    FailedLogins = events.Count(e => e.EventType == "FailedLogin"),
                    SuccessfulLogins = events.Count(e => e.EventType == "SuccessfulLogin"),
                    IpAddresses = events.Where(e => e.IpAddress != null)
                        .Select(e => e.IpAddress!)
                        .Distinct()
                        .ToList()
                };

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating security report for user {UserId}", userId);
                throw;
            }
        }

        public async Task<SecurityMetrics> GetSecurityMetricsAsync()
        {
            try
            {
                var metrics = new SecurityMetrics
                {
                    TotalUsers = await _context.Users.CountAsync(),
                    ActiveSessions = await _context.SecurityEvents
                        .Where(e => e.EventType == "Login" && 
                               e.Timestamp >= DateTime.UtcNow.AddHours(-24))
                        .Select(e => new { e.UserId, e.IpAddress, e.UserAgent })
                        .Distinct()
                        .CountAsync(),
                    FailedLoginAttempts = await _context.SecurityEvents.CountAsync(e => e.EventType == "FailedLogin" && e.Timestamp >= DateTime.UtcNow.AddHours(-1)),
                    SuspiciousActivities = await _context.SecurityEvents.CountAsync(e => e.EventType == "SuspiciousActivity" && e.Timestamp >= DateTime.UtcNow.AddHours(-1))
                };

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security metrics");
                throw;
            }
        }

        public async Task<IEnumerable<SecurityVulnerability>> ScanForVulnerabilitiesAsync()
        {
            try
            {
                var vulnerabilities = new List<SecurityVulnerability>();

                // Şifre güvenliği kontrolü
                var weakPasswords = await _context.Users
                    .Where(u => u.PasswordHash != null && u.PasswordHash.Length < 32)
                    .Select(u => new SecurityVulnerability
                    {
                        Type = "WeakPassword",
                        Severity = "High",
                        Description = $"User {u.UserName} has a weak password",
                        AffectedResource = u.Id
                    })
                    .ToListAsync();

                vulnerabilities.AddRange(weakPasswords);

                // Eski oturumlar kontrolü - SecurityEvents üzerinden
                var oldSessions = await _context.SecurityEvents
                    .Where(e => e.EventType == "Login" && 
                           e.Timestamp < DateTime.UtcNow.AddDays(-30))
                    .GroupBy(e => new { e.UserId, e.IpAddress, e.UserAgent })
                    .Select(g => new SecurityVulnerability
                    {
                        Type = "OldSession",
                        Severity = "Medium",
                        Description = $"User {g.Key.UserId} has an old session from IP {g.Key.IpAddress} using {g.Key.UserAgent}",
                        AffectedResource = g.Key.UserId
                    })
                    .ToListAsync();

                vulnerabilities.AddRange(oldSessions);

                return vulnerabilities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning for vulnerabilities");
                throw;
            }
        }

        public async Task<ComplianceReport> GenerateComplianceReportAsync()
        {
            try
            {
                return await Task.Run(() =>
                {
                    var report = new ComplianceReport
                    {
                        GeneratedAt = DateTime.UtcNow,
                        PasswordPolicy = new PasswordPolicyCompliance
                        {
                            MinLength = 8,
                            RequiresUppercase = true,
                            RequiresLowercase = true,
                            RequiresNumbers = true,
                            RequiresSpecialChars = true
                        },
                        SessionPolicy = new SessionPolicyCompliance
                        {
                            MaxSessionDuration = TimeSpan.FromHours(24),
                            RequiresSecureCookies = true,
                            RequiresHttps = true
                        },
                        DataProtection = new DataProtectionCompliance
                        {
                            EncryptionEnabled = true,
                            MaskingEnabled = true,
                            BackupEnabled = true
                        }
                    };

                    return report;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating compliance report");
                throw;
            }
        }

        // Yardımcı Metodlar
        private string MaskEmail(string email)
        {
            var parts = email.Split('@');
            if (parts.Length != 2)
                return email;

            var name = parts[0];
            var domain = parts[1];

            if (name.Length <= 2)
                return $"{name[0]}***@{domain}";

            return $"{name[0]}{new string('*', name.Length - 2)}{name[name.Length - 1]}@{domain}";
        }

        private string MaskPhoneNumber(string phone)
        {
            if (string.IsNullOrEmpty(phone) || phone.Length < 10)
                return phone;

            return $"{phone.Substring(0, 3)}***{phone.Substring(phone.Length - 4)}";
        }

        private string MaskCreditCard(string card)
        {
            if (string.IsNullOrEmpty(card) || card.Length < 16)
                return card;

            return $"{card.Substring(0, 4)}****{card.Substring(card.Length - 4)}";
        }

        private string MaskSSN(string ssn)
        {
            if (string.IsNullOrEmpty(ssn) || ssn.Length < 9)
                return ssn;

            return $"***-**-{ssn.Substring(ssn.Length - 4)}";
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private bool IsValidPhoneNumber(string phone)
        {
            return Regex.IsMatch(phone, @"^\+?[1-9]\d{1,14}$");
        }

        private bool IsValidUsername(string username)
        {
            return Regex.IsMatch(username, @"^[a-zA-Z0-9_]{3,20}$");
        }

        private bool IsValidPassword(string password)
        {
            return password.Length >= 8 &&
                   password.Any(char.IsUpper) &&
                   password.Any(char.IsLower) &&
                   password.Any(char.IsDigit) &&
                   password.Any(c => !char.IsLetterOrDigit(c));
        }

        private bool IsValidOrigin(string origin)
        {
            var allowedOrigins = _configuration.GetSection("AllowedOrigins").Get<string[]>();
            if (allowedOrigins == null)
            {
                _logger.LogWarning("AllowedOrigins configuration is missing or null.");
                return false;
            }

            return allowedOrigins.Contains(origin);
        }

        private bool IsValidContentType(string contentType)
        {
            var allowedTypes = new[] { "application/json", "application/x-www-form-urlencoded", "multipart/form-data" };
            return allowedTypes.Contains(contentType?.ToLower());
        }
    }

    public class SecurityEvent
    {
        public required string Id { get; set; }
        public required string UserId { get; set; }
        public required string EventType { get; set; }
        public required string Description { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class SessionInfo
    {
        public required string SessionId { get; set; }
        public required string IpAddress { get; set; }
        public required string UserAgent { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class SecurityReport
    {
        public required string UserId { get; set; }
        public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(-30);
        public DateTime EndDate { get; set; } = DateTime.UtcNow;
        public int TotalEvents { get; set; }
        public required Dictionary<string, int> EventTypes { get; set; }
        public int SuspiciousActivities { get; set; }
        public int FailedLogins { get; set; }
        public int SuccessfulLogins { get; set; }
        public required List<string> IpAddresses { get; set; }
    }

    public class SecurityMetrics
    {
        public int TotalUsers { get; set; }
        public int ActiveSessions { get; set; }
        public int FailedLoginAttempts { get; set; }
        public int SuspiciousActivities { get; set; }
    }

    public class SecurityVulnerability
    {
        public required string Type { get; set; }
        public required string Severity { get; set; }
        public required string Description { get; set; }
        public required string AffectedResource { get; set; }
    }

    public class ComplianceReport
    {
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public required PasswordPolicyCompliance PasswordPolicy { get; set; }
        public required SessionPolicyCompliance SessionPolicy { get; set; }
        public required DataProtectionCompliance DataProtection { get; set; }
    }

    public class PasswordPolicyCompliance
    {
        public int MinLength { get; set; }
        public bool RequiresUppercase { get; set; }
        public bool RequiresLowercase { get; set; }
        public bool RequiresNumbers { get; set; }
        public bool RequiresSpecialChars { get; set; }
    }

    public class SessionPolicyCompliance
    {
        public TimeSpan MaxSessionDuration { get; set; }
        public bool RequiresSecureCookies { get; set; }
        public bool RequiresHttps { get; set; }
    }

    public class DataProtectionCompliance
    {
        public bool EncryptionEnabled { get; set; }
        public bool MaskingEnabled { get; set; }
        public bool BackupEnabled { get; set; }
    }
} 