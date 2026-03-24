using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.Routing;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - Contains Middleware with Options/Extensions
namespace Api.Middleware;

/// <summary>
/// Middleware that authenticates requests using the session cookie written by auth endpoints.
/// When a valid session token is found, stores a SessionStatusDto in HttpContext.Items
/// and enriches HttpContext.User with basic identity claims.
///
/// Issue #1676 Phase 3: Migrated from ActiveSession (legacy) to SessionStatusDto (DDD)
/// </summary>
internal class SessionAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionAuthenticationMiddleware> _logger;

    public SessionAuthenticationMiddleware(RequestDelegate next, ILogger<SessionAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IMediator mediator)
    {
        _logger.LogDebug("[SessionAuth] InvokeAsync START - Path: {Path}, Method: {Method}",
            LogValueSanitizer.SanitizePath(context.Request.Path), LogValueSanitizer.Sanitize(context.Request.Method));

        // Process only API routes
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.Ordinal))
        {
            try
            {
                var cookieName = CookieHelpers.GetSessionCookieName(context);
                if (context.Request.Cookies.TryGetValue(cookieName, out var token) && !string.IsNullOrWhiteSpace(token))
                {
                    _logger.LogDebug("[SessionAuth] Cookie found, validating session token (length: {Length})", token.Length);

                    // Validate session via DDD CQRS ValidateSessionQuery
                    var query = new ValidateSessionQuery(SessionToken: token);
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var result = await mediator.Send(query).ConfigureAwait(false);
                    stopwatch.Stop();

                    _logger.LogInformation("[SessionAuth] ValidateSessionQuery completed in {ElapsedMs}ms - IsValid: {IsValid}",
                        stopwatch.ElapsedMilliseconds, result.IsValid);

                    if (result.IsValid && result.User != null)
                    {
                        // Store session status in HttpContext.Items for endpoints (DDD DTO format)
                        // Issue #1676 Phase 3: Migrated from ActiveSession to SessionStatusDto
                        context.Items[nameof(SessionStatusDto)] = result;

                        // If no authenticated user is set, populate ClaimsPrincipal for observability and helpers
                        if (context.User?.Identity?.IsAuthenticated != true)
                        {
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

                            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "SessionCookie"));
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Security.SecurityException or FormatException)
            {
                // MIDDLEWARE BOUNDARY PATTERN: Authentication middleware must not block requests on validation errors
                // Rationale: This middleware validates session cookies but must not crash the request pipeline if
                // validation fails (DB errors, crypto errors, malformed tokens). Failed authentication simply means
                // the request proceeds as unauthenticated. We log the error for monitoring but allow the request.
                // Context: Session validation involves DB queries and crypto operations that can fail
                _logger.LogWarning(ex, "Session cookie validation failed");
            }
        }

        await _next(context).ConfigureAwait(false);
    }
}

internal static class SessionAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SessionAuthenticationMiddleware>();
    }
}