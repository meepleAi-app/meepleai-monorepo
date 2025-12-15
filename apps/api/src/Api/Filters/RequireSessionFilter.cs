using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
using Api.Infrastructure.Entities;

namespace Api.Filters;

/// <summary>
/// Endpoint filter that validates active session before endpoint execution.
/// Eliminates repetitive session validation code across 64+ endpoint occurrences.
///
/// Usage: Apply via .RequireSession() fluent method on route endpoints.
///
/// Pattern:
/// - Automatically validates session exists and is active
/// - Stores validated SessionStatusDto in HttpContext.Items for endpoint access
/// - Returns 401 Unauthorized if validation fails
/// - Opt-in approach prevents breaking changes
///
/// Example:
/// <code>
/// group.MapGet("/endpoint", async (HttpContext context) =>
/// {
///     // Session is already validated and available in HttpContext.Items
///     var session = context.Items[nameof(SessionStatusDto)] as SessionStatusDto;
///     // Use session...
/// })
/// .RequireSession();
/// </code>
///
/// Note: Streaming endpoints (SSE) that need session for mid-stream logging should
/// continue using manual validation due to their specialized error handling requirements.
///
/// Related: SessionValidationExtensions.cs provides TryGetActiveSession() used internally
/// Issue: #1446, #1676 Phase 3
/// </summary>
internal class RequireSessionFilter : IEndpointFilter
{
    /// <summary>
    /// Validates active session before endpoint execution.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context.</param>
    /// <param name="next">The next filter in the pipeline.</param>
    /// <returns>
    /// - Endpoint result if session is valid
    /// - 401 Unauthorized if session validation fails
    /// </returns>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Use existing validation extension method
        var (authenticated, _, error) = httpContext.TryGetActiveSession();

        if (!authenticated)
        {
            return error;
        }

        // Session was validated by TryGetActiveSession() extension method
        // which checks HttpContext.Items[nameof(SessionStatusDto)]
        // Endpoint can safely retrieve session from HttpContext.Items
        return await next(context).ConfigureAwait(false);
    }
}
