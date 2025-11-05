using System.Security.Claims;
using Api.Models;
using Api.Routing;
using Api.Services;

namespace Api.Middleware;

/// <summary>
/// Middleware that authenticates requests using the session cookie written by auth endpoints.
/// When a valid session token is found, stores an ActiveSession in HttpContext.Items
/// and enriches HttpContext.User with basic identity claims.
/// </summary>
public class SessionAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionAuthenticationMiddleware> _logger;

    public SessionAuthenticationMiddleware(RequestDelegate next, ILogger<SessionAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AuthService authService)
    {
        // Process only API routes
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            try
            {
                var cookieName = CookieHelpers.GetSessionCookieName(context);
                if (context.Request.Cookies.TryGetValue(cookieName, out var token) && !string.IsNullOrWhiteSpace(token))
                {
                    var session = await authService.ValidateSessionAsync(token);
                    if (session != null)
                    {
                        // Make session available to endpoints expecting it
                        context.Items[nameof(ActiveSession)] = session;

                        // If no authenticated user is set, populate ClaimsPrincipal for observability and helpers
                        if (context.User?.Identity?.IsAuthenticated != true)
                        {
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

                            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "SessionCookie"));
                        }
                    }
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                // MIDDLEWARE BOUNDARY PATTERN: Authentication middleware must not block requests on validation errors
                // Rationale: This middleware validates session cookies but must not crash the request pipeline if
                // validation fails (DB errors, crypto errors, malformed tokens). Failed authentication simply means
                // the request proceeds as unauthenticated. We log the error for monitoring but allow the request.
                // Context: Session validation involves DB queries and crypto operations that can fail
                _logger.LogWarning(ex, "Session cookie validation failed");
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        await _next(context);
    }
}

public static class SessionAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SessionAuthenticationMiddleware>();
    }
}

