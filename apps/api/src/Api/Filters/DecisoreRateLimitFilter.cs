using System.Globalization;
using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Services;

namespace Api.Filters;

/// <summary>
/// Endpoint filter applying rate limiting for Decisore Agent expert analysis.
/// Issue #4334: Prevents abuse of compute-intensive multi-model evaluations.
///
/// Pattern: Fixed window rate limiting with Redis-backed token bucket.
/// Limit: 10 expert analyses per minute (higher cost than quick validations).
///
/// Usage:
/// <code>
/// group.MapPost("/analyze", handler)
///     .AddEndpointFilter&lt;DecisoreRateLimitFilter&gt;();
/// </code>
/// </summary>
internal class DecisoreRateLimitFilter : IEndpointFilter
{
    /// <summary>
    /// Maximum expert analyses allowed per window (burst capacity).
    /// Default: 10 requests per minute (stricter due to LLM costs).
    /// </summary>
    public const int DefaultMaxRequests = 10;

    /// <summary>
    /// Refill rate in requests per second.
    /// Default: 10/60 = 0.167 requests/second.
    /// </summary>
    public const double DefaultRefillRate = 10.0 / 60.0;

    private readonly ILogger<DecisoreRateLimitFilter> _logger;

    public DecisoreRateLimitFilter(ILogger<DecisoreRateLimitFilter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Applies rate limiting before endpoint execution.
    /// </summary>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var rateLimiter = httpContext.RequestServices.GetService<RateLimitService>() as IRateLimitService
                          ?? httpContext.RequestServices.GetRequiredService<IRateLimitService>();

        // Build rate limit key from authenticated user
        var rateKey = GetRateLimitKey(httpContext);
        if (rateKey is null)
        {
            // User not authenticated (should not happen after RequireSessionFilter)
            return Results.Unauthorized();
        }

        // Check rate limit with Decisore-specific quota
        var result = await rateLimiter.CheckRateLimitAsync(
            rateKey,
            DefaultMaxRequests,
            DefaultRefillRate,
            httpContext.RequestAborted).ConfigureAwait(false);

        // Add observability headers
        httpContext.Response.Headers["X-RateLimit-Limit"] = DefaultMaxRequests.ToString(CultureInfo.InvariantCulture);
        httpContext.Response.Headers["X-RateLimit-Remaining"] = Math.Max(result.TokensRemaining, 0).ToString(CultureInfo.InvariantCulture);

        if (!result.Allowed)
        {
            httpContext.Response.Headers["Retry-After"] = result.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);

            _logger.LogWarning(
                "Decisore rate limit exceeded for user {UserId}. Retry after {RetryAfter}s (quota: {Quota}/min)",
                rateKey.Replace("decisore:expert:", string.Empty, StringComparison.Ordinal),
                result.RetryAfterSeconds,
                DefaultMaxRequests);

            var payload = new
            {
                error = "Rate limit exceeded",
                retryAfter = result.RetryAfterSeconds,
                message = $"Too many expert analysis requests. Limit: {DefaultMaxRequests} per minute. Please wait before retrying.",
                quota = new
                {
                    limit = DefaultMaxRequests,
                    remaining = 0,
                    resetAfterSeconds = result.RetryAfterSeconds
                }
            };

            return Results.Json(payload, statusCode: StatusCodes.Status429TooManyRequests);
        }

        return await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the rate limit key for Decisore expert analysis.
    /// Uses "decisore:expert:" prefix for separate bucket from other agent operations.
    /// </summary>
    private static string? GetRateLimitKey(HttpContext httpContext)
    {
        // Check for session authentication
        if (httpContext.Items.TryGetValue(nameof(SessionStatusDto), out var sessionObj) &&
            sessionObj is SessionStatusDto session &&
            session.User is not null)
        {
            return $"decisore:expert:{session.User.Id}";
        }

        // Check for API key authentication
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
        {
            return $"decisore:expert:{userId}";
        }

        return null;
    }
}
