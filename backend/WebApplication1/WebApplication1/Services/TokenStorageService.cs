using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebApplication1.Data;
using System.Linq;
using System.Collections.Generic;

namespace WebApplication1.Services
{
    public class TokenStorageService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TokenStorageService> _logger;
        private readonly HashingService _hashingService;

        public TokenStorageService(
            ApplicationDbContext context,
            ILogger<TokenStorageService> logger,
            HashingService hashingService)
        {
            _context = context;
            _logger = logger;
            _hashingService = hashingService;
        }

        public async Task<string> StoreTokenAsync(string userId, string tokenType, string token, DateTime? expiration = null)
        {
            try
            {
                var tokenEntity = new StoredToken
                {
                    UserId = userId,
                    TokenType = tokenType,
                    Token = token,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiration ?? DateTime.UtcNow.AddDays(30),
                    IsRevoked = false
                };

                _context.StoredTokens.Add(tokenEntity);
                await _context.SaveChangesAsync();

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing token for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> ValidateTokenAsync(string token, string tokenType)
        {
            try
            {
                var storedToken = await _context.StoredTokens
                    .FirstOrDefaultAsync(t => t.Token == token && t.TokenType == tokenType);

                if (storedToken == null)
                    return false;

                if (storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return false;
            }
        }

        public async Task RevokeTokenAsync(string token)
        {
            try
            {
                var storedToken = await _context.StoredTokens
                    .FirstOrDefaultAsync(t => t.Token == token);

                if (storedToken != null)
                {
                    storedToken.IsRevoked = true;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                throw;
            }
        }

        public async Task RevokeAllUserTokensAsync(string userId, string tokenType = null)
        {
            try
            {
                var query = _context.StoredTokens.Where(t => t.UserId == userId);
                
                if (!string.IsNullOrEmpty(tokenType))
                    query = query.Where(t => t.TokenType == tokenType);

                var tokens = await query.ToListAsync();
                foreach (var token in tokens)
                {
                    token.IsRevoked = true;
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all tokens for user {UserId}", userId);
                throw;
            }
        }

        public async Task<string> GenerateAndStoreAccessTokenAsync(string userId, TimeSpan? expiration = null)
        {
            try
            {
                var token = _hashingService.GenerateTemporaryAccessToken(userId, expiration);
                await StoreTokenAsync(userId, "AccessToken", token, DateTime.UtcNow.Add(expiration ?? TimeSpan.FromDays(30)));
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating and storing access token for user {UserId}", userId);
                throw;
            }
        }

        public async Task<string> GenerateAndStoreUrlTokenAsync(string url, TimeSpan? expiration = null)
        {
            try
            {
                var token = _hashingService.GenerateSecureUrlToken(url, expiration);
                await StoreTokenAsync("system", "UrlToken", token, DateTime.UtcNow.Add(expiration ?? TimeSpan.FromDays(7)));
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating and storing URL token");
                throw;
            }
        }

        public async Task CleanupExpiredTokensAsync()
        {
            try
            {
                var expiredTokens = await _context.StoredTokens
                    .Where(t => t.ExpiresAt < DateTime.UtcNow || t.IsRevoked)
                    .ToListAsync();

                _context.StoredTokens.RemoveRange(expiredTokens);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired tokens");
                throw;
            }
        }

        public async Task<IEnumerable<StoredToken>> GetUserTokensAsync(string userId, string tokenType = null)
        {
            try
            {
                var query = _context.StoredTokens.Where(t => t.UserId == userId);
                
                if (!string.IsNullOrEmpty(tokenType))
                    query = query.Where(t => t.TokenType == tokenType);

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tokens for user {UserId}", userId);
                throw;
            }
        }
    }

    public class StoredToken
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string TokenType { get; set; }
        public string Token { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
    }
} 