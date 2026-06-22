using System.Security.Claims;
using Api.BoundedContexts.Administration.Application;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.Helpers;
using Api.Routing;
using Api.Services;
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

    public async Task InvokeAsync(HttpContext context, IMediator mediator, AuditService auditService)
    {
        _logger.LogDebug("[SessionAuth] InvokeAsync START - Path: {Path}, Method: {Method}",
            LogSanitizer.SanitizePath(context.Request.Path), LogSanitizer.Sanitize(context.Request.Method));

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

                    // SP5 S2 D-S2-4: an impersonation session whose ImpersonatedUntil window elapsed
                    // is rejected with an explicit 401 (NOT fail-open) and an ImpersonationAutoEnded
                    // audit row. Subsequent requests bearing the same cookie keep hitting this branch
                    // until the session is cleaned up. Audit is best-effort (EnqueueAuditAsync swallows
                    // failures) — a logging hiccup must not change the 401 outcome.
                    if (!result.IsValid && result.WasImpersonationAutoEnded)
                    {
                        _logger.LogWarning(
                            "[SessionAuth] Impersonation session auto-ended (expired): subject {SubjectId}, actor {ActorId}",
                            result.ImpersonationSubjectUserId, result.ImpersonationActorUserId);

                        await auditService.EnqueueAuditAsync(new AuditOutboxPayload
                        {
                            Action = "ImpersonationAutoEnded",
                            Resource = "Session",
                            // D-S2-3: user_id = subject (impersonated target), impersonated_user_id = actor (admin).
                            UserId = result.ImpersonationSubjectUserId?.ToString(),
                            ResourceId = result.SessionId?.ToString(),
                            Result = "Success",
                            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                            UserAgent = context.Request.Headers.UserAgent.FirstOrDefault(),
                            RequestType = nameof(SessionAuthenticationMiddleware),
                            Details = "{}",
                            ImpersonatedUserId = result.ImpersonationActorUserId,
                            StepUpTokenId = null,
                            Timestamp = DateTimeOffset.UtcNow,
                            Oversize = false,
                        }).ConfigureAwait(false);

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(
                            new { error = "impersonation_expired", message = "Impersonation session has expired." })
                            .ConfigureAwait(false);
                        return; // short-circuit the pipeline — do NOT call _next
                    }

                    if (result.IsValid && result.Principal?.Subject != null)
                    {
                        // Store session status in HttpContext.Items for endpoints (DDD DTO format)
                        // Issue #1676 Phase 3: Migrated from ActiveSession to SessionStatusDto
                        context.Items[nameof(SessionStatusDto)] = result;

                        // If no authenticated user is set, populate ClaimsPrincipal for observability and helpers
                        if (context.User?.Identity?.IsAuthenticated != true)
                        {
                            var claims = new List<Claim>
                            {
                                new(ClaimTypes.NameIdentifier, result.Principal!.Subject.Id.ToString()),
                                new(ClaimTypes.Email, result.Principal!.Subject.Email),
                                new(ClaimTypes.Role, result.Principal!.Subject.Role)
                            };

                            if (!string.IsNullOrWhiteSpace(result.Principal!.Subject.DisplayName))
                            {
                                claims.Add(new Claim(ClaimTypes.Name, result.Principal!.Subject.DisplayName));
                            }

                            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "SessionCookie"));
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // MIDDLEWARE BOUNDARY PATTERN: fail-open on session validation errors.
                // DB errors, crypto failures, malformed tokens and network timeouts must not crash
                // the request pipeline — a failed validation simply means the request proceeds as
                // unauthenticated. A previously narrow exception filter caused NpgsqlException and
                // TimeoutException to surface as HTTP 500 instead of returning 401.
                // OperationCanceledException is excluded: client disconnects are expected.
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