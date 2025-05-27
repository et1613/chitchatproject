using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography.X509Certificates;
using Polly;
using System.Net.Http;
using System.Text.Json;
using Polly.CircuitBreaker;

namespace WebApplication1.Services
{
    public class DigitalSignatureOptions
    {
        public int KeySize { get; set; } = 2048;
        public string DefaultAlgorithm { get; set; } = "RSA";
        public int SignatureValidityHours { get; set; } = 24;
        public bool EnableKeyRotation { get; set; } = true;
        public int KeyRotationDays { get; set; } = 30;
        public string KeyStoragePath { get; set; } = "Keys";
        public bool EnableCaching { get; set; } = true;
        public int CacheExpirationMinutes { get; set; } = 60;
        public bool EnableHsm { get; set; } = false;
        public string HsmEndpoint { get; set; }
        public string HsmApiKey { get; set; }
        public bool EnableTimestampService { get; set; } = false;
        public string TimestampServiceUrl { get; set; }
        public int BatchSize { get; set; } = 100;
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 1000;
        public bool EnableCircuitBreaker { get; set; } = true;
        public int CircuitBreakerThreshold { get; set; } = 5;
        public int CircuitBreakerDurationSeconds { get; set; } = 30;
    }

    public class SignatureResult
    {
        public string Signature { get; set; }
        public string Algorithm { get; set; }
        public DateTime Timestamp { get; set; }
        public string KeyId { get; set; }
        public string ChainOfTrust { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
    }

    public class BatchSignatureRequest
    {
        public string Content { get; set; }
        public string PrivateKey { get; set; }
        public string Algorithm { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
    }

    public class BatchSignatureResult
    {
        public List<SignatureResult> Signatures { get; set; }
        public Dictionary<string, string> Errors { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    public class SignatureMetrics
    {
        public int TotalSignatures { get; set; }
        public int SuccessfulSignatures { get; set; }
        public int FailedSignatures { get; set; }
        public double AverageProcessingTime { get; set; }
        public Dictionary<string, int> AlgorithmUsage { get; set; }
        public Dictionary<string, int> ErrorTypes { get; set; }
    }

    public class DigitalSignatureService
    {
        private readonly ILogger<DigitalSignatureService> _logger;
        private readonly DigitalSignatureOptions _options;
        private readonly IDistributedCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ConcurrentDictionary<string, (string key, DateTime expiry)> _keyCache;
        private readonly ConcurrentDictionary<string, (string signature, DateTime expiry)> _signatureCache;
        private readonly ConcurrentDictionary<string, SignatureMetrics> _metrics;
        private readonly AsyncCircuitBreakerPolicy _circuitBreaker;
        private readonly IAsyncPolicy _retryPolicy;

        public DigitalSignatureService(
            ILogger<DigitalSignatureService> logger,
            IOptions<DigitalSignatureOptions> options,
            IDistributedCache cache,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _options = options.Value;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _keyCache = new ConcurrentDictionary<string, (string, DateTime)>();
            _signatureCache = new ConcurrentDictionary<string, (string, DateTime)>();
            _metrics = new ConcurrentDictionary<string, SignatureMetrics>();

            _circuitBreaker = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: _options.CircuitBreakerThreshold,
                    durationOfBreak: TimeSpan.FromSeconds(_options.CircuitBreakerDurationSeconds)
                );

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    _options.MaxRetryAttempts,
                    retryAttempt => TimeSpan.FromMilliseconds(_options.RetryDelayMilliseconds * Math.Pow(2, retryAttempt))
                );

            if (!Directory.Exists(_options.KeyStoragePath))
            {
                Directory.CreateDirectory(_options.KeyStoragePath);
            }
        }

        public async Task<SignatureResult> SignMessageAsync(string content, string privateKey, string algorithm = null, Dictionary<string, string> metadata = null)
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                try
                {
                    var startTime = DateTime.UtcNow;
                    algorithm ??= _options.DefaultAlgorithm;
                    var keyId = GenerateKeyId(privateKey);
                    var cacheKey = $"{content}:{keyId}:{algorithm}";

                    if (_options.EnableCaching)
                    {
                        var cachedSignature = await _cache.GetStringAsync(cacheKey);
                        if (!string.IsNullOrEmpty(cachedSignature))
                        {
                            _logger.LogInformation("Using cached signature for content");
                            return JsonSerializer.Deserialize<SignatureResult>(cachedSignature);
                        }
                    }

                    byte[] signature;
                    if (_options.EnableHsm)
                    {
                        signature = await SignWithHsmAsync(content, privateKey, algorithm);
                    }
                    else
                    {
                        signature = await SignLocallyAsync(content, privateKey, algorithm);
                    }

                    var timestamp = _options.EnableTimestampService
                        ? await GetTimestampAsync(signature)
                        : DateTime.UtcNow;

                    var result = new SignatureResult
                    {
                        Signature = Convert.ToBase64String(signature),
                        Algorithm = algorithm,
                        Timestamp = timestamp,
                        KeyId = keyId,
                        ChainOfTrust = await GenerateChainOfTrustAsync(keyId),
                        Metadata = metadata ?? new Dictionary<string, string>()
                    };

                    if (_options.EnableCaching)
                    {
                        await _cache.SetStringAsync(
                            cacheKey,
                            JsonSerializer.Serialize(result),
                            new DistributedCacheEntryOptions
                            {
                                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes)
                            }
                        );
                    }

                    UpdateMetrics(algorithm, true, DateTime.UtcNow - startTime);
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error signing message with algorithm {Algorithm}", algorithm);
                    UpdateMetrics(algorithm, false, TimeSpan.Zero, ex);
                    throw new DigitalSignatureException("Failed to sign message", ex);
                }
            });
        }

        public async Task<BatchSignatureResult> SignBatchAsync(IEnumerable<BatchSignatureRequest> requests)
        {
            var result = new BatchSignatureResult
            {
                Signatures = new List<SignatureResult>(),
                Errors = new Dictionary<string, string>()
            };

            var startTime = DateTime.UtcNow;
            var batches = requests.Chunk(_options.BatchSize);

            foreach (var batch in batches)
            {
                var tasks = batch.Select(async request =>
                {
                    try
                    {
                        var signature = await SignMessageAsync(
                            request.Content,
                            request.PrivateKey,
                            request.Algorithm,
                            request.Metadata
                        );
                        return (true, signature, null);
                    }
                    catch (Exception ex)
                    {
                        return (false, null, ex.Message);
                    }
                });

                var batchResults = await Task.WhenAll(tasks);
                foreach (var (success, signature, error) in batchResults)
                {
                    if (success)
                    {
                        result.Signatures.Add(signature);
                    }
                    else
                    {
                        result.Errors[signature?.KeyId ?? "unknown"] = error;
                    }
                }
            }

            result.ProcessingTime = DateTime.UtcNow - startTime;
            return result;
        }

        public async Task<bool> VerifySignatureAsync(string content, SignatureResult signatureResult, string publicKey)
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                try
                {
                    if (DateTime.UtcNow > signatureResult.Timestamp.AddHours(_options.SignatureValidityHours))
                    {
                        _logger.LogWarning("Signature has expired");
                        return false;
                    }

                    if (!await VerifyChainOfTrustAsync(signatureResult.ChainOfTrust))
                    {
                        _logger.LogWarning("Chain of trust verification failed");
                        return false;
                    }

                    var signatureBytes = Convert.FromBase64String(signatureResult.Signature);
                    bool isValid;

                    if (_options.EnableHsm)
                    {
                        isValid = await VerifyWithHsmAsync(content, signatureBytes, publicKey, signatureResult.Algorithm);
                    }
                    else
                    {
                        isValid = await VerifyLocallyAsync(content, signatureBytes, publicKey, signatureResult.Algorithm);
                    }

                    _logger.LogInformation("Signature verification result: {IsValid}", isValid);
                    return isValid;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error verifying signature");
                    return false;
                }
            });
        }

        public async Task<SignatureMetrics> GetMetricsAsync()
        {
            return new SignatureMetrics
            {
                TotalSignatures = _metrics.Values.Sum(m => m.TotalSignatures),
                SuccessfulSignatures = _metrics.Values.Sum(m => m.SuccessfulSignatures),
                FailedSignatures = _metrics.Values.Sum(m => m.FailedSignatures),
                AverageProcessingTime = _metrics.Values.Average(m => m.AverageProcessingTime),
                AlgorithmUsage = _metrics.Values
                    .SelectMany(m => m.AlgorithmUsage)
                    .GroupBy(kv => kv.Key)
                    .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value)),
                ErrorTypes = _metrics.Values
                    .SelectMany(m => m.ErrorTypes)
                    .GroupBy(kv => kv.Key)
                    .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value))
            };
        }

        private async Task<byte[]> SignWithHsmAsync(string content, string privateKey, string algorithm)
        {
            var client = _httpClientFactory.CreateClient("HSM");
            var response = await client.PostAsJsonAsync(_options.HsmEndpoint, new
            {
                Content = content,
                PrivateKey = privateKey,
                Algorithm = algorithm
            });

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HsmSignResponse>();
            return Convert.FromBase64String(result.Signature);
        }

        private async Task<byte[]> SignLocallyAsync(string content, string privateKey, string algorithm)
        {
            switch (algorithm.ToUpper())
            {
                case "RSA":
                    return await SignWithRSAAsync(content, privateKey);
                case "ECDSA":
                    return await SignWithECDSAAsync(content, privateKey);
                case "EDDSA":
                    return await SignWithEdDSAAsync(content, privateKey);
                default:
                    throw new ArgumentException($"Unsupported algorithm: {algorithm}");
            }
        }

        private async Task<bool> VerifyWithHsmAsync(string content, byte[] signature, string publicKey, string algorithm)
        {
            var client = _httpClientFactory.CreateClient("HSM");
            var response = await client.PostAsJsonAsync($"{_options.HsmEndpoint}/verify", new
            {
                Content = content,
                Signature = Convert.ToBase64String(signature),
                PublicKey = publicKey,
                Algorithm = algorithm
            });

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HsmVerifyResponse>();
            return result.IsValid;
        }

        private async Task<bool> VerifyLocallyAsync(string content, byte[] signature, string publicKey, string algorithm)
        {
            switch (algorithm.ToUpper())
            {
                case "RSA":
                    return await VerifyWithRSAAsync(content, signature, publicKey);
                case "ECDSA":
                    return await VerifyWithECDSAAsync(content, signature, publicKey);
                case "EDDSA":
                    return await VerifyWithEdDSAAsync(content, signature, publicKey);
                default:
                    throw new ArgumentException($"Unsupported algorithm: {algorithm}");
            }
        }

        private async Task<DateTime> GetTimestampAsync(byte[] signature)
        {
            var client = _httpClientFactory.CreateClient("Timestamp");
            var response = await client.PostAsJsonAsync(_options.TimestampServiceUrl, new
            {
                Signature = Convert.ToBase64String(signature)
            });

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<TimestampResponse>();
            return result.Timestamp;
        }

        private async Task<string> GenerateChainOfTrustAsync(string keyId)
        {
            // Implement chain of trust generation logic
            return await Task.FromResult($"trust-chain-{keyId}");
        }

        private async Task<bool> VerifyChainOfTrustAsync(string chainOfTrust)
        {
            // Implement chain of trust verification logic
            return await Task.FromResult(true);
        }

        private void UpdateMetrics(string algorithm, bool success, TimeSpan processingTime, Exception ex = null)
        {
            var metrics = _metrics.GetOrAdd(algorithm, _ => new SignatureMetrics
            {
                AlgorithmUsage = new Dictionary<string, int>(),
                ErrorTypes = new Dictionary<string, int>()
            });

            metrics.TotalSignatures++;
            if (success)
            {
                metrics.SuccessfulSignatures++;
            }
            else
            {
                metrics.FailedSignatures++;
                if (ex != null)
                {
                    var errorType = ex.GetType().Name;
                    metrics.ErrorTypes[errorType] = metrics.ErrorTypes.GetValueOrDefault(errorType) + 1;
                }
            }

            metrics.AlgorithmUsage[algorithm] = metrics.AlgorithmUsage.GetValueOrDefault(algorithm) + 1;
            metrics.AverageProcessingTime = (metrics.AverageProcessingTime * (metrics.TotalSignatures - 1) + processingTime.TotalMilliseconds) / metrics.TotalSignatures;
        }

        private async Task<byte[]> SignWithRSAAsync(string content, string privateKey)
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKey), out _);
            var data = Encoding.UTF8.GetBytes(content);
            return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        private async Task<byte[]> SignWithECDSAAsync(string content, string privateKey)
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportECPrivateKey(Convert.FromBase64String(privateKey), out _);
            var data = Encoding.UTF8.GetBytes(content);
            return ecdsa.SignData(data, HashAlgorithmName.SHA256);
        }

        private async Task<byte[]> SignWithEdDSAAsync(string content, string privateKey)
        {
            using var eddsa = new Ed25519();
            eddsa.ImportPrivateKey(Convert.FromBase64String(privateKey));
            var data = Encoding.UTF8.GetBytes(content);
            return eddsa.SignData(data);
        }

        private async Task<bool> VerifyWithRSAAsync(string content, byte[] signature, string publicKey)
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKey), out _);
            var data = Encoding.UTF8.GetBytes(content);
            return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        private async Task<bool> VerifyWithECDSAAsync(string content, byte[] signature, string publicKey)
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportECPublicKey(Convert.FromBase64String(publicKey), out _);
            var data = Encoding.UTF8.GetBytes(content);
            return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
        }

        private async Task<bool> VerifyWithEdDSAAsync(string content, byte[] signature, string publicKey)
        {
            using var eddsa = new Ed25519();
            eddsa.ImportPublicKey(Convert.FromBase64String(publicKey));
            var data = Encoding.UTF8.GetBytes(content);
            return eddsa.VerifyData(data, signature);
        }

        private string GenerateKeyId(string key) => 
            Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }

    public class HsmSignResponse
    {
        public string Signature { get; set; }
    }

    public class HsmVerifyResponse
    {
        public bool IsValid { get; set; }
    }

    public class TimestampResponse
    {
        public DateTime Timestamp { get; set; }
    }

    public class DigitalSignatureException : Exception
    {
        public DigitalSignatureException(string message) : base(message) { }
        public DigitalSignatureException(string message, Exception inner) : base(message, inner) { }
    }

    // Ed25519 implementation (simplified for example)
    public class Ed25519 : IDisposable
    {
        public void ImportPrivateKey(string privateKey) { }
        public void ImportPublicKey(string publicKey) { }
        public byte[] ExportPrivateKey() => new byte[32];
        public byte[] ExportPublicKey() => new byte[32];
        public byte[] SignData(byte[] data) => new byte[64];
        public bool VerifyData(byte[] data, byte[] signature) => true;
        public void Dispose() { }
    }
} 