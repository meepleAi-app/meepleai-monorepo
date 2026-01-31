using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Services;

namespace Api.Filters;

/// <summary>
/// Endpoint filter that applies specific rate limiting for notification bulk operations.
/// Uses existing Redis-based rate limiting infrastructure with stricter limits.
///
/// Usage: Apply via .RequireNotificationRateLimit() fluent method on route endpoints.
///
/// Pattern:
/// - Uses user-specific rate limit key for authenticated users
/// - Applies fixed window: 10 requests per minute (configurable)
/// - Returns 429 Too Many Requests with Retry-After header when limited
/// - Logs rate limit violations for security monitoring
///
/// Example:
/// <code>
/// group.MapPost("/notifications/mark-all-read", handler)
///     .RequireNotificationRateLimit();
/// </code>
///
/// Issue: #2155 - Rate limiting for mark-all endpoint
/// </summary>
internal class NotificationRateLimitFilter : IEndpointFilter
{
    /// <summary>
    /// Maximum requests allowed per window (burst capacity).
    /// Default: 10 requests per minute.
    /// </summary>
    public const int DefaultMaxRequests = 10;

    /// <summary>
    /// Refill rate in requests per second.
    /// Default: 10/60 = 0.167 (10 tokens per minute).
    /// </summary>
    public const double DefaultRefillRate = 10.0 / 60.0;

    private readonly ILogger<NotificationRateLimitFilter> _logger;

    public NotificationRateLimitFilter(ILogger<NotificationRateLimitFilter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Applies rate limiting before endpoint execution.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context.</param>
    /// <param name="next">The next filter in the pipeline.</param>
    /// <returns>
    /// - Endpoint result if rate limit not exceeded
    /// - 429 Too Many Requests if rate limit exceeded
    /// </returns>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var rateLimiter = httpContext.RequestServices.GetService<RateLimitService>() as IRateLimitService
                          ?? httpContext.RequestServices.GetRequiredService<IRateLimitService>();

        // Build rate limit key based on authenticated user
        var rateKey = GetRateLimitKey(httpContext);
        if (rateKey is null)
        {
            // User not authenticated - should not happen as this filter is used after auth
            return Results.Unauthorized();
        }

        // Check rate limit with notification-specific limits
        var result = await rateLimiter.CheckRateLimitAsync(
            rateKey,
            DefaultMaxRequests,
            DefaultRefillRate,
            httpContext.RequestAborted).ConfigureAwait(false);

        // Add rate limit headers for observability
        httpContext.Response.Headers["X-RateLimit-Limit"] = DefaultMaxRequests.ToString(CultureInfo.InvariantCulture);
        httpContext.Response.Headers["X-RateLimit-Remaining"] = Math.Max(result.TokensRemaining, 0).ToString(CultureInfo.InvariantCulture);

        if (!result.Allowed)
        {
            httpContext.Response.Headers["Retry-After"] = result.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);

            _logger.LogWarning(
                "Notification rate limit exceeded for user {UserId}. Retry after {RetryAfter}s",
                rateKey.Replace("notifications:", string.Empty, StringComparison.Ordinal),
                result.RetryAfterSeconds);

            var payload = new
            {
                error = "Rate limit exceeded",
                retryAfter = result.RetryAfterSeconds,
                message = "Too many mark-all requests. Please wait before retrying."
            };

            return Results.Json(payload, statusCode: StatusCodes.Status429TooManyRequests);
        }

        return await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the rate limit key for the current request.
    /// Uses a notification-specific prefix for separate bucket from global rate limiting.
    /// </summary>
    private static string? GetRateLimitKey(HttpContext httpContext)
    {
        // Check for session authentication first
        if (httpContext.Items.TryGetValue(nameof(SessionStatusDto), out var sessionObj) &&
            sessionObj is SessionStatusDto session &&
            session.User is not null)
        {
            return $"notifications:{session.User.Id}";
        }

        // Check for API key authentication
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
        {
            return $"notifications:{userId}";
        }

        return null;
    }
}
