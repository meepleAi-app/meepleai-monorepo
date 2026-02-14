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
/// - FaqUpvote: 10 req/min (FAQ upvoting, prevents vote manipulation)
/// - ShareRequestAdmin: 100 req/min (admin share request operations) - Issue #3098
/// - ShareRequestCreation: 30 req/min (creating share requests) - Issue #3098
/// - ShareRequestQuery: 100 req/min (querying share requests) - Issue #3098
/// - ShareRequestUpdate: 50 req/min (updating share requests) - Issue #3098
/// - UserDashboard: 100 req/min (user dashboard operations) - Issue #3098
/// - BggSearch: 20 req/hour (BoardGameGeek search operations) - Issue #3120
/// - ProposePrivateGame: 2 req/min (private game catalog proposals) - Issue #3665
/// - BulkImportAdmin: 1 req/5min (bulk import operations, Admin only) - Issue #4354
/// - Default: 60 req/min (general API protection)
/// </summary>
internal static class RateLimitingServiceExtensions
{
    public static IServiceCollection AddRateLimitingServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Issue #2705: Allow disabling rate limiting for integration tests
        // Issue #3102: Also check DISABLE_RATE_LIMITING env var as fallback for WebApplicationFactory tests
        // where configuration might not be applied yet during service registration
        var rateLimitingEnabled = configuration.GetValue("RateLimiting:Enabled", true);
        var disableRateLimitingEnvVar = Environment.GetEnvironmentVariable("DISABLE_RATE_LIMITING");
        var disabledByEnvVar = string.Equals(disableRateLimitingEnvVar, "true", StringComparison.OrdinalIgnoreCase);

        if (!rateLimitingEnabled || disabledByEnvVar)
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

                // Issue #3098: Share request rate limiting policies
                options.AddPolicy("ShareRequestAdmin", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));

                options.AddPolicy("ShareRequestCreation", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));

                options.AddPolicy("ShareRequestQuery", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));

                options.AddPolicy("ShareRequestUpdate", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));

                // Issue #3098: User dashboard rate limiting policy
                options.AddPolicy("UserDashboard", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));

                // Issue #3120: BGG search rate limiting policy
                options.AddPolicy("BggSearch", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));

                // Issue #3665: Private game proposal rate limiting policy
                options.AddPolicy("ProposePrivateGame", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));

                // Issue #4354: Bulk import rate limiting policy
                options.AddPolicy("BulkImportAdmin", _ =>
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

            // Policy 4: ShareRequestAdmin - 100 req/min for admin share request operations (Issue #3098)
            options.AddPolicy("ShareRequestAdmin", httpContext =>
            {
                var userId = GetUserId(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"share-request-admin-{userId}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 100, // Same as SharedGamesAdmin
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Policy 5: ShareRequestCreation - 30 req/min for creating share requests (Issue #3098)
            options.AddPolicy("ShareRequestCreation", httpContext =>
            {
                var userId = GetUserId(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"share-request-create-{userId}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 30, // Lower limit for create operations
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Policy 6: ShareRequestQuery - 100 req/min for querying share requests (Issue #3098)
            options.AddPolicy("ShareRequestQuery", httpContext =>
            {
                var userId = GetUserId(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"share-request-query-{userId}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 100, // Similar to admin operations
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Policy 7: ShareRequestUpdate - 50 req/min for updating share requests (Issue #3098)
            options.AddPolicy("ShareRequestUpdate", httpContext =>
            {
                var userId = GetUserId(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"share-request-update-{userId}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 50, // Moderate limit for update operations
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Policy 8: UserDashboard - 100 req/min for user dashboard operations (Issue #3098)
            options.AddPolicy("UserDashboard", httpContext =>
            {
                var userId = GetUserId(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"user-dashboard-{userId}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 100, // User-specific operations
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Policy 9: BggSearch - 20 req/hour for BGG search operations (Issue #3120)
            options.AddPolicy("BggSearch", httpContext =>
            {
                var userId = GetUserId(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"bgg-search-{userId}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromHours(1), // 1 hour window
                        PermitLimit = 20, // 20 searches per hour to respect BGG API
                        SegmentsPerWindow = 6, // 10-minute segments
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Policy 10: ProposePrivateGame - 2 req/min for proposing private games (Issue #3665)
            options.AddPolicy("ProposePrivateGame", httpContext =>
            {
                var userId = GetUserId(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"propose-private-game-{userId}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 2, // Low limit to prevent proposal spam
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Policy 11: BulkImportAdmin - 1 req/5min for bulk import operations (Issue #4354)
            options.AddPolicy("BulkImportAdmin", httpContext =>
            {
                var userId = GetUserId(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"bulk-import-{userId}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(5),
                        PermitLimit = 1, // Max 1 bulk import every 5 minutes
                        SegmentsPerWindow = 5, // 1-minute segments
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
