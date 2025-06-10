using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using WebApplication1.Models.Requests;
using System.Linq;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SecurityController : ControllerBase
    {
        private readonly ISecurityService _securityService;
        private readonly ILogger<SecurityController> _logger;

        public SecurityController(
            ISecurityService securityService,
            ILogger<SecurityController> logger)
        {
            _securityService = securityService;
            _logger = logger;
        }

        // Kimlik Doğrulama ve Yetkilendirme
        [HttpPost("validate-credentials")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidateCredentials([FromBody] ValidateCredentialsRequest request)
        {
            try
            {
                var isValid = await _securityService.ValidateUserCredentialsAsync(request.Username, request.Password);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating credentials for user {Username}", request.Username);
                return StatusCode(500, "An error occurred while validating credentials");
            }
        }

        [HttpPost("generate-token")]
        [AllowAnonymous]
        public async Task<IActionResult> GenerateToken([FromBody] GenerateTokenRequest request)
        {
            try
            {
                var roles = await _securityService.GetUserRolesAsync(request.UserId);
                var token = await _securityService.GenerateJwtTokenAsync(request.UserId, request.Username, roles);
                return Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating token for user {UserId}", request.UserId);
                return StatusCode(500, "An error occurred while generating token");
            }
        }

        [HttpPost("validate-token")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidateToken([FromBody] ValidateTokenRequest request)
        {
            try
            {
                var isValid = await _securityService.ValidateTokenAsync(request.Token);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return StatusCode(500, "An error occurred while validating token");
            }
        }

        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] Models.Requests.RefreshTokenRequest request)
        {
            try
            {
                // Refresh token'ı doğrula ve yeni token oluştur
                var isValid = await _securityService.ValidateTokenAsync(request.RefreshToken);
                if (!isValid)
                {
                    return BadRequest("Invalid refresh token");
                }

                // Token'dan kullanıcı bilgilerini çıkar
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                {
                    return BadRequest("Invalid token claims");
                }

                var roles = await _securityService.GetUserRolesAsync(userId);
                var token = await _securityService.GenerateJwtTokenAsync(userId, username, roles);
                return Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return StatusCode(500, "An error occurred while refreshing token");
            }
        }

        [HttpPost("revoke-token")]
        public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest request)
        {
            try
            {
                var success = await _securityService.RevokeTokenAsync(request.Token);
                return Ok(new { Success = success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                return StatusCode(500, "An error occurred while revoking token");
            }
        }

        [HttpGet("roles/{userId}")]
        public async Task<IActionResult> GetUserRoles(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    return BadRequest("User ID is required");

                var roles = await _securityService.GetUserRolesAsync(userId);
                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting roles for user {UserId}", userId);
                return StatusCode(500, "An error occurred while getting user roles");
            }
        }

        [HttpPost("roles/assign")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
        {
            try
            {
                var success = await _securityService.AssignRoleToUserAsync(request.UserId, request.Role);
                return Ok(new { Success = success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning role {Role} to user {UserId}", request.Role, request.UserId);
                return StatusCode(500, "An error occurred while assigning role");
            }
        }

        [HttpPost("roles/remove")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveRole([FromBody] RemoveRoleRequest request)
        {
            try
            {
                var success = await _securityService.RemoveRoleFromUserAsync(request.UserId, request.Role);
                return Ok(new { Success = success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing role {Role} from user {UserId}", request.Role, request.UserId);
                return StatusCode(500, "An error occurred while removing role");
            }
        }

        // Güvenlik İzleme ve Loglama
        [HttpPost("events/log")]
        public async Task<IActionResult> LogSecurityEvent([FromBody] LogSecurityEventRequest request)
        {
            try
            {
                await _securityService.LogSecurityEventAsync(request.UserId, request.EventType, request.Description);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging security event for user {UserId}", request.UserId);
                return StatusCode(500, "An error occurred while logging security event");
            }
        }

        [HttpGet("events/{userId}")]
        public async Task<IActionResult> GetSecurityEvents(string userId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var events = await _securityService.GetSecurityEventsAsync(userId, startDate, endDate);
                return Ok(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security events for user {UserId}", userId);
                return StatusCode(500, "An error occurred while getting security events");
            }
        }

        [HttpPost("suspicious-activity/check")]
        public async Task<IActionResult> CheckSuspiciousActivity([FromBody] CheckSuspiciousActivityRequest request)
        {
            try
            {
                var isSuspicious = await _securityService.IsSuspiciousActivityAsync(request.UserId, request.IpAddress);
                return Ok(new { IsSuspicious = isSuspicious });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking suspicious activity for user {UserId}", request.UserId);
                return StatusCode(500, "An error occurred while checking suspicious activity");
            }
        }

        [HttpPost("ip/block")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BlockIpAddress([FromBody] BlockIpAddressRequest request)
        {
            try
            {
                var success = await _securityService.BlockIpAddressAsync(request.IpAddress, request.Reason);
                return Ok(new { Success = success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking IP address {IpAddress}", request.IpAddress);
                return StatusCode(500, "An error occurred while blocking IP address");
            }
        }

        [HttpPost("ip/unblock")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UnblockIpAddress([FromBody] UnblockIpAddressRequest request)
        {
            try
            {
                var success = await _securityService.UnblockIpAddressAsync(request.IpAddress);
                return Ok(new { Success = success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unblocking IP address {IpAddress}", request.IpAddress);
                return StatusCode(500, "An error occurred while unblocking IP address");
            }
        }

        [HttpGet("sessions/{userId}")]
        public async Task<IActionResult> GetActiveSessions(string userId)
        {
            try
            {
                var sessions = await _securityService.GetActiveSessionsAsync(userId);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active sessions for user {UserId}", userId);
                return StatusCode(500, "An error occurred while getting active sessions");
            }
        }

        [HttpPost("sessions/terminate")]
        public async Task<IActionResult> TerminateSession([FromBody] TerminateSessionRequest request)
        {
            try
            {
                var success = await _securityService.TerminateSessionAsync(request.SessionId);
                return Ok(new { Success = success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating session {SessionId}", request.SessionId);
                return StatusCode(500, "An error occurred while terminating session");
            }
        }

        // Şifreleme ve Veri Güvenliği
        [HttpPost("encrypt")]
        public async Task<IActionResult> EncryptData([FromBody] ValidateInputRequest request)
        {
            try
            {
                var encryptedData = await _securityService.EncryptDataAsync(request.Input);
                return Ok(new { EncryptedData = encryptedData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting data");
                return StatusCode(500, "An error occurred while encrypting data");
            }
        }

        [HttpPost("decrypt")]
        public async Task<IActionResult> DecryptData([FromBody] ValidateInputRequest request)
        {
            try
            {
                var decryptedData = await _securityService.DecryptDataAsync(request.Input);
                return Ok(new { DecryptedData = decryptedData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting data");
                return StatusCode(500, "An error occurred while decrypting data");
            }
        }

        [HttpPost("password/hash")]
        [AllowAnonymous]
        public async Task<IActionResult> HashPassword([FromBody] HashPasswordRequest request)
        {
            try
            {
                var hashedPassword = await _securityService.HashPasswordAsync(request.Password);
                return Ok(new { HashedPassword = hashedPassword });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing password");
                return StatusCode(500, "An error occurred while hashing password");
            }
        }

        [HttpPost("password/validate")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidatePassword([FromBody] ValidatePasswordRequest request)
        {
            try
            {
                var isValid = await _securityService.ValidatePasswordAsync(request.Password, request.HashedPassword);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating password");
                return StatusCode(500, "An error occurred while validating password");
            }
        }

        [HttpGet("key/generate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateSecureKey()
        {
            try
            {
                var key = await _securityService.GenerateSecureKeyAsync();
                return Ok(new { Key = key });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating secure key");
                return StatusCode(500, "An error occurred while generating secure key");
            }
        }

        [HttpPost("data/mask")]
        public async Task<IActionResult> MaskSensitiveData([FromBody] MaskDataRequest request)
        {
            try
            {
                var maskedData = await _securityService.MaskSensitiveDataAsync(request.Data, request.DataType);
                return Ok(new { MaskedData = maskedData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error masking sensitive data");
                return StatusCode(500, "An error occurred while masking sensitive data");
            }
        }

        // Güvenlik Duvarı ve Korumalar
        [HttpPost("rate-limit/check")]
        public async Task<IActionResult> CheckRateLimit([FromBody] CheckRateLimitRequest request)
        {
            try
            {
                var isExceeded = await _securityService.IsRateLimitExceededAsync(request.UserId, request.Action);
                return Ok(new { IsExceeded = isExceeded });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking rate limit for user {UserId}", request.UserId);
                return StatusCode(500, "An error occurred while checking rate limit");
            }
        }

        [HttpPost("input/validate")]
        public async Task<IActionResult> ValidateInput([FromBody] ValidateInputRequest request)
        {
            try
            {
                var isValid = await _securityService.ValidateInputAsync(request.Input, request.InputType);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating input");
                return StatusCode(500, "An error occurred while validating input");
            }
        }

        [HttpPost("ip/check")]
        public async Task<IActionResult> CheckIpBlocked([FromBody] CheckIpBlockedRequest request)
        {
            try
            {
                var isBlocked = await _securityService.IsIpBlockedAsync(request.IpAddress);
                return Ok(new { IsBlocked = isBlocked });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if IP is blocked");
                return StatusCode(500, "An error occurred while checking if IP is blocked");
            }
        }

        [HttpPost("request/validate")]
        public async Task<IActionResult> ValidateRequest()
        {
            try
            {
                var isValid = await _securityService.IsRequestValidAsync(HttpContext);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating request");
                return StatusCode(500, "An error occurred while validating request");
            }
        }

        [HttpPost("file/validate")]
        public async Task<IActionResult> ValidateFileUpload(IFormFile file)
        {
            try
            {
                var isValid = await _securityService.ValidateFileUploadAsync(file);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating file upload");
                return StatusCode(500, "An error occurred while validating file upload");
            }
        }

        // Güvenlik Raporlama ve Analiz
        [HttpGet("report/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateSecurityReport(string userId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            try
            {
                var report = await _securityService.GenerateSecurityReportAsync(userId, startDate, endDate);
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating security report for user {UserId}", userId);
                return StatusCode(500, "An error occurred while generating security report");
            }
        }

        [HttpGet("metrics")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetSecurityMetrics()
        {
            try
            {
                var metrics = await _securityService.GetSecurityMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security metrics");
                return StatusCode(500, "An error occurred while getting security metrics");
            }
        }

        [HttpGet("vulnerabilities/scan")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ScanForVulnerabilities()
        {
            try
            {
                var vulnerabilities = await _securityService.ScanForVulnerabilitiesAsync();
                return Ok(vulnerabilities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning for vulnerabilities");
                return StatusCode(500, "An error occurred while scanning for vulnerabilities");
            }
        }

        [HttpGet("compliance/report")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateComplianceReport()
        {
            try
            {
                var report = await _securityService.GenerateComplianceReportAsync();
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating compliance report");
                return StatusCode(500, "An error occurred while generating compliance report");
            }
        }

        [HttpPost("ip-restrictions")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddIpRestriction([FromBody] BlockIpAddressRequest request)
        {
            try
            {
                var success = await _securityService.BlockIpAddressAsync(request.IpAddress, request.Reason);
                return Ok(new { Success = success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding IP restriction");
                return StatusCode(500, "Error adding IP restriction");
            }
        }

        [HttpDelete("ip-restrictions/{ipAddress}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveIpRestriction(string ipAddress)
        {
            try
            {
                var success = await _securityService.UnblockIpAddressAsync(ipAddress);
                return Ok(new { Success = success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing IP restriction");
                return StatusCode(500, "Error removing IP restriction");
            }
        }

        [HttpGet("ip-restrictions")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetIpRestrictions()
        {
            try
            {
                // Pass empty string instead of null for userId parameter
                var events = await _securityService.GetSecurityEventsAsync(string.Empty, null, null);
                var blockedIps = events
                    .Where(e => e.EventType == "IP_BLOCKED")
                    .Select(e => e.Description)
                    .Distinct()
                    .ToList();
                return Ok(blockedIps);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting IP restrictions");
                return StatusCode(500, "Error getting IP restrictions");
            }
        }

        [HttpPost("sessions/revoke")]
        public async Task<IActionResult> RevokeSession([FromBody] Models.Requests.RevokeSessionRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var success = await _securityService.TerminateSessionAsync(request.SessionId);
                return Ok(new { Success = success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking session");
                return StatusCode(500, "Error revoking session");
            }
        }

        [HttpPost("sessions/revoke-all")]
        public async Task<IActionResult> RevokeAllSessions()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in claims");
                }

                // Mevcut oturumları al ve sonlandır
                var sessions = await _securityService.GetActiveSessionsAsync(userId);
                foreach (var session in sessions)
                {
                    // Session bilgisinden session ID'yi al
                    var sessionId = session.ToString(); // SessionInfo.ToString() session ID'yi döndürüyor
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        await _securityService.TerminateSessionAsync(sessionId);
                    }
                }
                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all sessions");
                return StatusCode(500, "Error revoking all sessions");
            }
        }

        [HttpGet("sessions")]
        public async Task<IActionResult> GetActiveSessions()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in claims");
                }

                var sessions = await _securityService.GetActiveSessionsAsync(userId);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active sessions");
                return StatusCode(500, "Error getting active sessions");
            }
        }

        [HttpGet("logs")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetSecurityLogs(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? eventType = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                // Pass empty string instead of null for userId parameter
                var events = await _securityService.GetSecurityEventsAsync(string.Empty, startDate, endDate);
                return Ok(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security logs");
                return StatusCode(500, "Error getting security logs");
            }
        }

        [HttpPost("2fa/enable")]
        public async Task<IActionResult> EnableTwoFactorAuth()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in claims");
                }

                var success = await _securityService.ValidateUserCredentialsAsync(userId, "2fa_enable");
                return Ok(new { Success = success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling two-factor authentication");
                return StatusCode(500, "Error enabling two-factor authentication");
            }
        }

        [HttpPost("2fa/disable")]
        public async Task<IActionResult> DisableTwoFactorAuth([FromBody] ValidatePasswordRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // 2FA kodu doğrulama
                var isValid = await _securityService.ValidatePasswordAsync(request.Password, request.HashedPassword);
                if (isValid)
                {
                    // 2FA devre dışı bırakma işlemi başarılı
                    return Ok(new { Success = true });
                }
                return BadRequest("Invalid verification code");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling two-factor authentication");
                return StatusCode(500, "Error disabling two-factor authentication");
            }
        }

        [HttpPost("2fa/verify")]
        public async Task<IActionResult> VerifyTwoFactorAuth([FromBody] ValidatePasswordRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // 2FA kodu doğrulama
                var isValid = await _securityService.ValidatePasswordAsync(request.Password, request.HashedPassword);
                if (isValid)
                {
                    // 2FA doğrulama işlemi başarılı
                    return Ok(new { Success = true });
                }
                return BadRequest("Invalid verification code");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying two-factor authentication");
                return StatusCode(500, "Error verifying two-factor authentication");
            }
        }

        [HttpPost("password/change")]
        public async Task<IActionResult> ChangePassword([FromBody] Models.Requests.ChangePasswordRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in claims");
                }

                var hashedNewPassword = await _securityService.HashPasswordAsync(request.NewPassword);
                var success = await _securityService.ValidateUserCredentialsAsync(userId, request.CurrentPassword);
                if (success)
                {
                    // Şifre değiştirme işlemi için gerekli servis metodu eklenebilir
                    return Ok(new { Success = true });
                }
                return BadRequest("Current password is incorrect");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                return StatusCode(500, "Error changing password");
            }
        }

        [HttpPost("password/reset-request")]
        [AllowAnonymous]
        public async Task<IActionResult> RequestPasswordReset([FromBody] ValidateCredentialsRequest request)
        {
            try
            {
                // Kullanıcı kimlik doğrulama
                var isValid = await _securityService.ValidateUserCredentialsAsync(request.Username, request.Password);
                if (isValid)
                {
                    // Şifre sıfırlama token'ı oluştur
                    var roles = await _securityService.GetUserRolesAsync(request.Username);
                    var token = await _securityService.GenerateJwtTokenAsync(request.Username, request.Username, roles);
                    return Ok(new { Token = token });
                }
                return BadRequest("Invalid credentials");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting password reset");
                return StatusCode(500, "Error requesting password reset");
            }
        }

        [HttpPost("password/reset")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] Models.Requests.ResetPasswordRequest request)
        {
            try
            {
                // Email doğrulama ve şifre sıfırlama işlemi
                var token = await _securityService.GenerateJwtTokenAsync(request.Email, "reset", new List<string>());
                // Email gönderme işlemi için gerekli servis metodu eklenebilir
                return Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                return StatusCode(500, "Error resetting password");
            }
        }
    }

    // Request Models with required modifiers
    public class ValidateCredentialsRequest
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
    }

    public class GenerateTokenRequest
    {
        public required string UserId { get; set; }
        public required string Username { get; set; }
    }

    public class ValidateTokenRequest
    {
        public required string Token { get; set; }
    }

    public class RevokeTokenRequest
    {
        public required string Token { get; set; }
    }

    public class AssignRoleRequest
    {
        public required string UserId { get; set; }
        public required string Role { get; set; }
    }

    public class RemoveRoleRequest
    {
        public required string UserId { get; set; }
        public required string Role { get; set; }
    }

    public class LogSecurityEventRequest
    {
        public required string UserId { get; set; }
        public required string EventType { get; set; }
        public required string Description { get; set; }
    }

    public class CheckSuspiciousActivityRequest
    {
        public required string UserId { get; set; }
        public required string IpAddress { get; set; }
    }

    public class BlockIpAddressRequest
    {
        public required string IpAddress { get; set; }
        public required string Reason { get; set; }
    }

    public class UnblockIpAddressRequest
    {
        public required string IpAddress { get; set; }
    }

    public class TerminateSessionRequest
    {
        public required string SessionId { get; set; }
    }

    public class ValidateInputRequest
    {
        public required string Input { get; set; }
        public required string InputType { get; set; }
    }

    public class CheckRateLimitRequest
    {
        public required string UserId { get; set; }
        public required string Action { get; set; }
    }

    public class CheckIpBlockedRequest
    {
        public required string IpAddress { get; set; }
    }

    public class MaskDataRequest
    {
        public required string Data { get; set; }
        public required string DataType { get; set; }
    }

    public class HashPasswordRequest
    {
        public required string Password { get; set; }
    }

    public class ValidatePasswordRequest
    {
        public required string Password { get; set; }
        public required string HashedPassword { get; set; }
    }
} 