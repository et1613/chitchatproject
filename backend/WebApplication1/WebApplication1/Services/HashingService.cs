using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace WebApplication1.Services
{
    public class TokenData
    {
        public required string UserId { get; set; }
        public required string Url { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public required DateTime ExpiresAt { get; set; }

        public TokenData(string userId, string url, DateTime expiresAt)
        {
            UserId = userId;
            Url = url;
            ExpiresAt = expiresAt;
        }
    }

    public class HashingOptions
    {
        public int SaltSize { get; set; } = 16; // bytes
        public int KeySize { get; set; } = 32; // bytes
        public int Iterations { get; set; } = 100000;
        public char Delimiter { get; set; } = ':';
        public HashAlgorithmName HashAlgorithm { get; set; } = HashAlgorithmName.SHA512;
        public int FileHashBufferSize { get; set; } = 8192; // 8KB buffer for file hashing
        public int TokenExpirationMinutes { get; set; } = 60;
        public bool EnableKeyRotation { get; set; } = true;
        public int KeyRotationDays { get; set; } = 30;
    }

    public class HashingService
    {
        private readonly HashingOptions _options;
        private readonly ILogger<HashingService> _logger;
        private readonly RandomNumberGenerator _rng;

        public HashingService(
            IOptions<HashingOptions> options,
            ILogger<HashingService> logger)
        {
            _options = options.Value;
            _logger = logger;
            _rng = RandomNumberGenerator.Create();
        }

        public string HashPassword(string password)
        {
            try
            {
                // Generate a random salt
                byte[] salt = new byte[_options.SaltSize];
                _rng.GetBytes(salt);

                // Hash the password with the salt
                byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                    password: Encoding.UTF8.GetBytes(password),
                    salt: salt,
                    iterations: _options.Iterations,
                    hashAlgorithm: _options.HashAlgorithm,
                    outputLength: _options.KeySize);

                // Combine the salt and hash
                return $"{Convert.ToBase64String(salt)}{_options.Delimiter}{Convert.ToBase64String(hash)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing password");
                throw new CryptographicException("Error hashing password", ex);
            }
        }

        public bool VerifyHash(string password, string hashedPassword)
        {
            try
            {
                // Split the stored hash into salt and hash
                var parts = hashedPassword.Split(_options.Delimiter);
                if (parts.Length != 2)
                {
                    _logger.LogWarning("Invalid hash format");
                    return false;
                }

                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] storedHash = Convert.FromBase64String(parts[1]);

                // Hash the provided password with the stored salt
                byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
                    password: Encoding.UTF8.GetBytes(password),
                    salt: salt,
                    iterations: _options.Iterations,
                    hashAlgorithm: _options.HashAlgorithm,
                    outputLength: _options.KeySize);

                // Compare the computed hash with the stored hash
                return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying password hash");
                return false;
            }
        }

        public string GenerateSecureToken(int length = 32)
        {
            try
            {
                byte[] tokenBytes = new byte[length];
                _rng.GetBytes(tokenBytes);
                return Convert.ToBase64String(tokenBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating secure token");
                throw new CryptographicException("Error generating secure token", ex);
            }
        }

        public string HashData(string data)
        {
            try
            {
                using var sha256 = SHA256.Create();
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(hashBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing data");
                throw new CryptographicException("Error hashing data", ex);
            }
        }

        public string GenerateSalt()
        {
            try
            {
                byte[] salt = new byte[_options.SaltSize];
                _rng.GetBytes(salt);
                return Convert.ToBase64String(salt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating salt");
                throw new CryptographicException("Error generating salt", ex);
            }
        }

        public string HashWithSalt(string data, string salt)
        {
            try
            {
                byte[] saltBytes = Convert.FromBase64String(salt);
                byte[] dataBytes = Encoding.UTF8.GetBytes(data);

                using var hmac = new HMACSHA512(saltBytes);
                byte[] hashBytes = hmac.ComputeHash(dataBytes);
                return Convert.ToBase64String(hashBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing data with salt");
                throw new CryptographicException("Error hashing data with salt", ex);
            }
        }

        public bool VerifyHashWithSalt(string data, string salt, string hashedData)
        {
            try
            {
                string computedHash = HashWithSalt(data, salt);
                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(computedHash),
                    Encoding.UTF8.GetBytes(hashedData));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying hash with salt");
                return false;
            }
        }

        public string GeneratePasswordResetToken()
        {
            try
            {
                byte[] tokenBytes = new byte[32];
                _rng.GetBytes(tokenBytes);
                return Convert.ToBase64String(tokenBytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating password reset token");
                throw new CryptographicException("Error generating password reset token", ex);
            }
        }

        public string GenerateEmailVerificationToken()
        {
            try
            {
                byte[] tokenBytes = new byte[24];
                _rng.GetBytes(tokenBytes);
                return Convert.ToBase64String(tokenBytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating email verification token");
                throw new CryptographicException("Error generating email verification token", ex);
            }
        }

        public string GenerateSessionToken()
        {
            try
            {
                byte[] tokenBytes = new byte[48];
                _rng.GetBytes(tokenBytes);
                return Convert.ToBase64String(tokenBytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating session token");
                throw new CryptographicException("Error generating session token", ex);
            }
        }

        public string GenerateApiKey()
        {
            try
            {
                byte[] keyBytes = new byte[32];
                _rng.GetBytes(keyBytes);
                return Convert.ToBase64String(keyBytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating API key");
                throw new CryptographicException("Error generating API key", ex);
            }
        }

        public string GenerateSecureRandomString(int length)
        {
            try
            {
                const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+-=[]{}|;:,.<>?";
                byte[] randomBytes = new byte[length];
                _rng.GetBytes(randomBytes);

                var result = new StringBuilder(length);
                for (int i = 0; i < length; i++)
                {
                    result.Append(chars[randomBytes[i] % chars.Length]);
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating secure random string");
                throw new CryptographicException("Error generating secure random string", ex);
            }
        }

        public async Task<string> HashFileAsync(Stream fileStream)
        {
            try
            {
                using var sha256 = SHA256.Create();
                byte[] buffer = new byte[_options.FileHashBufferSize];
                int bytesRead;

                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                }

                sha256.TransformFinalBlock(buffer, 0, 0);
                var hash = sha256.Hash;
                if (hash == null)
                    throw new CryptographicException("Failed to compute hash");

                return Convert.ToBase64String(hash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing file");
                throw new CryptographicException("Error hashing file", ex);
            }
        }

        public string GenerateTemporaryAccessToken(string userId, TimeSpan? expiration = null)
        {
            try
            {
                var tokenData = new
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.Add(expiration ?? TimeSpan.FromMinutes(_options.TokenExpirationMinutes))
                };

                var tokenJson = System.Text.Json.JsonSerializer.Serialize(tokenData);
                var tokenBytes = Encoding.UTF8.GetBytes(tokenJson);
                
                using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_options.KeySize.ToString()));
                var signature = hmac.ComputeHash(tokenBytes);

                return $"{Convert.ToBase64String(tokenBytes)}.{Convert.ToBase64String(signature)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating temporary access token");
                throw new CryptographicException("Error generating temporary access token", ex);
            }
        }

        public bool ValidateTemporaryAccessToken(string token, out string userId)
        {
            userId = string.Empty; // Default value, will be set if token is valid

            try
            {
                var parts = token.Split('.');
                if (parts.Length != 2)
                {
                    return false;
                }

                var tokenBytes = Convert.FromBase64String(parts[0]);
                var signature = Convert.FromBase64String(parts[1]);

                using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_options.KeySize.ToString()));
                var computedSignature = hmac.ComputeHash(tokenBytes);

                if (!CryptographicOperations.FixedTimeEquals(signature, computedSignature))
                {
                    return false;
                }

                var tokenData = System.Text.Json.JsonSerializer.Deserialize<TokenData>(
                    Encoding.UTF8.GetString(tokenBytes));

                if (tokenData == null)
                {
                    return false;
                }

                if (tokenData.ExpiresAt < DateTime.UtcNow)
                {
                    return false;
                }

                userId = tokenData.UserId;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating temporary access token");
                return false;
            }
        }

        public string GenerateSecurePassword(int length = 16)
        {
            try
            {
                const string lowercase = "abcdefghijklmnopqrstuvwxyz";
                const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                const string numbers = "0123456789";
                const string special = "!@#$%^&*()_+-=[]{}|;:,.<>?";

                var allChars = lowercase + uppercase + numbers + special;
                var password = new StringBuilder(length);

                // Ensure at least one character from each category
                password.Append(lowercase[RandomNumberGenerator.GetInt32(lowercase.Length)]);
                password.Append(uppercase[RandomNumberGenerator.GetInt32(uppercase.Length)]);
                password.Append(numbers[RandomNumberGenerator.GetInt32(numbers.Length)]);
                password.Append(special[RandomNumberGenerator.GetInt32(special.Length)]);

                // Fill the rest with random characters
                for (int i = 4; i < length; i++)
                {
                    password.Append(allChars[RandomNumberGenerator.GetInt32(allChars.Length)]);
                }

                // Shuffle the password
                var passwordArray = password.ToString().ToCharArray();
                for (int i = passwordArray.Length - 1; i > 0; i--)
                {
                    int j = RandomNumberGenerator.GetInt32(i + 1);
                    (passwordArray[i], passwordArray[j]) = (passwordArray[j], passwordArray[i]);
                }

                return new string(passwordArray);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating secure password");
                throw new CryptographicException("Error generating secure password", ex);
            }
        }

        public string GenerateSecureFileName(string originalFileName)
        {
            try
            {
                var extension = Path.GetExtension(originalFileName);
                var timestamp = DateTime.UtcNow.Ticks;
                var randomBytes = new byte[8];
                _rng.GetBytes(randomBytes);
                var randomPart = Convert.ToBase64String(randomBytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", "");

                return $"{timestamp}-{randomPart}{extension}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating secure file name");
                throw new CryptographicException("Error generating secure file name", ex);
            }
        }

        public string GenerateSecureUrlToken(string url, TimeSpan? expiration = null)
        {
            try
            {
                var tokenData = new
                {
                    Url = url,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.Add(expiration ?? TimeSpan.FromMinutes(_options.TokenExpirationMinutes))
                };

                var tokenJson = System.Text.Json.JsonSerializer.Serialize(tokenData);
                var tokenBytes = Encoding.UTF8.GetBytes(tokenJson);
                
                using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_options.KeySize.ToString()));
                var signature = hmac.ComputeHash(tokenBytes);

                return $"{Convert.ToBase64String(tokenBytes)}.{Convert.ToBase64String(signature)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating secure URL token");
                throw new CryptographicException("Error generating secure URL token", ex);
            }
        }

        public bool ValidateSecureUrlToken(string token, string url)
        {
            try
            {
                var parts = token.Split('.');
                if (parts.Length != 2)
                    return false;

                var tokenBytes = Convert.FromBase64String(parts[0]);
                var signature = Convert.FromBase64String(parts[1]);

                using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_options.KeySize.ToString()));
                var computedSignature = hmac.ComputeHash(tokenBytes);

                if (!CryptographicOperations.FixedTimeEquals(signature, computedSignature))
                    return false;

                var tokenData = System.Text.Json.JsonSerializer.Deserialize<TokenData>(
                    Encoding.UTF8.GetString(tokenBytes));

                if (tokenData == null)
                    return false;

                if (tokenData.ExpiresAt < DateTime.UtcNow)
                    return false;

                return tokenData.Url == url;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating secure URL token");
                return false;
            }
        }
    }
} 