using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebApplication1.Services;
using System.Text.Json;
using System.Net;
using System.Security.Claims;

namespace WebApplication1.Middleware
{
    public class TokenValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenValidationMiddleware> _logger;

        public TokenValidationMiddleware(
            RequestDelegate next,
            ILogger<TokenValidationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var tokenService = context.RequestServices.GetRequiredService<ITokenService>();
            try
            {
                // Skip token validation for authentication endpoints
                if (IsAuthenticationEndpoint(context.Request.Path))
                {
                    await _next(context);
                    return;
                }

                var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                if (string.IsNullOrEmpty(token))
                {
                    await _next(context);
                    return;
                }

                var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("User ID not found in token");
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Invalid token" }));
                    return;
                }

                if (!await tokenService.ValidateRefreshTokenAsync(token, userId))
                {
                    _logger.LogWarning($"Invalid token detected for user {userId}");
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Invalid token" }));
                    return;
                }

                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TokenValidationMiddleware");
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Internal server error" }));
            }
        }

        private bool IsAuthenticationEndpoint(PathString path)
        {
            return path.StartsWithSegments("/api/auth/login") ||
                   path.StartsWithSegments("/api/auth/register") ||
                   path.StartsWithSegments("/api/auth/refresh-token");
        }
    }

    // Extension method for easy middleware registration
    public static class TokenValidationMiddlewareExtensions
    {
        public static IApplicationBuilder UseTokenValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TokenValidationMiddleware>();
        }
    }
} 