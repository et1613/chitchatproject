using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApplication1.Services;
using System.Collections.Generic;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DigitalSignatureController : ControllerBase
    {
        private readonly ILogger<DigitalSignatureController> _logger;
        private readonly DigitalSignatureService _signatureService;

        public DigitalSignatureController(
            ILogger<DigitalSignatureController> logger,
            DigitalSignatureService signatureService)
        {
            _logger = logger;
            _signatureService = signatureService;
        }

        [HttpPost("sign")]
        public async Task<ActionResult<SignatureResult>> SignMessage([FromBody] SignMessageRequest request)
        {
            try
            {
                var result = await _signatureService.SignMessageAsync(
                    request.Content,
                    request.PrivateKey,
                    request.Algorithm,
                    request.Metadata
                );
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error signing message");
                return StatusCode(500, new { error = "Failed to sign message" });
            }
        }

        [HttpPost("sign/batch")]
        public async Task<ActionResult<BatchSignatureResult>> SignBatch([FromBody] List<BatchSignatureRequest> requests)
        {
            try
            {
                var result = await _signatureService.SignBatchAsync(requests);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error signing batch");
                return StatusCode(500, new { error = "Failed to sign batch" });
            }
        }

        [HttpPost("verify")]
        public async Task<ActionResult<bool>> VerifySignature([FromBody] VerifySignatureRequest request)
        {
            try
            {
                var isValid = await _signatureService.VerifySignatureAsync(
                    request.Content,
                    request.Signature,
                    request.PublicKey
                );
                return Ok(new { isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying signature");
                return StatusCode(500, new { error = "Failed to verify signature" });
            }
        }

        [HttpGet("metrics")]
        public async Task<ActionResult<SignatureMetrics>> GetMetrics()
        {
            try
            {
                var metrics = await _signatureService.GetMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting metrics");
                return StatusCode(500, new { error = "Failed to get metrics" });
            }
        }
    }

    public class SignMessageRequest
    {
        public string Content { get; set; }
        public string PrivateKey { get; set; }
        public string Algorithm { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
    }

    public class VerifySignatureRequest
    {
        public string Content { get; set; }
        public SignatureResult Signature { get; set; }
        public string PublicKey { get; set; }
    }
} 