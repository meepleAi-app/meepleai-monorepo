using System.Security.Claims;
using System.Text.Encodings.Web;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.Routing;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Authentication;

/// <summary>
/// Authentication handler that integrates SessionAuthenticationMiddleware with ASP.NET Core's authentication system.
///
/// Issue TEST-650 (#659): Fixes 500 errors when .RequireAuthorization() is called on endpoints.
///
/// Problem: Production code set DefaultChallengeScheme = null, causing InvalidOperationException when
/// ASP.NET tried to challenge unauthenticated requests (500 instead of 401/403).
///
/// Solution: Provide a proper authentication handler that:
/// 1. Validates session cookies via DDD CQRS ValidateSessionQuery (same as SessionAuthenticationMiddleware)
/// 2. Creates ClaimsPrincipal for authenticated users
/// 3. Returns NoResult for unauthenticated requests (allows proper 401 from authorization)
/// 4. Handles challenge (401) and forbidden (403) responses correctly
///
/// Architecture:
/// - SessionAuthenticationMiddleware: Runs first, populates HttpContext.Items[SessionStatusDto]
/// - SessionAuthenticationHandler: Runs during authentication, creates ClaimsPrincipal from session
/// - Both use the same ValidateSessionQuery via MediatR for consistency
/// - Endpoints using .RequireAuthorization() now get proper 401/403 instead of 500
///
/// Issue #1676 Phase 3: Migrated from ActiveSession (legacy) to SessionStatusDto (DDD)
/// </summary>
internal class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IMediator _mediator;

    public SessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IMediator mediator)
        : base(options, logger, encoder)
    {
        _mediator = mediator;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Skip authentication for non-API routes (static files, health checks, etc.)
        if (!Context.Request.Path.StartsWithSegments("/api", StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        if (Context.Items.TryGetValue(nameof(SessionStatusDto), out var cached) && cached is SessionStatusDto cachedSession)
        {
            return AuthenticateResult.Success(CreateTicketFromSessionStatus(cachedSession, Scheme.Name));
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
            // Validate session via DDD CQRS ValidateSessionQuery (same logic as SessionAuthenticationMiddleware)
            var query = new ValidateSessionQuery(SessionToken: token);
            var result = await _mediator.Send(query).ConfigureAwait(false);

            if (!result.IsValid || result.User == null)
            {
                // Invalid/expired session - return NoResult (allows proper 401)
                return AuthenticateResult.NoResult();
            }

            // Create claims from validated session
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, result.User.Id.ToString()),
                new(ClaimTypes.Email, result.User.Email),
                new(ClaimTypes.Role, result.User.Role)
            };

            if (!string.IsNullOrWhiteSpace(result.User.DisplayName))
            {
                claims.Add(new Claim(ClaimTypes.Name, result.User.DisplayName));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            // Store session in HttpContext.Items for endpoints that need it (DDD DTO format)
            // Issue #1676 Phase 3: Migrated from ActiveSession to SessionStatusDto
            Context.Items[nameof(SessionStatusDto)] = result;

            return AuthenticateResult.Success(ticket);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Authentication handler boundary - must not propagate exceptions
        // Any exception during authentication should result in NoResult to allow proper 401 response
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Session validation failed in SessionAuthenticationHandler");
            return AuthenticateResult.NoResult();
        }
#pragma warning restore CA1031
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

    private static AuthenticationTicket CreateTicketFromSessionStatus(SessionStatusDto sessionStatus, string schemeName)
    {
        if (sessionStatus.User == null)
        {
            throw new InvalidOperationException("Cannot create ticket from session status without user information");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sessionStatus.User.Id.ToString()),
            new(ClaimTypes.Email, sessionStatus.User.Email),
            new(ClaimTypes.Role, sessionStatus.User.Role)
        };

        if (!string.IsNullOrWhiteSpace(sessionStatus.User.DisplayName))
        {
            claims.Add(new Claim(ClaimTypes.Name, sessionStatus.User.DisplayName));
        }

        var identity = new ClaimsIdentity(claims, schemeName);
        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationTicket(principal, schemeName);
    }
}
