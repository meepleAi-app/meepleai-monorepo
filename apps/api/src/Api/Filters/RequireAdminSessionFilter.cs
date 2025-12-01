using Api.Extensions;
using Api.Models;

namespace Api.Filters;

/// <summary>
/// Endpoint filter that validates active session AND Admin role before endpoint execution.
/// Combines session validation with role-based authorization in a single filter.
///
/// Usage: Apply via .RequireAdminSession() fluent method on route endpoints.
///
/// Pattern:
/// - Validates session exists and is active (401 Unauthorized if not)
/// - Validates user has Admin role (403 Forbidden if not)
/// - Stores validated ActiveSession in HttpContext.Items for endpoint access
///
/// Example:
/// <code>
/// group.MapPost("/admin/endpoint", async (HttpContext context) =>
/// {
///     // Session is validated AND user is Admin
///     var session = context.Items[nameof(ActiveSession)] as ActiveSession;
///     // Perform admin operations...
/// })
/// .RequireAdminSession();
/// </code>
///
/// Related: RequireSessionFilter, SessionValidationExtensions
/// Issue: #1446 (Future Enhancement)
/// </summary>
public class RequireAdminSessionFilter : IEndpointFilter
{
    /// <summary>
    /// Validates active session and Admin role before endpoint execution.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context.</param>
    /// <param name="next">The next filter in the pipeline.</param>
    /// <returns>
    /// - Endpoint result if session is valid and user is Admin
    /// - 401 Unauthorized if session validation fails
    /// - 403 Forbidden if user does not have Admin role
    /// </returns>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Validate session exists and is active
        var (authorized, _, error) = httpContext.RequireAdminSession();

        if (!authorized)
        {
            return error;
        }

        // Session is validated and user has Admin role
        // Endpoint can safely retrieve session from HttpContext.Items
        return await next(context).ConfigureAwait(false);
    }
}
