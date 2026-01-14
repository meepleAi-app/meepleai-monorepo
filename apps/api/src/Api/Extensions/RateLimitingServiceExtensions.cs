using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Extensions;

/// <summary>
/// ISSUE #2424: Rate Limiting Configuration for API Protection
///
/// Implements sliding window rate limiting with per-user/per-IP partitioning.
/// Protects API from abuse while allowing legitimate high-frequency usage.
///
/// Policies:
/// - SharedGamesAdmin: 100 req/min (authenticated admin operations)
/// - SharedGamesPublic: 300 req/min (public search operations)
/// - Default: 60 req/min (general API protection)
/// </summary>
internal static class RateLimitingServiceExtensions
{
    public static IServiceCollection AddRateLimitingServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRateLimiter(options =>
        {
            // Default policy: 60 req/min per IP (general API protection)
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var ipAddress = GetClientIpAddress(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: ipAddress,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 60,
                        SegmentsPerWindow = 6, // 10-second segments
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0, // No queueing (fail fast)
                    });
            });

            // Policy 1: SharedGamesAdmin - 100 req/min for authenticated admin operations
            options.AddPolicy("SharedGamesAdmin", httpContext =>
            {
                var userId = GetUserId(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: userId,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 100, // Higher limit for admin operations
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Policy 2: SharedGamesPublic - 300 req/min for public search operations
            options.AddPolicy("SharedGamesPublic", httpContext =>
            {
                var ipAddress = GetClientIpAddress(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: ipAddress,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 300, // High limit for legitimate search traffic
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Rejection behavior: Return 429 Too Many Requests
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                double? retryAfterSeconds = null;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue))
                {
                    retryAfterSeconds = retryAfterValue.TotalSeconds;
                    context.HttpContext.Response.Headers.RetryAfter = retryAfterValue.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        error = "Too Many Requests",
                        message = "Rate limit exceeded. Please try again later.",
                        retryAfterSeconds
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            };
        });

        return services;
    }

    /// <summary>
    /// Gets the client IP address from the HttpContext.
    /// Checks X-Forwarded-For header first (for proxied requests), then RemoteIpAddress.
    /// </summary>
    private static string GetClientIpAddress(HttpContext httpContext)
    {
        // Check X-Forwarded-For header (Traefik proxy sets this)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // Take first IP if multiple (client IP)
            var firstIp = forwardedFor.Split(',')[0].Trim();
            return firstIp;
        }

        // Fallback to RemoteIpAddress
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Gets the user ID from the HttpContext User claims.
    /// Returns IP address as fallback for unauthenticated requests.
    /// </summary>
    private static string GetUserId(HttpContext httpContext)
    {
        var userId = httpContext.User?.FindFirst("sub")?.Value
            ?? httpContext.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        return userId ?? GetClientIpAddress(httpContext);
    }
}
