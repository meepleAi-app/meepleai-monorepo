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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Session cookie validation failed");
            }
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

