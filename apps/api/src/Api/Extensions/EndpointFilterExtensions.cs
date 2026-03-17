using Api.Filters;

namespace Api.Extensions;

/// <summary>
/// Extension methods for applying endpoint filters to route endpoints.
/// Provides fluent API for common authentication and authorization patterns.
///
/// Issue: #1446 - Session Validation Middleware
/// </summary>
internal static class EndpointFilterExtensions
{
    /// <summary>
    /// Requires an active session for the endpoint.
    /// Automatically validates session before endpoint execution.
    ///
    /// The validated ActiveSession is available in HttpContext.Items[nameof(ActiveSession)].
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <example>
    /// <code>
    /// group.MapGet("/endpoint", async (HttpContext context) =>
    /// {
    ///     var session = context.Items[nameof(ActiveSession)] as ActiveSession;
    ///     // Use session...
    /// })
    /// .RequireSession();
    /// </code>
    /// </example>
    public static RouteHandlerBuilder RequireSession(this RouteHandlerBuilder builder)

    {
        return builder.AddEndpointFilter<RequireSessionFilter>();
    }

    /// <summary>
    /// Requires an active session with Admin role for the endpoint.
    /// Automatically validates session and Admin role before endpoint execution.
    ///
    /// Returns 401 Unauthorized if session is invalid, 403 Forbidden if not Admin.
    /// The validated ActiveSession is available in HttpContext.Items[nameof(ActiveSession)].
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <example>
    /// <code>
    /// group.MapPost("/admin/endpoint", async (HttpContext context) =>
    /// {
    ///     // Session is validated AND user is Admin
    ///     var session = context.Items[nameof(ActiveSession)] as ActiveSession;
    ///     // Perform admin operations...
    /// })
    /// .RequireAdminSession();
    /// </code>
    /// </example>
    public static RouteHandlerBuilder RequireAdminSession(this RouteHandlerBuilder builder)

    {
        return builder.AddEndpointFilter<RequireAdminSessionFilter>();
    }

    /// <summary>
    /// Requires authentication via session OR API key for the endpoint.
    /// Supports dual authentication modes for maximum flexibility.
    ///
    /// The validated ActiveSession (if session auth) is available in HttpContext.Items[nameof(ActiveSession)].
    /// If API key auth, user claims are available via HttpContext.User.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <example>
    /// <code>
    /// group.MapGet("/endpoint", async (HttpContext context) =>
    /// {
    ///     // User is authenticated (either session or API key)
    ///     var session = context.Items[nameof(ActiveSession)] as ActiveSession;
    ///     if (session != null)
    ///     {
    ///         // Session authentication
    ///     }
    ///     else if (context.User.Identity?.IsAuthenticated is true)
    ///     {
    ///         // API key authentication
    ///     }
    /// })
    /// .RequireAuthenticatedUser();
    /// </code>
    /// </example>
    public static RouteHandlerBuilder RequireAuthenticatedUser(this RouteHandlerBuilder builder)

    {
        return builder.AddEndpointFilter<RequireAuthenticatedUserFilter>();
    }

    /// <summary>
    /// Applies notification-specific rate limiting for bulk operations.
    /// Uses stricter limits than global rate limiting to prevent abuse.
    ///
    /// Default limits: 10 requests per minute (fixed window).
    /// Returns 429 Too Many Requests when limit exceeded.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <example>
    /// <code>
    /// group.MapPost("/notifications/mark-all-read", handler)
    ///     .RequireNotificationRateLimit();
    /// </code>
    /// </example>
    /// <remarks>
    /// Issue: #2155 - Rate limiting for mark-all endpoint
    /// </remarks>
    public static RouteHandlerBuilder RequireNotificationRateLimit(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<NotificationRateLimitFilter>();
    }

    /// <summary>
    /// Requires access to a specific feature flag for the endpoint.
    /// Checks both role-based and tier-based access using FeatureFlagService.
    ///
    /// Must be used after .RequireSession() to ensure session is available.
    /// Returns 403 Forbidden if the user's role+tier doesn't allow the feature.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="featureName">The feature flag name to check (e.g., "advanced_rag").</param>
    /// <returns>The builder for method chaining.</returns>
    /// <example>
    /// <code>
    /// group.MapGet("/advanced-search", handler)
    ///     .RequireSession()
    ///     .RequireFeature("advanced_rag");
    /// </code>
    /// </example>
    /// <remarks>
    /// Issue: #3674 - Feature Flag Tier-Based Access Verification
    /// </remarks>
    public static RouteHandlerBuilder RequireFeature(this RouteHandlerBuilder builder, string featureName)
    {
        return builder.AddEndpointFilter(new RequireFeatureFilterFactory(featureName));
    }

    /// <summary>
    /// Requires public registration to be enabled for the endpoint.
    /// Blocks with 403 if Registration:PublicEnabled is false or unreachable (fail-closed).
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The builder for method chaining.</returns>
    public static RouteHandlerBuilder RequirePublicRegistration(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<RequirePublicRegistrationFilter>();
    }
}
