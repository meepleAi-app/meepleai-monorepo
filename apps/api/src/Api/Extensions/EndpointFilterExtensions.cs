using Api.Filters;

namespace Api.Extensions;

/// <summary>
/// Extension methods for applying endpoint filters to route endpoints.
/// Provides fluent API for common authentication and authorization patterns.
///
/// Issue: #1446 - Session Validation Middleware
/// </summary>
public static class EndpointFilterExtensions
{
    /// <summary>
    /// Requires an active session for the endpoint.
    /// Automatically validates session before endpoint execution.
    ///
    /// The validated ActiveSession is available in HttpContext.Items[nameof(ActiveSession)].
    /// </summary>
    /// <typeparam name="TBuilder">The route handler builder type.</typeparam>
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
    /// <typeparam name="TBuilder">The route handler builder type.</typeparam>
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
    /// <typeparam name="TBuilder">The route handler builder type.</typeparam>
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
    ///     else if (context.User.Identity?.IsAuthenticated == true)
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
}
