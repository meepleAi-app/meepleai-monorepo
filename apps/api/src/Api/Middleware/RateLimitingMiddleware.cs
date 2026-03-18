using System.Net;
using System.Text.Json;
using System.Globalization;
using Api.Extensions;
using Api.Models;
using Api.Services;

#pragma warning disable MA0048 // File name must match type name - Contains Middleware with Options/Extensions
namespace Api.Middleware;

/// <summary>
/// Applies per-request rate limiting headers and enforcement based on user role or IP.
/// - Authenticated: key = user:{UserId}, role from ActiveSession/User claims
/// - Anonymous: key = ip:{RemoteIp}
/// Adds X-RateLimit-Limit, X-RateLimit-Remaining, and Retry-After (when limited).
/// Returns 429 JSON body when limited.
/// </summary>
internal class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Issue #2296: Explicit bypass for CI/testing - primary mechanism
            // More reliable than ASPNETCORE_ENVIRONMENT which has initialization subtleties
            var disableRateLimiting = Environment.GetEnvironmentVariable("DISABLE_RATE_LIMITING");
            if (string.Equals(disableRateLimiting, "true", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            // Issue #2286: Disable rate limiting for CI environment (K6 performance tests)
            // Defense in depth: fallback to environment-based detection
            var env = context.RequestServices.GetRequiredService<IHostEnvironment>();
            var aspnetcoreEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (string.Equals(env.EnvironmentName, "CI", StringComparison.Ordinal) ||
                string.Equals(aspnetcoreEnv, "CI", StringComparison.Ordinal))
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            var services = context.RequestServices;

            // Prefer concrete RateLimitService to support test overrides; fallback to interface
            var rateLimiter = services.GetService<RateLimitService>() as IRateLimitService
                               ?? services.GetRequiredService<IRateLimitService>();

            // Determine identity: ActiveSession if present, otherwise anonymous by IP
            string role;
            string rateKey;

            var (authenticated, session, _) = context.TryGetActiveSession();

            if (authenticated && session.User is not null)
            {
                role = session.User.Role;
                rateKey = $"user:{session.User.Id}";
            }
            else
            {
                role = "anonymous";
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                rateKey = $"ip:{ip}";
            }

            var cfg = await rateLimiter.GetConfigForRoleAsync(role, context.RequestAborted).ConfigureAwait(false);
            var rl = await rateLimiter.CheckRateLimitAsync(rateKey, cfg.MaxTokens, cfg.RefillRate, context.RequestAborted).ConfigureAwait(false);

            // Always add headers for observability
            context.Response.Headers["X-RateLimit-Limit"] = cfg.MaxTokens.ToString(CultureInfo.InvariantCulture);
            context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(rl.TokensRemaining, 0).ToString(CultureInfo.InvariantCulture);

            if (!rl.Allowed)
            {
                context.Response.Headers["Retry-After"] = rl.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var payload = new
                {
                    error = "Rate limit exceeded",
                    retryAfter = rl.RetryAfterSeconds,
                    message = "Too many requests. Please retry later."
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(payload)).ConfigureAwait(false);
                return;
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // MIDDLEWARE BOUNDARY PATTERN: Rate limiting middleware must fail-open to avoid self-DOS
            // SEC-I4: Error level so alerting detects rate limit infrastructure failures (e.g. Redis down)
            _logger.LogError(ex,
                "SEC-I4: Rate limiting fail-open. Redis may be down — all requests allowed without rate limiting. Path: {Path}, Method: {Method}",
                context.Request.Path, context.Request.Method);
            await _next(context).ConfigureAwait(false);
            return;
        }
#pragma warning restore CA1031 // Do not catch general exception types

        await _next(context).ConfigureAwait(false);
    }
}

internal static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRoleAwareRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitingMiddleware>();
    }
}
