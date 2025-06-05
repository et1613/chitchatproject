using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApplication1.Services;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using WebApplication1.Models.DigitalSignature;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DigitalSignatureController : ControllerBase
    {
        private readonly IDigitalSignatureService _signatureService;
        private readonly ILogger<DigitalSignatureController> _logger;

        public DigitalSignatureController(
            IDigitalSignatureService signatureService,
            ILogger<DigitalSignatureController> logger)
        {
            _signatureService = signatureService;
            _logger = logger;
        }

        [HttpPost("sign")]
        public async Task<IActionResult> SignData([FromBody] SignDataRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var signature = await _signatureService.SignDataAsync(
                    request.Data,
                    request.CertificateId,
                    request.SignatureAlgorithm,
                    request.HashAlgorithm);

                return Ok(signature);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error signing data");
                return StatusCode(500, "Error signing data");
            }
        }

        [HttpPost("verify")]
        public async Task<IActionResult> VerifySignature([FromBody] VerifySignatureRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _signatureService.VerifySignatureAsync(
                    request.Data,
                    request.Signature,
                    request.CertificateId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying signature");
                return StatusCode(500, "Error verifying signature");
            }
        }

        [HttpPost("certificates")]
        public async Task<IActionResult> CreateCertificate([FromBody] CreateCertificateRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var certificate = await _signatureService.CreateCertificateAsync(
                    request.SubjectName,
                    request.ValidFrom,
                    request.ValidTo,
                    request.KeySize,
                    request.Password);

                return Ok(certificate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating certificate");
                return StatusCode(500, "Error creating certificate");
            }
        }

        [HttpGet("certificates")]
        public async Task<IActionResult> GetUserCertificates()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var certificates = await _signatureService.GetUserCertificatesAsync(userId);
                return Ok(certificates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user certificates");
                return StatusCode(500, "Error getting user certificates");
            }
        }

        [HttpGet("certificates/{certificateId}")]
        public async Task<IActionResult> GetCertificate(string certificateId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var certificate = await _signatureService.GetCertificateAsync(certificateId);
                return Ok(certificate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting certificate");
                return StatusCode(500, "Error getting certificate");
            }
        }

        [HttpDelete("certificates/{certificateId}")]
        public async Task<IActionResult> RevokeCertificate(string certificateId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _signatureService.RevokeCertificateAsync(certificateId, userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking certificate");
                return StatusCode(500, "Error revoking certificate");
            }
        }

        [HttpPost("certificates/{certificateId}/export")]
        public async Task<IActionResult> ExportCertificate(string certificateId, [FromBody] ExportCertificateRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var certificateData = await _signatureService.ExportCertificateAsync(
                    certificateId,
                    request.Format,
                    request.IncludePrivateKey,
                    request.Password);

                return Ok(certificateData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting certificate");
                return StatusCode(500, "Error exporting certificate");
            }
        }

        [HttpPost("certificates/import")]
        public async Task<IActionResult> ImportCertificate([FromBody] ImportCertificateRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var certificate = await _signatureService.ImportCertificateAsync(
                    request.CertificateData,
                    request.Password,
                    request.Format);

                return Ok(certificate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing certificate");
                return StatusCode(500, "Error importing certificate");
            }
        }

        [HttpPost("certificates/{certificateId}/renew")]
        public async Task<IActionResult> RenewCertificate(string certificateId, [FromBody] RenewCertificateRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var certificate = await _signatureService.RenewCertificateAsync(
                    certificateId,
                    request.ValidTo,
                    request.Password);

                return Ok(certificate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renewing certificate");
                return StatusCode(500, "Error renewing certificate");
            }
        }

        [HttpGet("certificates/{certificateId}/status")]
        public async Task<IActionResult> GetCertificateStatus(string certificateId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var status = await _signatureService.GetCertificateStatusAsync(certificateId);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting certificate status");
                return StatusCode(500, "Error getting certificate status");
            }
        }
    }

    public class SignDataRequest
    {
        public required string Data { get; set; }
        public required string CertificateId { get; set; }
        public string? SignatureAlgorithm { get; set; }
        public string? HashAlgorithm { get; set; }
    }

    public class VerifySignatureRequest
    {
        public required string Data { get; set; }
        public required string Signature { get; set; }
        public required string CertificateId { get; set; }
    }

    public class CreateCertificateRequest
    {
        public required string SubjectName { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public int KeySize { get; set; } = 2048;
        public string? Password { get; set; }
    }

    public class ExportCertificateRequest
    {
        public required string Format { get; set; }
        public bool IncludePrivateKey { get; set; }
        public string? Password { get; set; }
    }

    public class ImportCertificateRequest
    {
        public required string CertificateData { get; set; }
        public string? Password { get; set; }
        public required string Format { get; set; }
    }

    public class RenewCertificateRequest
    {
        public DateTime ValidTo { get; set; }
        public string? Password { get; set; }
    }
} 