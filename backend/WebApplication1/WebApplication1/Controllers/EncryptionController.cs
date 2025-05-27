using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;
using System.Threading.Tasks;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EncryptionController : ControllerBase
    {
        private readonly EncryptionService _encryptionService;
        private readonly ILogger<EncryptionController> _logger;

        public EncryptionController(EncryptionService encryptionService, ILogger<EncryptionController> logger)
        {
            _encryptionService = encryptionService;
            _logger = logger;
        }

        [HttpPost("rsa/generate")]
        public IActionResult GenerateRsaKeyPair([FromQuery] string keyId = null)
        {
            try
            {
                var (publicKey, privateKey) = _encryptionService.GenerateRsaKeyPair(keyId: keyId);
                return Ok(new { publicKey, privateKey });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating RSA key pair");
                return StatusCode(500, "Error generating RSA key pair");
            }
        }

        [HttpPost("ecdsa/generate")]
        public IActionResult GenerateEcdsaKeyPair([FromQuery] string keyId = null)
        {
            try
            {
                var (publicKey, privateKey) = _encryptionService.GenerateEcdsaKeyPair(keyId: keyId);
                return Ok(new { publicKey, privateKey });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating ECDSA key pair");
                return StatusCode(500, "Error generating ECDSA key pair");
            }
        }

        [HttpPost("aes/generate")]
        public IActionResult GenerateAesKey([FromQuery] string keyId = null)
        {
            try
            {
                var (key, iv) = _encryptionService.GenerateAesKey(keyId: keyId);
                return Ok(new { key, iv });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AES key");
                return StatusCode(500, "Error generating AES key");
            }
        }

        [HttpPost("rsa/encrypt")]
        public IActionResult EncryptWithRsa([FromBody] RsaEncryptRequest request)
        {
            try
            {
                var encrypted = _encryptionService.EncryptWithRsa(request.Content, request.PublicKey);
                return Ok(new { encrypted });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting with RSA");
                return StatusCode(500, "Error encrypting with RSA");
            }
        }

        [HttpPost("rsa/decrypt")]
        public IActionResult DecryptWithRsa([FromBody] RsaDecryptRequest request)
        {
            try
            {
                var decrypted = _encryptionService.DecryptWithRsa(request.EncryptedContent, request.PrivateKey);
                return Ok(new { decrypted });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting with RSA");
                return StatusCode(500, "Error decrypting with RSA");
            }
        }

        [HttpPost("ecdsa/sign")]
        public IActionResult SignWithEcdsa([FromBody] EcdsaSignRequest request)
        {
            try
            {
                var signature = _encryptionService.SignWithEcdsa(request.Content, request.PrivateKey);
                return Ok(new { signature });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error signing with ECDSA");
                return StatusCode(500, "Error signing with ECDSA");
            }
        }

        [HttpPost("ecdsa/verify")]
        public IActionResult VerifyWithEcdsa([FromBody] EcdsaVerifyRequest request)
        {
            try
            {
                var isValid = _encryptionService.VerifyWithEcdsa(request.Content, request.Signature, request.PublicKey);
                return Ok(new { isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying with ECDSA");
                return StatusCode(500, "Error verifying with ECDSA");
            }
        }

        [HttpPost("aes/encrypt")]
        public IActionResult EncryptWithAes([FromBody] AesEncryptRequest request)
        {
            try
            {
                var encrypted = _encryptionService.EncryptWithAes(request.Content, request.Key, request.Iv);
                return Ok(new { encrypted });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting with AES");
                return StatusCode(500, "Error encrypting with AES");
            }
        }

        [HttpPost("aes/decrypt")]
        public IActionResult DecryptWithAes([FromBody] AesDecryptRequest request)
        {
            try
            {
                var decrypted = _encryptionService.DecryptWithAes(request.EncryptedContent, request.Key, request.Iv);
                return Ok(new { decrypted });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting with AES");
                return StatusCode(500, "Error decrypting with AES");
            }
        }

        [HttpPost("aes-gcm/encrypt")]
        public IActionResult EncryptWithAesGcm([FromBody] AesGcmEncryptRequest request)
        {
            try
            {
                var (cipherText, tag, nonce) = _encryptionService.EncryptWithAesGcm(request.Content, request.Key);
                return Ok(new { cipherText, tag, nonce });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting with AES-GCM");
                return StatusCode(500, "Error encrypting with AES-GCM");
            }
        }

        [HttpPost("aes-gcm/decrypt")]
        public IActionResult DecryptWithAesGcm([FromBody] AesGcmDecryptRequest request)
        {
            try
            {
                var decrypted = _encryptionService.DecryptWithAesGcm(request.CipherText, request.Tag, request.Nonce, request.Key);
                return Ok(new { decrypted });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting with AES-GCM");
                return StatusCode(500, "Error decrypting with AES-GCM");
            }
        }

        [HttpPost("hmac")]
        public IActionResult ComputeHmac([FromBody] HmacRequest request)
        {
            try
            {
                var hmac = _encryptionService.ComputeHmacSha256(request.Content, request.Key);
                return Ok(new { hmac });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing HMAC");
                return StatusCode(500, "Error computing HMAC");
            }
        }
    }

    public class RsaEncryptRequest
    {
        public string Content { get; set; }
        public string PublicKey { get; set; }
    }

    public class RsaDecryptRequest
    {
        public string EncryptedContent { get; set; }
        public string PrivateKey { get; set; }
    }

    public class EcdsaSignRequest
    {
        public string Content { get; set; }
        public string PrivateKey { get; set; }
    }

    public class EcdsaVerifyRequest
    {
        public string Content { get; set; }
        public string Signature { get; set; }
        public string PublicKey { get; set; }
    }

    public class AesEncryptRequest
    {
        public string Content { get; set; }
        public string Key { get; set; }
        public string Iv { get; set; }
    }

    public class AesDecryptRequest
    {
        public string EncryptedContent { get; set; }
        public string Key { get; set; }
        public string Iv { get; set; }
    }

    public class AesGcmEncryptRequest
    {
        public string Content { get; set; }
        public string Key { get; set; }
    }

    public class AesGcmDecryptRequest
    {
        public string CipherText { get; set; }
        public string Tag { get; set; }
        public string Nonce { get; set; }
        public string Key { get; set; }
    }

    public class HmacRequest
    {
        public string Content { get; set; }
        public string Key { get; set; }
    }
} 