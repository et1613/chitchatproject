using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebApplication1.Repositories;

namespace WebApplication1.Services
{
    public class TokenCleanupService : BackgroundService
    {
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly ILogger<TokenCleanupService> _logger;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);

        public TokenCleanupService(
            IRefreshTokenRepository refreshTokenRepository,
            ILogger<TokenCleanupService> logger)
        {
            _refreshTokenRepository = refreshTokenRepository;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Token Cleanup Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Token Cleanup Service is running cleanup task.");
                    await _refreshTokenRepository.CleanupExpiredTokensAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while cleaning up tokens.");
                }

                await Task.Delay(_cleanupInterval, stoppingToken);
            }

            _logger.LogInformation("Token Cleanup Service is stopping.");
        }
    }
} 