using System.Security.Claims;
using System.Text.Encodings.Web;
using Api.Models;
using Api.Routing;
using Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Api.Authentication;

/// <summary>
/// Authentication handler that integrates SessionAuthenticationMiddleware with ASP.NET Core's authentication system.
///
/// Issue TEST-650 (#659): Fixes 500 errors when .RequireAuthorization() is called on endpoints.
///
/// Problem: Production code set DefaultChallengeScheme = null, causing InvalidOperationException when
/// ASP.NET tried to challenge unauthenticated requests (500 instead of 401/403).
///
/// Solution: Provide a proper authentication handler that:
/// 1. Validates session cookies via AuthService (same as SessionAuthenticationMiddleware)
/// 2. Creates ClaimsPrincipal for authenticated users
/// 3. Returns NoResult for unauthenticated requests (allows proper 401 from authorization)
/// 4. Handles challenge (401) and forbidden (403) responses correctly
///
/// Architecture:
/// - SessionAuthenticationMiddleware: Runs first, populates HttpContext.Items[ActiveSession]
/// - SessionAuthenticationHandler: Runs during authentication, creates ClaimsPrincipal from session
/// - Both use the same AuthService.ValidateSessionAsync() for consistency
/// - Endpoints using .RequireAuthorization() now get proper 401/403 instead of 500
/// </summary>
public class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AuthService _authService;

    public SessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AuthService authService)
        : base(options, logger, encoder)
    {
        _authService = authService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Skip authentication for non-API routes (static files, health checks, etc.)
        if (!Context.Request.Path.StartsWithSegments("/api"))
        {
            return AuthenticateResult.NoResult();
        }

        // Try to get session cookie
        var cookieName = CookieHelpers.GetSessionCookieName(Context);
        if (!Context.Request.Cookies.TryGetValue(cookieName, out var token) || string.IsNullOrWhiteSpace(token))
        {
            // No cookie present - return NoResult (allows proper 401 from authorization)
            return AuthenticateResult.NoResult();
        }

        try
        {
            // Validate session via AuthService (same logic as SessionAuthenticationMiddleware)
            var session = await _authService.ValidateSessionAsync(token);
            if (session == null)
            {
                // Invalid/expired session - return NoResult (allows proper 401)
                return AuthenticateResult.NoResult();
            }

            // Create claims from validated session
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, session.User.Id),
                new(ClaimTypes.Email, session.User.Email),
                new(ClaimTypes.Role, session.User.Role)
            };

            if (!string.IsNullOrWhiteSpace(session.User.DisplayName))
            {
                claims.Add(new Claim(ClaimTypes.Name, session.User.DisplayName!));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            // Store session in HttpContext.Items for endpoints that need it
            // (Consistent with SessionAuthenticationMiddleware behavior)
            Context.Items[nameof(ActiveSession)] = session;

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Session validation failed in SessionAuthenticationHandler");
            return AuthenticateResult.NoResult();
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // Return 401 Unauthorized without any redirect or additional processing
        // This is the correct behavior for API endpoints
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        // Return 403 Forbidden without any redirect
        // This is the correct behavior for API endpoints
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
