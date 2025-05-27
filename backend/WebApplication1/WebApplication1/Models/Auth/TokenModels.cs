using System;
using System.Collections.Generic;
using System.Text.Json;

namespace WebApplication1.Models.Auth
{
    public class StoredToken
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string TokenType { get; set; }
        public string Token { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
        public string RevocationReason { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string Metadata { get; set; }
        public int UsageCount { get; set; }
        public DateTime LastUsedAt { get; set; }
        public string LastUsedIp { get; set; }
        public string LastUsedUserAgent { get; set; }

        public TokenMetadata GetMetadata()
        {
            if (string.IsNullOrEmpty(Metadata))
                return null;

            try
            {
                return JsonSerializer.Deserialize<TokenMetadata>(Metadata);
            }
            catch
            {
                return null;
            }
        }

        public void SetMetadata(TokenMetadata metadata)
        {
            if (metadata == null)
            {
                Metadata = null;
                return;
            }

            Metadata = JsonSerializer.Serialize(metadata);
        }
    }

    public class TokenBlacklist
    {
        public int Id { get; set; }
        public string Token { get; set; }
        public DateTime AddedAt { get; set; }
    }

    public class TokenMetadata
    {
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string DeviceId { get; set; }
        public Dictionary<string, string> CustomData { get; set; }
    }

    public class TokenUsageStats
    {
        public int UsageCount { get; set; }
        public DateTime LastUsedAt { get; set; }
        public string LastUsedIp { get; set; }
        public string LastUsedUserAgent { get; set; }
    }
} 