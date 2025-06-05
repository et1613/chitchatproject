using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebApplication1.Data;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using WebApplication1.Models.Auth;

namespace WebApplication1.Services
{
    public class TokenMetadata
    {
        public required string IpAddress { get; set; }
        public required string UserAgent { get; set; }
        public string? DeviceId { get; set; }
        public Dictionary<string, string>? CustomData { get; set; }
    }

    public class TokenUsageStats
    {
        public int UsageCount { get; set; }
        public DateTime LastUsedAt { get; set; }
        public string? LastUsedIp { get; set; }
        public string? LastUsedUserAgent { get; set; }
    }

    public class TokenStorageService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TokenStorageService> _logger;
        private readonly HashingService _hashingService;
        private readonly IMemoryCache _cache;
        private readonly MemoryCacheEntryOptions _cacheOptions;
        private const string TOKEN_CACHE_PREFIX = "token_";
        private const string BLACKLIST_CACHE_PREFIX = "blacklist_";
        private const int MAX_TOKEN_USAGE = 1000;
        private const int BATCH_SIZE = 100;

        public TokenStorageService(
            ApplicationDbContext context,
            ILogger<TokenStorageService> logger,
            HashingService hashingService,
            IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _hashingService = hashingService;
            _cache = cache;
            _cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(30))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1));
        }

        public async Task<string> StoreTokenAsync(string userId, string tokenType, string token, DateTime? expiration = null, TokenMetadata? metadata = null)
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
                    IsRevoked = false,
                    UsageCount = 0,
                    LastUsedAt = DateTime.UtcNow
                };

                if (metadata != null)
                {
                    tokenEntity.SetMetadata(metadata);
                }

                _context.StoredTokens.Add(tokenEntity);
                await _context.SaveChangesAsync();

                // Cache the token
                var cacheKey = $"{TOKEN_CACHE_PREFIX}{token}";
                _cache.Set(cacheKey, tokenEntity, _cacheOptions);

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing token for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> ValidateTokenAsync(string token, string tokenType, string? ipAddress = null, string? userAgent = null)
        {
            try
            {
                // Check blacklist first
                if (await IsTokenBlacklistedAsync(token))
                    return false;

                // Try to get from cache
                var cacheKey = $"{TOKEN_CACHE_PREFIX}{token}";
                if (_cache.TryGetValue(cacheKey, out StoredToken? cachedToken))
                {
                    return ValidateCachedToken(cachedToken, tokenType);
                }

                // If not in cache, check database
                var storedToken = await _context.StoredTokens
                    .FirstOrDefaultAsync(t => t.Token == token && t.TokenType == tokenType);

                if (storedToken == null)
                    return false;

                if (storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
                {
                    await AddToBlacklistAsync(token);
                    return false;
                }

                // Update usage statistics
                storedToken.UsageCount++;
                storedToken.LastUsedAt = DateTime.UtcNow;
                if (ipAddress != null) storedToken.LastUsedIp = ipAddress;
                if (userAgent != null) storedToken.LastUsedUserAgent = userAgent;

                await _context.SaveChangesAsync();

                // Cache the updated token
                _cache.Set(cacheKey, storedToken, _cacheOptions);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return false;
            }
        }

        private bool ValidateCachedToken(StoredToken? token, string tokenType)
        {
            if (token == null || token.TokenType != tokenType || token.IsRevoked || token.ExpiresAt < DateTime.UtcNow)
                return false;

            if (token.UsageCount >= MAX_TOKEN_USAGE)
                return false;

            return true;
        }

        public async Task RevokeTokenAsync(string token, string? reason = null)
        {
            try
            {
                var storedToken = await _context.StoredTokens
                    .FirstOrDefaultAsync(t => t.Token == token);

                if (storedToken != null)
                {
                    storedToken.IsRevoked = true;
                    storedToken.RevocationReason = reason;
                    storedToken.RevokedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // Remove from cache and add to blacklist
                    _cache.Remove($"{TOKEN_CACHE_PREFIX}{token}");
                    await AddToBlacklistAsync(token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                throw;
            }
        }

        public async Task RevokeAllUserTokensAsync(string userId, string? tokenType = null, string? reason = null)
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
                    token.RevocationReason = reason;
                    token.RevokedAt = DateTime.UtcNow;
                    _cache.Remove($"{TOKEN_CACHE_PREFIX}{token.Token}");
                    await AddToBlacklistAsync(token.Token);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all tokens for user {UserId}", userId);
                throw;
            }
        }

        public async Task<string> GenerateAndStoreAccessTokenAsync(string userId, TimeSpan? expiration = null, TokenMetadata? metadata = null)
        {
            try
            {
                var token = _hashingService.GenerateTemporaryAccessToken(userId, expiration);
                var expirationTime = expiration.HasValue 
                    ? DateTime.UtcNow.Add(expiration.Value) 
                    : DateTime.UtcNow.AddDays(30);

                return await StoreTokenAsync(userId, "access", token, expirationTime, metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating access token for user {UserId}", userId);
                throw;
            }
        }

        public async Task<string> GenerateAndStoreUrlTokenAsync(string url, TimeSpan? expiration = null, TokenMetadata? metadata = null)
        {
            try
            {
                var token = _hashingService.GenerateTemporaryAccessToken(url, expiration);
                var expirationTime = expiration.HasValue 
                    ? DateTime.UtcNow.Add(expiration.Value) 
                    : DateTime.UtcNow.AddDays(7);

                return await StoreTokenAsync(url, "url", token, expirationTime, metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating URL token for {Url}", url);
                throw;
            }
        }

        public async Task CleanupExpiredTokensAsync()
        {
            try
            {
                var expiredTokens = await _context.StoredTokens
                    .Where(t => t.ExpiresAt < DateTime.UtcNow || t.IsRevoked)
                    .Take(BATCH_SIZE)
                    .ToListAsync();

                foreach (var token in expiredTokens)
                {
                    _cache.Remove($"{TOKEN_CACHE_PREFIX}{token.Token}");
                    await AddToBlacklistAsync(token.Token);
                }

                _context.StoredTokens.RemoveRange(expiredTokens);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired tokens");
                throw;
            }
        }

        public async Task<IEnumerable<StoredToken>> GetUserTokensAsync(string userId, string? tokenType = null)
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

        public async Task<TokenUsageStats?> GetTokenUsageStatsAsync(string token)
        {
            try
            {
                var storedToken = await _context.StoredTokens
                    .FirstOrDefaultAsync(t => t.Token == token);

                if (storedToken == null)
                    return null;

                return new TokenUsageStats
                {
                    UsageCount = storedToken.UsageCount,
                    LastUsedAt = storedToken.LastUsedAt,
                    LastUsedIp = storedToken.LastUsedIp,
                    LastUsedUserAgent = storedToken.LastUsedUserAgent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token usage stats");
                return null;
            }
        }

        public async Task RotateTokenAsync(string oldToken, string userId, string tokenType)
        {
            try
            {
                var storedToken = await _context.StoredTokens
                    .FirstOrDefaultAsync(t => t.Token == oldToken && t.UserId == userId && t.TokenType == tokenType);

                if (storedToken == null)
                    throw new InvalidOperationException("Token not found");

                // Generate new token
                var newToken = _hashingService.GenerateTemporaryAccessToken(userId);
                
                // Store new token
                await StoreTokenAsync(userId, tokenType, newToken, storedToken.ExpiresAt);

                // Revoke old token
                await RevokeTokenAsync(oldToken, "Token rotated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rotating token");
                throw;
            }
        }

        private async Task<bool> IsTokenBlacklistedAsync(string token)
        {
            var cacheKey = $"{BLACKLIST_CACHE_PREFIX}{token}";
            if (_cache.TryGetValue(cacheKey, out _))
                return true;

            var isBlacklisted = await _context.TokenBlacklist
                .AnyAsync(b => b.Token == token);

            if (isBlacklisted)
                _cache.Set(cacheKey, true, _cacheOptions);

            return isBlacklisted;
        }

        private async Task AddToBlacklistAsync(string token)
        {
            try
            {
                var blacklistEntry = new TokenBlacklist
                {
                    Token = token,
                    AddedAt = DateTime.UtcNow
                };

                _context.TokenBlacklist.Add(blacklistEntry);
                await _context.SaveChangesAsync();

                var cacheKey = $"{BLACKLIST_CACHE_PREFIX}{token}";
                _cache.Set(cacheKey, true, _cacheOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding token to blacklist");
                throw;
            }
        }

        public async Task CleanupBlacklistAsync()
        {
            try
            {
                var oldEntries = await _context.TokenBlacklist
                    .Where(b => b.AddedAt < DateTime.UtcNow.AddDays(-7))
                    .Take(BATCH_SIZE)
                    .ToListAsync();

                foreach (var entry in oldEntries)
                {
                    _cache.Remove($"{BLACKLIST_CACHE_PREFIX}{entry.Token}");
                }

                _context.TokenBlacklist.RemoveRange(oldEntries);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up token blacklist");
                throw;
            }
        }
    }

    public class StoredToken
    {
        public int Id { get; set; }
        public required string UserId { get; set; }
        public required string TokenType { get; set; }
        public required string Token { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
        public string? RevocationReason { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? Metadata { get; set; }
        public int UsageCount { get; set; }
        public DateTime LastUsedAt { get; set; }
        public string? LastUsedIp { get; set; }
        public string? LastUsedUserAgent { get; set; }

        public void SetMetadata(TokenMetadata metadata)
        {
            if (metadata != null)
            {
                Metadata = JsonSerializer.Serialize(metadata);
                if (metadata.IpAddress != null) LastUsedIp = metadata.IpAddress;
                if (metadata.UserAgent != null) LastUsedUserAgent = metadata.UserAgent;
                if (metadata.DeviceId != null)
                {
                    // Assuming LastUsedIp and LastUsedUserAgent are set by the caller
                }
            }
        }
    }

    public class TokenBlacklist
    {
        public int Id { get; set; }
        public required string Token { get; set; }
        public DateTime AddedAt { get; set; }
    }
} 