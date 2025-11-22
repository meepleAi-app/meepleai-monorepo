using Api.Extensions;

namespace Api.Filters;

/// <summary>
/// Endpoint filter that validates authentication via either session OR API key.
/// Supports dual authentication modes for maximum flexibility.
///
/// Usage: Apply via .RequireAuthenticatedUser() fluent method on route endpoints.
///
/// Pattern:
/// - Validates authentication via session cookie OR API key header
/// - Returns 401 Unauthorized if neither authentication method is valid
/// - Session is available in HttpContext.Items[nameof(ActiveSession)] if session auth
/// - User claims are available via HttpContext.User if API key auth
///
/// Example:
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
///
/// Related: RequireSessionFilter, SessionValidationExtensions
/// Issue: #1446 (Future Enhancement)
/// </summary>
public class RequireAuthenticatedUserFilter : IEndpointFilter
{
    /// <summary>
    /// Validates authentication via session or API key before endpoint execution.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context.</param>
    /// <param name="next">The next filter in the pipeline.</param>
    /// <returns>
    /// - Endpoint result if authentication is valid (session or API key)
    /// - 401 Unauthorized if neither authentication method is valid
    /// </returns>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Check for session OR API key authentication
        var (authenticated, session, error) = httpContext.TryGetAuthenticatedUser();

        if (!authenticated)
        {
            return error;
        }

        // User is authenticated (either session or API key)
        // Endpoint can check HttpContext.Items[nameof(ActiveSession)] for session
        // or HttpContext.User for API key authentication
        return await next(context);
    }
}
