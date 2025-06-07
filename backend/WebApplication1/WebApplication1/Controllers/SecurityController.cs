using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using WebApplication1.Models.Security;
using WebApplication1.Models.Requests;

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

        [HttpPost("ip-restrictions")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddIpRestriction([FromBody] AddIpRestrictionRequest request)
        {
            try
            {
                var result = await _securityService.AddIpRestrictionAsync(
                    request.IpAddress,
                    request.Description,
                    request.ExpirationMinutes);

                return Ok(result);
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
                var result = await _securityService.RemoveIpRestrictionAsync(ipAddress);
                return Ok(result);
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
                var restrictions = await _securityService.GetIpRestrictionsAsync();
                return Ok(restrictions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting IP restrictions");
                return StatusCode(500, "Error getting IP restrictions");
            }
        }

        [HttpPost("sessions/revoke")]
        public async Task<IActionResult> RevokeSession([FromBody] RevokeSessionRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _securityService.RevokeSessionAsync(request.SessionId, userId);
                return Ok(result);
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
                    return Unauthorized();

                var result = await _securityService.RevokeAllSessionsAsync(userId);
                return Ok(result);
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
                    return Unauthorized();

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
                var logs = await _securityService.GetSecurityLogsAsync(
                    startDate,
                    endDate,
                    eventType,
                    page,
                    pageSize);

                return Ok(logs);
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
                    return Unauthorized();

                var result = await _securityService.EnableTwoFactorAuthAsync(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling two-factor authentication");
                return StatusCode(500, "Error enabling two-factor authentication");
            }
        }

        [HttpPost("2fa/disable")]
        public async Task<IActionResult> DisableTwoFactorAuth([FromBody] DisableTwoFactorAuthRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _securityService.DisableTwoFactorAuthAsync(userId, request.Code);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling two-factor authentication");
                return StatusCode(500, "Error disabling two-factor authentication");
            }
        }

        [HttpPost("2fa/verify")]
        public async Task<IActionResult> VerifyTwoFactorAuth([FromBody] VerifyTwoFactorAuthRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _securityService.VerifyTwoFactorAuthAsync(userId, request.Code);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying two-factor authentication");
                return StatusCode(500, "Error verifying two-factor authentication");
            }
        }

        [HttpPost("password/change")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _securityService.ChangePasswordAsync(
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

        [HttpPost("password/reset-request")]
        [AllowAnonymous]
        public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetRequest request)
        {
            try
            {
                var result = await _securityService.RequestPasswordResetAsync(request.Email);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting password reset");
                return StatusCode(500, "Error requesting password reset");
            }
        }

        [HttpPost("password/reset")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var result = await _securityService.ResetPasswordAsync(
                    request.Email,
                    request.Token,
                    request.NewPassword);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                return StatusCode(500, "Error resetting password");
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

    public class AddIpRestrictionRequest
    {
        public required string IpAddress { get; set; }
        public string? Description { get; set; }
        public int ExpirationMinutes { get; set; } = 60;
    }

    public class RevokeSessionRequest
    {
        public required string SessionId { get; set; }
    }

    public class DisableTwoFactorAuthRequest
    {
        public required string Code { get; set; }
    }

    public class VerifyTwoFactorAuthRequest
    {
        public required string Code { get; set; }
    }

    public class ChangePasswordRequest
    {
        public required string CurrentPassword { get; set; }
        public required string NewPassword { get; set; }
    }

    public class RequestPasswordResetRequest
    {
        public required string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        public required string Email { get; set; }
        public required string Token { get; set; }
        public required string NewPassword { get; set; }
    }
} 