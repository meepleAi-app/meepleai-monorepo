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
    public static IServiceCollection AddRateLimitingServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Issue #2705: Allow disabling rate limiting for integration tests
        var rateLimitingEnabled = configuration.GetValue("RateLimiting:Enabled", true);
        if (!rateLimitingEnabled)
        {
            // Register a permissive rate limiter that allows all requests
            services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));

                options.AddPolicy("SharedGamesAdmin", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));

                options.AddPolicy("SharedGamesPublic", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));

                options.AddPolicy("FaqUpvote", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));
            });

            return services;
        }

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

            // Policy 3: FaqUpvote - 10 req/min per IP for FAQ upvoting (Issue #2681)
            options.AddPolicy("FaqUpvote", httpContext =>
            {
                var ipAddress = GetClientIpAddress(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"faq-upvote-{ipAddress}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 10, // Low limit to prevent vote manipulation
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
    /// Defensive implementation with full null checks to prevent rate limiter crashes.
    /// </summary>
    private static string GetClientIpAddress(HttpContext? httpContext)
    {
        const string fallback = "unknown";

        if (httpContext is null)
        {
            return fallback;
        }

        try
        {
            // Check X-Forwarded-For header (Traefik proxy sets this)
            var request = httpContext.Request;
            if (request is not null)
            {
                var forwardedFor = request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(forwardedFor))
                {
                    // Take first IP if multiple (client IP)
                    var firstIp = forwardedFor.Split(',')[0].Trim();
                    if (!string.IsNullOrWhiteSpace(firstIp))
                    {
                        return firstIp;
                    }
                }
            }

            // Fallback to RemoteIpAddress with full null check
            var connection = httpContext.Connection;
            if (connection is not null)
            {
                var remoteIp = connection.RemoteIpAddress;
                if (remoteIp is not null)
                {
                    return remoteIp.ToString();
                }
            }

            return fallback;
        }
        catch
        {
            // Defensive: never let rate limiter partition key throw
            return fallback;
        }
    }

    /// <summary>
    /// Gets the user ID from the HttpContext User claims.
    /// Returns IP address as fallback for unauthenticated requests.
    /// Defensive implementation with full null checks to prevent rate limiter crashes.
    /// </summary>
    private static string GetUserId(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return "unknown";
        }

        try
        {
            var user = httpContext.User;
            if (user is not null)
            {
                var userId = user.FindFirst("sub")?.Value
                    ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

                if (!string.IsNullOrWhiteSpace(userId))
                {
                    return userId;
                }
            }

            return GetClientIpAddress(httpContext);
        }
        catch
        {
            // Defensive: never let rate limiter partition key throw
            return GetClientIpAddress(httpContext);
        }
    }
}
