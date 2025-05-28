using System;
using System.Collections.Generic;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models.Auth
{
    public class StoredToken
    {
        public int Id { get; set; }

        [Required]
        public required string UserId { get; set; }

        [Required]
        public required string TokenType { get; set; }

        [Required]
        public required string Token { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime ExpiresAt { get; set; }

        public bool IsRevoked { get; set; }

        public string? RevocationReason { get; set; }

        public DateTime? RevokedAt { get; set; }

        public string? Metadata { get; set; }

        public int UsageCount { get; set; }

        public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

        public string? LastUsedIp { get; set; }

        public string? LastUsedUserAgent { get; set; }

        public TokenMetadata? GetMetadata()
        {
            if (string.IsNullOrEmpty(Metadata))
                return null;

            try
            {
                return JsonSerializer.Deserialize<TokenMetadata>(Metadata);
            }
            catch (JsonException)
            {
                // Log the error
                return null;
            }
        }

        public void SetMetadata(TokenMetadata? metadata)
        {
            if (metadata == null)
            {
                Metadata = null;
                return;
            }

            try
            {
                Metadata = JsonSerializer.Serialize(metadata);
            }
            catch (JsonException)
            {
                // Log the error
                throw new InvalidOperationException("Failed to serialize token metadata");
            }
        }
    }

    public class TokenBlacklist
    {
        public int Id { get; set; }

        [Required]
        public required string Token { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }

    public class TokenMetadata
    {
        [RegularExpression(@"^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$|^([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$")]
        public string? IpAddress { get; set; }

        [StringLength(500)]
        public string? UserAgent { get; set; }

        [StringLength(100)]
        public string? DeviceId { get; set; }

        public Dictionary<string, string>? CustomData { get; set; }

        public void AddCustomData(string key, string value)
        {
            CustomData ??= new Dictionary<string, string>();
            CustomData[key] = value;
        }

        public string? GetCustomData(string key)
        {
            return CustomData?.TryGetValue(key, out var value) == true ? value : null;
        }
    }

    public class TokenUsageStats
    {
        public int UsageCount { get; set; }

        public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

        [RegularExpression(@"^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$|^([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$")]
        public string? LastUsedIp { get; set; }

        [StringLength(500)]
        public string? LastUsedUserAgent { get; set; }
    }
} 