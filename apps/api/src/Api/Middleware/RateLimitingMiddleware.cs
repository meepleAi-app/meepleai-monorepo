using System.Net;
using System.Text.Json;
using Api.Models;
using Api.Services;

namespace Api.Middleware;

/// <summary>
/// Applies per-request rate limiting headers and enforcement based on user role or IP.
/// - Authenticated: key = user:{UserId}, role from ActiveSession/User claims
/// - Anonymous: key = ip:{RemoteIp}
/// Adds X-RateLimit-Limit, X-RateLimit-Remaining, and Retry-After (when limited).
/// Returns 429 JSON body when limited.
/// </summary>
public class RateLimitingMiddleware
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
            var services = context.RequestServices;

            // Prefer concrete RateLimitService to support test overrides; fallback to interface
            var rateLimiter = services.GetService<RateLimitService>() as IRateLimitService
                               ?? services.GetRequiredService<IRateLimitService>();

            // Determine identity: ActiveSession if present, otherwise anonymous by IP
            string role;
            string rateKey;

            if (context.Items.TryGetValue(nameof(ActiveSession), out var value) && value is ActiveSession session)
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

            var cfg = await rateLimiter.GetConfigForRoleAsync(role, context.RequestAborted);
            var rl = await rateLimiter.CheckRateLimitAsync(rateKey, cfg.MaxTokens, cfg.RefillRate, context.RequestAborted);

            // Always add headers for observability
            context.Response.Headers["X-RateLimit-Limit"] = cfg.MaxTokens.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(rl.TokensRemaining, 0).ToString();

            if (!rl.Allowed)
            {
                context.Response.Headers["Retry-After"] = rl.RetryAfterSeconds.ToString();
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var payload = new
                {
                    error = "Rate limit exceeded",
                    retryAfter = rl.RetryAfterSeconds,
                    message = "Too many requests. Please retry later."
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
                return;
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // MIDDLEWARE BOUNDARY PATTERN: Rate limiting middleware must fail-open to avoid self-DOS
            // Rationale: This catch should only handle rate limiting infrastructure failures (e.g. Redis down).
            _logger.LogWarning(ex, "Rate limiting middleware encountered an error; allowing request (fail-open)");
            await _next(context);
            return;
        }
#pragma warning restore CA1031 // Do not catch general exception types

        await _next(context);
    }
}

public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRoleAwareRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitingMiddleware>();
    }
}
