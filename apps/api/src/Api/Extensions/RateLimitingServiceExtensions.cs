using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Extensions;

/// <summary>
/// ISSUE #2424: Rate Limiting Configuration for API Protection
///
/// Design principle: Rate limiting targets only operations with genuine cost or abuse potential.
/// Simple read-only DB queries (library, games list, etc.) are NOT rate limited globally —
/// only expensive or abuse-prone operations have per-endpoint policies.
///
/// NO global rate limit is configured: only endpoints decorated with .RequireRateLimiting()
/// are subject to throttling. This prevents the Next.js proxy (single IP) from exhausting
/// a shared global quota that was never meant to protect read-only endpoints.
///
/// Per-endpoint policies (applied via .RequireRateLimiting("PolicyName")):
/// - SharedGamesAdmin:    100 req/min  — authenticated admin write operations
/// - SharedGamesPublic:   300 req/min  — public catalog search (IP-based)
/// - FaqUpvote:            10 req/min  — prevents vote manipulation (IP-based)
/// - ShareRequestAdmin:   100 req/min  — admin share request operations
/// - ShareRequestCreation: 30 req/min  — creating share requests
/// - ShareRequestQuery:   100 req/min  — querying share requests
/// - ShareRequestUpdate:   50 req/min  — updating share requests
/// - UserDashboard:       100 req/min  — user dashboard operations
/// - BggSearch:            20 req/hour — respects BoardGameGeek external API quota
/// - ProposePrivateGame:    2 req/min  — prevents catalog proposal spam
/// - BulkImportAdmin:       1 req/5min — heavy admin batch operation
/// - AgentQuery:           30 req/min  — AI query (expensive, per user)
/// - AgentCreation:        10 req/min  — AI agent creation (per user)
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
            // Register a permissive rate limiter that allows all requests (used in tests)
            services.AddRateLimiter(options =>
            {
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

                // Issue #4338: Agent query rate limiting policy
                options.AddPolicy("AgentQuery", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));

                // Issue #4683: Agent creation rate limiting policy
                options.AddPolicy("AgentCreation", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));

                // SEC-05: Auth rate limiting policies (disabled in tests)
                options.AddPolicy("AuthLogin", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));

                options.AddPolicy("AuthRegister", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));

                options.AddPolicy("AuthPasswordReset", _ =>
                    RateLimitPartition.GetNoLimiter<string>("unlimited"));
            });

            return services;
        }

        services.AddRateLimiter(options =>
        {
            // No GlobalLimiter: only endpoints with .RequireRateLimiting() are throttled.
            // Simple read-only DB queries have no rate limit — only costly/abuse-prone
            // operations (AI queries, BGG search, catalog proposals, bulk imports) do.

            // Policy 1: SharedGamesAdmin - 100 req/min for authenticated admin operations
            options.AddPolicy("SharedGamesAdmin", httpContext =>
            {
                var userId = GetUserId(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: userId,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 100,
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
                        PermitLimit = 300,
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
                        PermitLimit = 10,
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
                        PermitLimit = 100,
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
                        PermitLimit = 30,
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
                        PermitLimit = 100,
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
                        PermitLimit = 50,
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
                        PermitLimit = 100,
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Policy 9: BggSearch - 20 req/hour for BGG search operations (Issue #3120)
            // Low limit to respect BoardGameGeek's external API quota and avoid being blocked.
            options.AddPolicy("BggSearch", httpContext =>
            {
                var userId = GetUserId(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"bgg-search-{userId}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromHours(1),
                        PermitLimit = 20,
                        SegmentsPerWindow = 6,
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
                        PermitLimit = 2,
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
                        PermitLimit = 1,
                        SegmentsPerWindow = 5,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Policy 12: AgentQuery - 30 req/min for AI agent query operations (Issue #4338)
            // AI queries are expensive (LLM token cost); per-user limit prevents runaway usage.
            options.AddPolicy("AgentQuery", httpContext =>
            {
                var userId = GetUserId(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"agent-query-{userId}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 30,
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Policy 13: AgentCreation - 10 req/min for agent creation (Issue #4683)
            options.AddPolicy("AgentCreation", httpContext =>
            {
                var userId = GetUserId(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"agent-creation-{userId}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 10,
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // SEC-05: Policy 14: AuthLogin - 10 req/min per IP for login attempts
            // Prevents brute-force password attacks.
            options.AddPolicy("AuthLogin", httpContext =>
            {
                var ipAddress = GetClientIpAddress(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"auth-login-{ipAddress}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 10,
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // SEC-05: Policy 15: AuthRegister - 5 req/min per IP for registration
            // Prevents mass account creation.
            options.AddPolicy("AuthRegister", httpContext =>
            {
                var ipAddress = GetClientIpAddress(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"auth-register-{ipAddress}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 5,
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // SEC-05: Policy 16: AuthPasswordReset - 5 req/min per IP for password reset
            // Prevents password reset abuse and email flooding.
            options.AddPolicy("AuthPasswordReset", httpContext =>
            {
                var ipAddress = GetClientIpAddress(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"auth-password-reset-{ipAddress}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 5,
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Rejection behavior: Return 429 Too Many Requests with retry-after info
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
    /// SEC-06: Uses only RemoteIpAddress (already rewritten by ForwardedHeaders middleware).
    /// Raw X-Forwarded-For is NOT read directly to prevent IP spoofing bypass.
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
            // SEC-06: Only use RemoteIpAddress which is already resolved by
            // ForwardedHeaders middleware from trusted proxies.
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
            return GetClientIpAddress(httpContext);
        }
    }
}
