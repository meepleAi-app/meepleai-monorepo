using System.Globalization;
using System.Net;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Extensions;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Options;

namespace Api.Middleware;

/// <summary>
/// Enforces BGG API rate limits per user tier to prevent abuse and distribute capacity fairly.
/// Issue #4275: BGG Rate Limiting with User Quota Tracking
///
/// <para><strong>Rate Limits by Tier:</strong></para>
/// <list type="bullet">
/// <item><description>Free: 5 requests/minute</description></item>
/// <item><description>Normal: 10 requests/minute</description></item>
/// <item><description>Premium: 20 requests/minute</description></item>
/// <item><description>Editor: 15 requests/minute</description></item>
/// <item><description>Admin: Unlimited (bypassed)</description></item>
/// </list>
///
/// <para><strong>Response Headers:</strong></para>
/// <list type="bullet">
/// <item><description>X-RateLimit-Limit: Maximum requests allowed in window</description></item>
/// <item><description>X-RateLimit-Remaining: Requests remaining in current window</description></item>
/// <item><description>X-RateLimit-Reset: Unix timestamp when limit resets</description></item>
/// <item><description>Retry-After: Seconds to wait before retrying (429 only)</description></item>
/// </list>
///
/// <para><strong>Resilience:</strong></para>
/// <list type="bullet">
/// <item><description>Fail-open: Redis errors allow request (logged)</description></item>
/// <item><description>CI bypass: Disabled in Testing/CI environments</description></item>
/// </list>
/// </summary>
internal class BggRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BggRateLimitMiddleware> _logger;
    private readonly BggRateLimitOptions _options;

    public BggRateLimitMiddleware(
        RequestDelegate next,
        ILogger<BggRateLimitMiddleware> logger,
        IOptions<BggRateLimitOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, IRateLimitService rateLimitService)
    {
        try
        {
            // Filter: Only apply to BGG API endpoints
            if (!IsBggApiRequest(context))
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            // Extract user identity from active session
            var (authenticated, session, _) = context.TryGetActiveSession();

            if (!authenticated || session?.User is null)
            {
                // Unauthenticated BGG requests not allowed
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Authentication required",
                    message = "BGG API access requires authentication"
                }, context.RequestAborted).ConfigureAwait(false);
                return;
            }

            var user = session.User;
            var userRole = Role.Parse(user.Role);

            // Admin bypass
            if (_options.AdminBypass && userRole == Role.Admin)
            {
                // Add headers indicating unlimited access
                context.Response.Headers["X-RateLimit-Limit"] = "unlimited";
                context.Response.Headers["X-RateLimit-Remaining"] = "unlimited";
                await _next(context).ConfigureAwait(false);
                return;
            }

            // Get tier-based limit (Editor role gets special limit)
            var userTier = UserTier.Parse(user.Tier);
            var maxRequests = GetLimitForTier(userTier, userRole);
            var refillRate = maxRequests / (double)_options.WindowSeconds;

            // Check rate limit using existing service (token bucket algorithm)
            var rateKey = $"bgg:{user.Id}";
            var result = await rateLimitService.CheckRateLimitAsync(
                rateKey,
                maxRequests,
                refillRate,
                context.RequestAborted).ConfigureAwait(false);

            // Calculate reset timestamp
            var resetTimestamp = DateTimeOffset.UtcNow.AddSeconds(result.RetryAfterSeconds).ToUnixTimeSeconds();

            // Add response headers (always, for observability)
            context.Response.Headers["X-RateLimit-Limit"] = maxRequests.ToString(CultureInfo.InvariantCulture);
            context.Response.Headers["X-RateLimit-Remaining"] = result.TokensRemaining.ToString(CultureInfo.InvariantCulture);
            context.Response.Headers["X-RateLimit-Reset"] = resetTimestamp.ToString(CultureInfo.InvariantCulture);

            if (!result.Allowed)
            {
                // Rate limit exceeded - return 429
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.Headers["Retry-After"] = result.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);

                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Rate limit exceeded",
                    message = $"BGG API limit exceeded. Maximum {maxRequests} requests per minute for {userTier} tier.",
                    retryAfter = result.RetryAfterSeconds,
                    limit = maxRequests,
                    remaining = 0,
                    reset = resetTimestamp
                }, context.RequestAborted).ConfigureAwait(false);

                _logger.LogWarning(
                    "BGG rate limit exceeded for user {UserId} (tier: {Tier}). Limit: {Limit}/min, Retry after: {RetryAfter}s",
                    user.Id,
                    userTier,
                    maxRequests,
                    result.RetryAfterSeconds);

                return;
            }

            // Request allowed - continue pipeline
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Fail-open strategy: Log error but allow request
            _logger.LogError(ex, "BGG rate limit check failed. Failing open to prevent service disruption.");
            context.Response.Headers["X-RateLimit-Status"] = "Error";
            await _next(context).ConfigureAwait(false);
        }
    }

    private static bool IsBggApiRequest(HttpContext context)
    {
        var path = context.Request.Path.Value;
        return path?.StartsWith("/api/v1/bgg/", StringComparison.OrdinalIgnoreCase) == true;
    }

    private int GetLimitForTier(UserTier tier, Role userRole)
    {
        // Editor role gets special rate limit regardless of subscription tier
        if (userRole == Role.Editor)
            return _options.EditorTier;

        return tier.Value switch
        {
            "free" => _options.FreeTier,
            "normal" => _options.NormalTier,
            "premium" or "pro" => _options.PremiumTier,
            "enterprise" => _options.PremiumTier,
            _ => _options.FreeTier // Default to most restrictive
        };
    }
}

/// <summary>
/// Configuration options for BGG API rate limiting.
/// Issue #4275: Tier-based BGG quota tracking.
/// </summary>
public class BggRateLimitOptions
{
    /// <summary>
    /// Maximum BGG API requests per minute for Free tier users.
    /// Default: 5 requests/minute
    /// </summary>
    public int FreeTier { get; set; } = 5;

    /// <summary>
    /// Maximum BGG API requests per minute for Normal tier users.
    /// Default: 10 requests/minute
    /// </summary>
    public int NormalTier { get; set; } = 10;

    /// <summary>
    /// Maximum BGG API requests per minute for Premium tier users.
    /// Default: 20 requests/minute
    /// </summary>
    public int PremiumTier { get; set; } = 20;

    /// <summary>
    /// Maximum BGG API requests per minute for Editor tier users.
    /// Default: 15 requests/minute
    /// </summary>
    public int EditorTier { get; set; } = 15;

    /// <summary>
    /// Rate limit window in seconds.
    /// Default: 60 seconds (1 minute)
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Whether Admin users bypass rate limiting entirely.
    /// Default: true
    /// </summary>
    public bool AdminBypass { get; set; } = true;

    /// <summary>
    /// Whether to collect Prometheus metrics for BGG API usage.
    /// Default: true
    /// </summary>
    public bool EnableMetrics { get; set; } = true;
}

/// <summary>
/// Extension methods for registering BGG rate limiting middleware.
/// </summary>
internal static class BggRateLimitMiddlewareExtensions
{
    /// <summary>
    /// Adds BGG API rate limiting middleware to the pipeline.
    /// Must be called after authentication middleware.
    /// Issue #4275: Tier-based BGG quota enforcement.
    /// </summary>
    public static IApplicationBuilder UseBggRateLimit(this IApplicationBuilder app)
    {
        return app.UseMiddleware<BggRateLimitMiddleware>();
    }
}
