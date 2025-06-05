using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication1.Services;
using System.Security.Claims;
using WebApplication1.Models.Email;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EmailController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<EmailController> _logger;

        public EmailController(
            IEmailService emailService,
            ILogger<EmailController> logger)
        {
            _emailService = emailService;
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

        [HttpPost("send-template")]
        public async Task<IActionResult> SendTemplatedEmail([FromBody] SendTemplatedEmailRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _emailService.SendTemplatedEmailAsync(
                    request.To,
                    request.TemplateName,
                    request.TemplateData,
                    request.Attachments);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending templated email");
                return StatusCode(500, "Error sending templated email");
            }
        }

        [HttpPost("send-bulk")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SendBulkEmail([FromBody] SendBulkEmailRequest request)
        {
            try
            {
                var result = await _emailService.SendBulkEmailAsync(
                    request.Recipients,
                    request.Subject,
                    request.Body,
                    request.IsHtml,
                    request.Attachments);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending bulk email");
                return StatusCode(500, "Error sending bulk email");
            }
        }

        [HttpPost("templates")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateEmailTemplate([FromBody] CreateEmailTemplateRequest request)
        {
            try
            {
                var template = await _emailService.CreateEmailTemplateAsync(
                    request.Name,
                    request.Subject,
                    request.Body,
                    request.Description);

                return Ok(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating email template");
                return StatusCode(500, "Error creating email template");
            }
        }

        [HttpGet("templates")]
        public async Task<IActionResult> GetEmailTemplates()
        {
            try
            {
                var templates = await _emailService.GetEmailTemplatesAsync();
                return Ok(templates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting email templates");
                return StatusCode(500, "Error getting email templates");
            }
        }

        [HttpGet("templates/{templateName}")]
        public async Task<IActionResult> GetEmailTemplate(string templateName)
        {
            try
            {
                var template = await _emailService.GetEmailTemplateAsync(templateName);
                return Ok(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting email template");
                return StatusCode(500, "Error getting email template");
            }
        }

        [HttpPut("templates/{templateName}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateEmailTemplate(string templateName, [FromBody] UpdateEmailTemplateRequest request)
        {
            try
            {
                var template = await _emailService.UpdateEmailTemplateAsync(
                    templateName,
                    request.Subject,
                    request.Body,
                    request.Description);

                return Ok(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating email template");
                return StatusCode(500, "Error updating email template");
            }
        }

        [HttpDelete("templates/{templateName}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteEmailTemplate(string templateName)
        {
            try
            {
                var result = await _emailService.DeleteEmailTemplateAsync(templateName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting email template");
                return StatusCode(500, "Error deleting email template");
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

                await _emailService.SendVerificationEmailAsync(userId);
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

                var result = await _emailService.VerifyEmailAsync(userId, token);
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
                await _emailService.SendPasswordResetEmailAsync(request.Email);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset email");
                return StatusCode(500, "Error sending password reset email");
            }
        }

        [HttpPost("password-reset/{token}")]
        public async Task<IActionResult> ResetPassword(string token, [FromBody] ResetPasswordRequest request)
        {
            try
            {
                var result = await _emailService.ResetPasswordAsync(token, request.NewPassword);
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

    public class SendTemplatedEmailRequest
    {
        public required string To { get; set; }
        public required string TemplateName { get; set; }
        public Dictionary<string, string>? TemplateData { get; set; }
        public List<EmailAttachment>? Attachments { get; set; }
    }

    public class SendBulkEmailRequest
    {
        public required List<string> Recipients { get; set; }
        public required string Subject { get; set; }
        public required string Body { get; set; }
        public bool IsHtml { get; set; }
        public List<EmailAttachment>? Attachments { get; set; }
    }

    public class CreateEmailTemplateRequest
    {
        public required string Name { get; set; }
        public required string Subject { get; set; }
        public required string Body { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateEmailTemplateRequest
    {
        public required string Subject { get; set; }
        public required string Body { get; set; }
        public string? Description { get; set; }
    }

    public class SendPasswordResetEmailRequest
    {
        public required string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        public required string NewPassword { get; set; }
    }
} 