using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Security.Claims;
using WebApplication1.Models.Requests;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EmailController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly IAuthService _authService;
        private readonly ILogger<EmailController> _logger;

        public EmailController(
            IEmailService emailService,
            IAuthService authService,
            ILogger<EmailController> logger)
        {
            _emailService = emailService;
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendEmail([FromBody] SendEmailRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _emailService.SendEmailAsync(
                    request.To,
                    request.Subject,
                    request.Body,
                    request.IsHtml,
                    request.Attachments);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email");
                return StatusCode(500, "Error sending email");
            }
        }

        [HttpPost("verify")]
        public async Task<IActionResult> SendVerificationEmail()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Generate verification link
                var token = Guid.NewGuid().ToString();
                var verificationLink = $"{Request.Scheme}://{Request.Host}/api/email/verify/{token}";

                await _emailService.SendEmailVerificationAsync(userId, verificationLink);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending verification email");
                return StatusCode(500, "Error sending verification email");
            }
        }

        [HttpPost("verify/{token}")]
        public async Task<IActionResult> VerifyEmail(string token)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _authService.VerifyEmailAsync(userId, token);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying email");
                return StatusCode(500, "Error verifying email");
            }
        }

        [HttpPost("password-reset")]
        public async Task<IActionResult> SendPasswordResetEmail([FromBody] SendPasswordResetEmailRequest request)
        {
            try
            {
                var result = await _authService.ResetPasswordAsync(request.Email);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset email");
                return StatusCode(500, "Error sending password reset email");
            }
        }

        [HttpPost("password-reset/{token}")]
        public async Task<IActionResult> ResetPassword(string token, [FromBody] WebApplication1.Models.Requests.ChangePasswordRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                return StatusCode(500, "Error resetting password");
            }
        }
    }

    public class SendEmailRequest
    {
        public required string To { get; set; }
        public required string Subject { get; set; }
        public required string Body { get; set; }
        public bool IsHtml { get; set; }
        public List<EmailAttachment>? Attachments { get; set; }
    }

    public class SendPasswordResetEmailRequest
    {
        public required string Email { get; set; }
    }
} 