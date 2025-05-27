using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly IEmailService _emailService;

        public EmailController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        [HttpPost("test")]
        public async Task<IActionResult> TestEmail([FromBody] TestEmailRequest request)
        {
            try
            {
                await _emailService.SendEmailAsync(
                    request.To,
                    "ChitChat Test Email",
                    $@"
                    <h2>Test Email</h2>
                    <p>Bu bir test emailidir.</p>
                    <p>Gönderim zamanı: {DateTime.Now}</p>
                    ");

                return Ok(new { message = "Test email başarıyla gönderildi" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Email gönderimi başarısız: {ex.Message}" });
            }
        }
    }

    public class TestEmailRequest
    {
        public string To { get; set; }
    }
} 