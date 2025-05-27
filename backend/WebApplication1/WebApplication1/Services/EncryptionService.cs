using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Services
{
    // Custom exception for encryption errors
    public class EncryptionException : Exception
    {
        public EncryptionException(string message) : base(message) { }
        public EncryptionException(string message, Exception inner) : base(message, inner) { }
    }

    public class EncryptionService
    {
        private readonly ILogger<EncryptionService> _logger;
        private readonly Dictionary<string, (string key, string iv, DateTime createdAt)> _aesKeyStore = new();
        private readonly Dictionary<string, (string publicKey, string privateKey, DateTime createdAt)> _rsaKeyStore = new();
        private readonly Dictionary<string, (string publicKey, string privateKey, DateTime createdAt)> _ecdsaKeyStore = new();
        private readonly TimeSpan _keyRotationPeriod = TimeSpan.FromDays(30);

        public EncryptionService(ILogger<EncryptionService> logger)
        {
            _logger = logger;
        }

        // --- Key Management ---
        public (string publicKey, string privateKey) GenerateRsaKeyPair(int keySize = 2048, string keyId = null)
        {
            using var rsa = RSA.Create(keySize);
            var publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());
            var privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
            if (!string.IsNullOrEmpty(keyId))
                _rsaKeyStore[keyId] = (publicKey, privateKey, DateTime.UtcNow);
            return (publicKey, privateKey);
        }

        public (string publicKey, string privateKey) GenerateEcdsaKeyPair(string keyId = null)
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var publicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());
            var privateKey = Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey());
            if (!string.IsNullOrEmpty(keyId))
                _ecdsaKeyStore[keyId] = (publicKey, privateKey, DateTime.UtcNow);
            return (publicKey, privateKey);
        }

        public (string key, string iv) GenerateAesKey(int keySize = 256, string keyId = null)
        {
            using var aes = Aes.Create();
            aes.KeySize = keySize;
            aes.GenerateKey();
            aes.GenerateIV();
            if (!string.IsNullOrEmpty(keyId))
                _aesKeyStore[keyId] = (Convert.ToBase64String(aes.Key), Convert.ToBase64String(aes.IV), DateTime.UtcNow);
            return (Convert.ToBase64String(aes.Key), Convert.ToBase64String(aes.IV));
        }

        public void RotateAesKey(string keyId, int keySize = 256)
        {
            var (key, iv) = GenerateAesKey(keySize);
            _aesKeyStore[keyId] = (key, iv, DateTime.UtcNow);
            _logger.LogInformation($"AES key rotated for {keyId}");
        }

        public void RotateRsaKey(string keyId, int keySize = 2048)
        {
            var (pub, priv) = GenerateRsaKeyPair(keySize);
            _rsaKeyStore[keyId] = (pub, priv, DateTime.UtcNow);
            _logger.LogInformation($"RSA key rotated for {keyId}");
        }

        public void RotateEcdsaKey(string keyId)
        {
            var (pub, priv) = GenerateEcdsaKeyPair();
            _ecdsaKeyStore[keyId] = (pub, priv, DateTime.UtcNow);
            _logger.LogInformation($"ECDSA key rotated for {keyId}");
        }

        public (string key, string iv) GetAesKey(string keyId)
        {
            if (_aesKeyStore.TryGetValue(keyId, out var tuple))
                return (tuple.key, tuple.iv);
            throw new EncryptionException($"AES key not found for {keyId}");
        }
        public (string publicKey, string privateKey) GetRsaKey(string keyId)
        {
            if (_rsaKeyStore.TryGetValue(keyId, out var tuple))
                return (tuple.publicKey, tuple.privateKey);
            throw new EncryptionException($"RSA key not found for {keyId}");
        }
        public (string publicKey, string privateKey) GetEcdsaKey(string keyId)
        {
            if (_ecdsaKeyStore.TryGetValue(keyId, out var tuple))
                return (tuple.publicKey, tuple.privateKey);
            throw new EncryptionException($"ECDSA key not found for {keyId}");
        }

        // --- RSA ---
        public string EncryptWithRsa(string content, string publicKey)
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKey), out _);
                var data = Encoding.UTF8.GetBytes(content);
                var encrypted = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RSA encryption failed");
                throw new EncryptionException("RSA encryption failed", ex);
            }
        }
        public string DecryptWithRsa(string encryptedContent, string privateKey)
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKey), out _);
                var data = Convert.FromBase64String(encryptedContent);
                var decrypted = rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA256);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RSA decryption failed");
                throw new EncryptionException("RSA decryption failed", ex);
            }
        }

        // --- ECDSA (Elliptic Curve Digital Signature Algorithm) ---
        public string SignWithEcdsa(string content, string privateKey)
        {
            try
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out _);
                var data = Encoding.UTF8.GetBytes(content);
                var signature = ecdsa.SignData(data, HashAlgorithmName.SHA256);
                return Convert.ToBase64String(signature);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ECDSA signing failed");
                throw new EncryptionException("ECDSA signing failed", ex);
            }
        }
        public bool VerifyWithEcdsa(string content, string signature, string publicKey)
        {
            try
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
                var data = Encoding.UTF8.GetBytes(content);
                var sigBytes = Convert.FromBase64String(signature);
                return ecdsa.VerifyData(data, sigBytes, HashAlgorithmName.SHA256);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ECDSA verification failed");
                throw new EncryptionException("ECDSA verification failed", ex);
            }
        }

        // --- AES (CBC) ---
        public string EncryptWithAes(string content, string key, string iv)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = Convert.FromBase64String(key);
                aes.IV = Convert.FromBase64String(iv);
                using var encryptor = aes.CreateEncryptor();
                var data = Encoding.UTF8.GetBytes(content);
                var encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AES encryption failed");
                throw new EncryptionException("AES encryption failed", ex);
            }
        }
        public string DecryptWithAes(string encryptedContent, string key, string iv)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = Convert.FromBase64String(key);
                aes.IV = Convert.FromBase64String(iv);
                using var decryptor = aes.CreateDecryptor();
                var data = Convert.FromBase64String(encryptedContent);
                var decrypted = decryptor.TransformFinalBlock(data, 0, data.Length);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AES decryption failed");
                throw new EncryptionException("AES decryption failed", ex);
            }
        }

        // --- AES-GCM (Authenticated Encryption) ---
        public (string cipherText, string tag, string nonce) EncryptWithAesGcm(string content, string key)
        {
            try
            {
                var keyBytes = Convert.FromBase64String(key);
                var nonce = new byte[12];
                RandomNumberGenerator.Fill(nonce);
                var aesGcm = new AesGcm(keyBytes);
                var data = Encoding.UTF8.GetBytes(content);
                var cipher = new byte[data.Length];
                var tag = new byte[16];
                aesGcm.Encrypt(nonce, data, cipher, tag);
                return (Convert.ToBase64String(cipher), Convert.ToBase64String(tag), Convert.ToBase64String(nonce));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AES-GCM encryption failed");
                throw new EncryptionException("AES-GCM encryption failed", ex);
            }
        }
        public string DecryptWithAesGcm(string cipherText, string tag, string nonce, string key)
        {
            try
            {
                var keyBytes = Convert.FromBase64String(key);
                var nonceBytes = Convert.FromBase64String(nonce);
                var tagBytes = Convert.FromBase64String(tag);
                var cipherBytes = Convert.FromBase64String(cipherText);
                var aesGcm = new AesGcm(keyBytes);
                var plain = new byte[cipherBytes.Length];
                aesGcm.Decrypt(nonceBytes, cipherBytes, tagBytes, plain);
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AES-GCM decryption failed");
                throw new EncryptionException("AES-GCM decryption failed", ex);
            }
        }

        // --- HMAC ---
        public string ComputeHmacSha256(string content, string key)
        {
            using var hmac = new HMACSHA256(Convert.FromBase64String(key));
            var data = Encoding.UTF8.GetBytes(content);
            var hash = hmac.ComputeHash(data);
            return Convert.ToBase64String(hash);
        }

        // --- File Encryption/Decryption (AES) ---
        public void EncryptFileWithAes(string inputFile, string outputFile, string key, string iv)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = Convert.FromBase64String(key);
                aes.IV = Convert.FromBase64String(iv);
                using var encryptor = aes.CreateEncryptor();
                using var inStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
                using var outStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
                using var cryptoStream = new CryptoStream(outStream, encryptor, CryptoStreamMode.Write);
                inStream.CopyTo(cryptoStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File AES encryption failed");
                throw new EncryptionException("File AES encryption failed", ex);
            }
        }
        public void DecryptFileWithAes(string inputFile, string outputFile, string key, string iv)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = Convert.FromBase64String(key);
                aes.IV = Convert.FromBase64String(iv);
                using var decryptor = aes.CreateDecryptor();
                using var inStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
                using var outStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
                using var cryptoStream = new CryptoStream(inStream, decryptor, CryptoStreamMode.Read);
                cryptoStream.CopyTo(outStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File AES decryption failed");
                throw new EncryptionException("File AES decryption failed", ex);
            }
        }

        // --- Batch Operations ---
        public List<string> BatchEncryptWithAes(IEnumerable<string> contents, string key, string iv)
        {
            var result = new List<string>();
            foreach (var content in contents)
            {
                result.Add(EncryptWithAes(content, key, iv));
            }
            return result;
        }
        public List<string> BatchDecryptWithAes(IEnumerable<string> encryptedContents, string key, string iv)
        {
            var result = new List<string>();
            foreach (var enc in encryptedContents)
            {
                result.Add(DecryptWithAes(enc, key, iv));
            }
            return result;
        }

        // --- Async Examples ---
        public async Task<string> EncryptWithAesAsync(string content, string key, string iv)
        {
            return await Task.Run(() => EncryptWithAes(content, key, iv));
        }
        public async Task<string> DecryptWithAesAsync(string encryptedContent, string key, string iv)
        {
            return await Task.Run(() => DecryptWithAes(encryptedContent, key, iv));
        }

        // --- Test Helpers ---
        public (string publicKey, string privateKey) GetOrCreateRsaKey(string keyId)
        {
            if (_rsaKeyStore.ContainsKey(keyId))
                return (_rsaKeyStore[keyId].publicKey, _rsaKeyStore[keyId].privateKey);
            return GenerateRsaKeyPair(2048, keyId);
        }
        public (string publicKey, string privateKey) GetOrCreateEcdsaKey(string keyId)
        {
            if (_ecdsaKeyStore.ContainsKey(keyId))
                return (_ecdsaKeyStore[keyId].publicKey, _ecdsaKeyStore[keyId].privateKey);
            return GenerateEcdsaKeyPair(keyId);
        }
        public (string key, string iv) GetOrCreateAesKey(string keyId)
        {
            if (_aesKeyStore.ContainsKey(keyId))
                return (_aesKeyStore[keyId].key, _aesKeyStore[keyId].iv);
            return GenerateAesKey(256, keyId);
        }
    }
} 