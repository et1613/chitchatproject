using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace WebApplication1.Services
{
    // Custom exception for encryption errors
    public class EncryptionException : Exception
    {
        public EncryptionException(string message) : base(message) { }
        public EncryptionException(string message, Exception inner) : base(message, inner) { }
    }

    public class EncryptionService : IEncryptionService
    {
        private readonly ILogger<EncryptionService> _logger;
        private readonly IConfiguration _configuration;
        private readonly byte[] _key;
        private readonly byte[] _iv;
        private const int KeySize = 32; // 256 bits
        private const int IvSize = 16;  // 128 bits

        public EncryptionService(ILogger<EncryptionService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // Anahtar ve IV'yi configuration'dan al
            var keyString = _configuration["Encryption:Key"];
            var ivString = _configuration["Encryption:IV"];

            if (string.IsNullOrEmpty(keyString) || string.IsNullOrEmpty(ivString))
            {
                // Eğer configuration'da yoksa yeni oluştur
                var (key, iv) = GenerateSecureKeys();
                _key = Convert.FromBase64String(key);
                _iv = Convert.FromBase64String(iv);

                // TODO: Bu değerleri güvenli bir şekilde sakla
                _logger.LogWarning("Encryption key and IV were not found in configuration. New values were generated.");
            }
            else
            {
                try
                {
                    _key = Convert.FromBase64String(keyString);
                    _iv = Convert.FromBase64String(ivString);
                }
                catch (FormatException)
                {
                    throw new EncryptionException("Invalid key or IV format in configuration");
                }
            }
        }

        public (string key, string iv) GenerateSecureKeys()
        {
            using var aes = Aes.Create();
            aes.KeySize = KeySize * 8; // bits
            aes.GenerateKey();
            aes.GenerateIV();
            return (Convert.ToBase64String(aes.Key), Convert.ToBase64String(aes.IV));
        }

        public async Task<string> EncryptStringAsync(string plainText)
        {
            try
            {
                if (string.IsNullOrEmpty(plainText))
                    throw new ArgumentNullException(nameof(plainText));

                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;

                using var encryptor = aes.CreateEncryptor();
                using var msEncrypt = new MemoryStream();
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                using (var swEncrypt = new StreamWriter(csEncrypt))
                {
                    await swEncrypt.WriteAsync(plainText);
                }

                return Convert.ToBase64String(msEncrypt.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "String encryption failed");
                throw new EncryptionException("String encryption failed", ex);
            }
        }

        public async Task<string> DecryptStringAsync(string cipherText)
        {
            try
            {
                if (string.IsNullOrEmpty(cipherText))
                    throw new ArgumentNullException(nameof(cipherText));

                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;

                using var decryptor = aes.CreateDecryptor();
                using var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText));
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);

                return await srDecrypt.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "String decryption failed");
                throw new EncryptionException("String decryption failed", ex);
            }
        }

        public async Task<byte[]> EncryptFileAsync(Stream inputStream)
        {
            try
            {
                if (inputStream == null)
                    throw new ArgumentNullException(nameof(inputStream));

                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;

                using var encryptor = aes.CreateEncryptor();
                using var msEncrypt = new MemoryStream();
                using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);

                await inputStream.CopyToAsync(csEncrypt);
                await csEncrypt.FlushFinalBlockAsync();

                return msEncrypt.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File encryption failed");
                throw new EncryptionException("File encryption failed", ex);
            }
        }

        public async Task<byte[]> DecryptFileAsync(Stream inputStream)
        {
            try
            {
                if (inputStream == null)
                    throw new ArgumentNullException(nameof(inputStream));

                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;

                using var decryptor = aes.CreateDecryptor();
                using var msDecrypt = new MemoryStream();
                using var csDecrypt = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);

                await csDecrypt.CopyToAsync(msDecrypt);
                await csDecrypt.FlushFinalBlockAsync();

                return msDecrypt.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File decryption failed");
                throw new EncryptionException("File decryption failed", ex);
            }
        }

        public async Task<string> HashStringAsync(string input)
        {
            try
            {
                if (string.IsNullOrEmpty(input))
                    throw new ArgumentNullException(nameof(input));

                using var sha256 = SHA256.Create();
                var hashBytes = await Task.Run(() => sha256.ComputeHash(Encoding.UTF8.GetBytes(input)));
                return Convert.ToBase64String(hashBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "String hashing failed");
                throw new EncryptionException("String hashing failed", ex);
            }
        }

        public async Task<bool> VerifyHashAsync(string input, string hash)
        {
            try
            {
                if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(hash))
                    throw new ArgumentException("Input and hash cannot be null or empty");

                var computedHash = await HashStringAsync(input);
                return computedHash.Equals(hash, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hash verification failed");
                throw new EncryptionException("Hash verification failed", ex);
            }
        }

        public void EncryptFileWithAes(Stream inputStream, Stream outputStream, string key, string iv)
        {
            try
            {
                if (inputStream == null)
                    throw new ArgumentNullException(nameof(inputStream));
                if (outputStream == null)
                    throw new ArgumentNullException(nameof(outputStream));
                if (string.IsNullOrEmpty(key))
                    throw new ArgumentNullException(nameof(key));
                if (string.IsNullOrEmpty(iv))
                    throw new ArgumentNullException(nameof(iv));

                using var aes = Aes.Create();
                aes.Key = Convert.FromBase64String(key);
                aes.IV = Convert.FromBase64String(iv);

                using var encryptor = aes.CreateEncryptor();
                using var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write);

                inputStream.CopyTo(cryptoStream);
                cryptoStream.FlushFinalBlock();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File encryption with custom key failed");
                throw new EncryptionException("File encryption with custom key failed", ex);
            }
        }

        public void DecryptFileWithAes(Stream inputStream, Stream outputStream, string key, string iv)
        {
            try
            {
                if (inputStream == null)
                    throw new ArgumentNullException(nameof(inputStream));
                if (outputStream == null)
                    throw new ArgumentNullException(nameof(outputStream));
                if (string.IsNullOrEmpty(key))
                    throw new ArgumentNullException(nameof(key));
                if (string.IsNullOrEmpty(iv))
                    throw new ArgumentNullException(nameof(iv));

                using var aes = Aes.Create();
                aes.Key = Convert.FromBase64String(key);
                aes.IV = Convert.FromBase64String(iv);

                using var decryptor = aes.CreateDecryptor();
                using var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);

                cryptoStream.CopyTo(outputStream);
                cryptoStream.FlushFinalBlock();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File decryption with custom key failed");
                throw new EncryptionException("File decryption with custom key failed", ex);
            }
        }
    }
} 
