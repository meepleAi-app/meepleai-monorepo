using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.Extensions;
using System.Globalization;

namespace Api.Middleware;

/// <summary>
/// Middleware that enforces session quotas for session creation endpoints.
/// Issue #3671: Session limits enforcement with automatic termination.
///
/// <para><strong>Behavior:</strong></para>
/// <list type="bullet">
/// <item><description>Intercepts POST requests to /api/v1/games/{id}/sessions</description></item>
/// <item><description>Checks session quota before allowing request</description></item>
/// <item><description>Auto-terminates oldest sessions if quota exceeded</description></item>
/// <item><description>Admin/Editor roles bypass all quota checks</description></item>
/// <item><description>Fails open if quota service encounters errors (resilience)</description></item>
/// </list>
/// </summary>
internal class SessionQuotaMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionQuotaMiddleware> _logger;

    public SessionQuotaMiddleware(RequestDelegate next, ILogger<SessionQuotaMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISessionQuotaService quotaService)
    {
        try
        {
            // Filter: Only apply to session creation endpoints
            if (!IsSessionCreationRequest(context))
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            // Extract user identity from active session
            var (authenticated, session, _) = context.TryGetActiveSession();

            if (!authenticated || session?.User is null)
            {
                // Unauthenticated request - let authentication middleware handle
                await _next(context).ConfigureAwait(false);
                return;
            }

            var user = session.User;

            // Parse tier and role from strings to value objects
            var userTier = UserTier.Parse(user.Tier);
            var userRole = Role.Parse(user.Role);

            // Ensure quota (auto-terminates oldest sessions if needed)
            var result = await quotaService.EnsureQuotaAsync(
                user.Id,
                userTier,
                userRole,
                context.RequestAborted)
                .ConfigureAwait(false);

            // Add observability headers if sessions were terminated
            if (result.TerminatedSessionIds.Count > 0)
            {
                context.Response.Headers["X-Session-Quota-Terminated"] =
                    result.TerminatedSessionIds.Count.ToString(CultureInfo.InvariantCulture);
                context.Response.Headers["X-Session-Quota-Terminated-Ids"] =
                    string.Join(",", result.TerminatedSessionIds);

                _logger.LogInformation(
                    "Session quota middleware terminated {Count} session(s) for user {UserId}: {SessionIds}",
                    result.TerminatedSessionIds.Count,
                    user.Id,
                    string.Join(", ", result.TerminatedSessionIds));
            }

            // Continue to endpoint (quota is now ensured)
            await _next(context).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // MIDDLEWARE BOUNDARY PATTERN: Session quota middleware must fail-open to avoid self-DOS
            // Rationale: If quota service is down, don't block all session creation
            // Trade-off: Temporary quota bypass vs complete system unavailability
            _logger.LogWarning(ex,
                "Session quota middleware encountered an error; allowing request (fail-open)");
            await _next(context).ConfigureAwait(false);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Determines if the current request is a session creation endpoint.
    /// </summary>
    private static bool IsSessionCreationRequest(HttpContext context)
    {
        // Filter: POST /api/v1/games/{gameId}/sessions
        return context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && context.Request.Path.StartsWithSegments("/api/v1/games", StringComparison.Ordinal)
            && context.Request.Path.Value?.Contains("/sessions", StringComparison.Ordinal) == true;
    }
}

internal static class SessionQuotaMiddlewareExtensions
{
    /// <summary>
    /// Adds session quota enforcement middleware to the pipeline.
    /// Issue #3671: Must be placed after authentication/authorization middleware.
    /// </summary>
    public static IApplicationBuilder UseSessionQuotaEnforcement(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SessionQuotaMiddleware>();
    }
}
