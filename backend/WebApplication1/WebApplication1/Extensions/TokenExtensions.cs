using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WebApplication1.Services;

namespace WebApplication1.Extensions
{
    public static class TokenExtensions
    {
        public static string? GetUserIdFromToken(this HttpContext context)
        {
            return context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        public static string? GetUserEmailFromToken(this HttpContext context)
        {
            return context.User.FindFirst(ClaimTypes.Email)?.Value;
        }

        public static string? GetUserRoleFromToken(this HttpContext context)
        {
            return context.User.FindFirst(ClaimTypes.Role)?.Value;
        }

        public static string? GetDisplayNameFromToken(this HttpContext context)
        {
            return context.User.FindFirst("DisplayName")?.Value;
        }

        public static bool IsUserVerified(this HttpContext context)
        {
            var isVerifiedClaim = context.User.FindFirst("IsVerified")?.Value;
            return bool.TryParse(isVerifiedClaim, out var isVerified) && isVerified;
        }

        public static async Task<bool> ValidateAndRefreshTokenAsync(this HttpContext context)
        {
            var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            if (string.IsNullOrEmpty(token))
                return false;

            var userId = context.GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId))
                return false;

            var tokenService = context.RequestServices.GetRequiredService<ITokenService>();
            return await tokenService.ValidateRefreshTokenAsync(token, userId);
        }

        public static async Task<(string accessToken, string refreshToken)> RefreshTokensAsync(this HttpContext context)
        {
            var refreshToken = context.Request.Headers["X-Refresh-Token"].ToString();
            if (string.IsNullOrEmpty(refreshToken))
                throw new InvalidOperationException("Refresh token is required");

            var authService = context.RequestServices.GetRequiredService<IAuthService>();
            return await authService.RefreshTokenAsync(refreshToken);
        }

        public static async Task RevokeCurrentTokenAsync(this HttpContext context)
        {
            var refreshToken = context.Request.Headers["X-Refresh-Token"].ToString();
            if (string.IsNullOrEmpty(refreshToken))
                throw new InvalidOperationException("Refresh token is required");

            var tokenService = context.RequestServices.GetRequiredService<ITokenService>();
            await tokenService.RevokeRefreshTokenAsync(refreshToken);
        }

        public static async Task RevokeAllUserTokensAsync(this HttpContext context)
        {
            var userId = context.GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId))
                throw new InvalidOperationException("User ID not found in token");

            var tokenService = context.RequestServices.GetRequiredService<ITokenService>();
            await tokenService.RevokeAllRefreshTokensAsync(userId);
        }
    }
} 