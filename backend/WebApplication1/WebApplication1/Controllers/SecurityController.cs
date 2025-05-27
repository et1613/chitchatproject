using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SecurityController : ControllerBase
    {
        private readonly ISecurityService _securityService;
        private readonly ILogger<SecurityController> _logger;

        public SecurityController(ISecurityService securityService, ILogger<SecurityController> logger)
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
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var success = await _securityService.RefreshTokenAsync(request.RefreshToken);
                return Ok(new { Success = success });
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
        public async Task<IActionResult> EncryptData([FromBody] EncryptDataRequest request)
        {
            try
            {
                var encryptedData = await _securityService.EncryptDataAsync(request.Data);
                return Ok(new { EncryptedData = encryptedData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting data");
                return StatusCode(500, "An error occurred while encrypting data");
            }
        }

        [HttpPost("decrypt")]
        public async Task<IActionResult> DecryptData([FromBody] DecryptDataRequest request)
        {
            try
            {
                var decryptedData = await _securityService.DecryptDataAsync(request.EncryptedData);
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
    }

    // Request Models
    public class ValidateCredentialsRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class GenerateTokenRequest
    {
        public string UserId { get; set; }
        public string Username { get; set; }
    }

    public class ValidateTokenRequest
    {
        public string Token { get; set; }
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; }
    }

    public class RevokeTokenRequest
    {
        public string Token { get; set; }
    }

    public class AssignRoleRequest
    {
        public string UserId { get; set; }
        public string Role { get; set; }
    }

    public class RemoveRoleRequest
    {
        public string UserId { get; set; }
        public string Role { get; set; }
    }

    public class LogSecurityEventRequest
    {
        public string UserId { get; set; }
        public string EventType { get; set; }
        public string Description { get; set; }
    }

    public class CheckSuspiciousActivityRequest
    {
        public string UserId { get; set; }
        public string IpAddress { get; set; }
    }

    public class BlockIpAddressRequest
    {
        public string IpAddress { get; set; }
        public string Reason { get; set; }
    }

    public class UnblockIpAddressRequest
    {
        public string IpAddress { get; set; }
    }

    public class TerminateSessionRequest
    {
        public string SessionId { get; set; }
    }

    public class EncryptDataRequest
    {
        public string Data { get; set; }
    }

    public class DecryptDataRequest
    {
        public string EncryptedData { get; set; }
    }

    public class HashPasswordRequest
    {
        public string Password { get; set; }
    }

    public class ValidatePasswordRequest
    {
        public string Password { get; set; }
        public string HashedPassword { get; set; }
    }

    public class MaskDataRequest
    {
        public string Data { get; set; }
        public string DataType { get; set; }
    }

    public class CheckRateLimitRequest
    {
        public string UserId { get; set; }
        public string Action { get; set; }
    }

    public class ValidateInputRequest
    {
        public string Input { get; set; }
        public string InputType { get; set; }
    }

    public class CheckIpBlockedRequest
    {
        public string IpAddress { get; set; }
    }
} 