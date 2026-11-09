using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Api.Middleware;

/// <summary>
/// Rejects a browser request to a SignalR hub (<c>/hubs/...</c>) coming from an origin the <c>"web"</c> CORS
/// policy does not allow (#4962).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why.</b> Hub connections are authenticated by the session cookie, and in Production/Staging that cookie
/// is <c>SameSite=None</c>. CORS does not apply to a WebSocket: a page on another site can skip the negotiate
/// (which CORS does protect) and open the WebSocket directly, and the browser attaches the victim's cookie. The
/// connection would then join the live sessions the victim plays in (Cross-Site WebSocket Hijacking).
/// </para>
/// <para>
/// <b>The rule.</b> A browser always sends <c>Origin</c> on a WebSocket handshake and on a cross-origin fetch.
/// If it is there, it must be an origin of the <c>"web"</c> policy — the same list <c>app.UseCors("web")</c>
/// enforces on <c>/api</c>, read from <see cref="ICorsPolicyProvider"/> so there is one list, not two. A request
/// without <c>Origin</c> is not a browser carrying someone else's cookie, and goes on.
/// </para>
/// </remarks>
internal sealed class HubOriginGuardMiddleware
{
    internal const string PolicyName = "web";

    private readonly RequestDelegate _next;
    private readonly ILogger<HubOriginGuardMiddleware> _logger;

    public HubOriginGuardMiddleware(RequestDelegate next, ILogger<HubOriginGuardMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICorsPolicyProvider policies)
    {
        if (context.Request.Path.StartsWithSegments("/hubs", StringComparison.Ordinal)
            && context.Request.Headers.Origin is { Count: > 0 } origin
            && !await IsAllowedAsync(context, policies, origin.ToString()).ConfigureAwait(false))
        {
            _logger.LogWarning(
                "Hub request refused: origin {Origin} is not allowed by the {Policy} CORS policy",
                Api.Helpers.LogSanitizer.Sanitize(origin.ToString()),
                PolicyName);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private static async Task<bool> IsAllowedAsync(HttpContext context, ICorsPolicyProvider policies, string origin)
    {
        var policy = await policies.GetPolicyAsync(context, PolicyName).ConfigureAwait(false);
        return policy is not null && policy.IsOriginAllowed(origin);
    }
}
