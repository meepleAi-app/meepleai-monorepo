using System.Security.Claims;
using System.Text.Json;
using Api.BoundedContexts.Authentication.Application.Services;
using Microsoft.Extensions.Configuration;

#pragma warning disable MA0048 // File name must match type name - middleware folder convention groups class + extensions

namespace Api.BoundedContexts.Authentication.Infrastructure.Middleware;

/// <summary>
/// Enforces email allowlist on staging environment (DevOps wave 1).
/// </summary>
/// <remarks>
/// Pipeline position: wired AFTER <c>UseAuthentication</c> + <c>UseAuthorization</c>
/// + <c>UseEmailVerificationEnforcement</c>, BEFORE the body-reset middleware in
/// <see cref="Api.Extensions.WebApplicationExtensions"/>.
///
/// Activation: only registered when <c>app.Environment.IsEnvironment("Staging")</c>
/// (matches <c>ASPNETCORE_ENVIRONMENT=Staging</c> in <c>compose.staging.yml</c>).
///
/// Behavior:
/// - Unauthenticated requests pass through (auth middleware handles those)
/// - Authenticated user without email claim → 403 (fail-safe deny on staging)
/// - Authenticated user with email NOT in allowlist → 403 with structured JSON
/// - Authenticated user with email in allowlist → pass-through
/// - Empty allowlist → pass-through (default safe; warning logged at startup)
///
/// Response on deny:
/// <code>
/// {
///   "code": "STAGING_ACCESS_DENIED",
///   "message": "Staging access by invite only",
///   "contactEmail": "badsworm@gmail.com"
/// }
/// </code>
/// </remarks>
internal sealed class StagingAccessMiddleware
{
    /// <summary>
    /// Fallback contact when <c>Staging:ContactEmail</c> is not configured.
    /// Project owner per <c>devops-policy.md §4</c>.
    /// </summary>
    private const string DefaultContactEmail = "badsworm@gmail.com";

    private readonly RequestDelegate _next;
    private readonly string _contactEmail;

    public StagingAccessMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        var configured = configuration["Staging:ContactEmail"];
        _contactEmail = string.IsNullOrWhiteSpace(configured) ? DefaultContactEmail : configured;
    }

    public async Task InvokeAsync(HttpContext context, IStagingAccessGuard guard)
    {
        // Skip unauthenticated requests — auth middleware handles those
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var email = context.User.FindFirstValue(ClaimTypes.Email);
        if (!guard.IsEmailAllowed(email ?? string.Empty))
        {
            await WriteDeniedAsync(context).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private async Task WriteDeniedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";
        // Contact email comes from Staging:ContactEmail config (fallback: project owner).
        // Embedded in user-facing message so existing frontend error handler
        // (login _content.tsx:93 setError(err.message)) displays it without
        // code-specific branching. The structured `code` and `contactEmail` fields
        // remain for future typed handling.
        var payload = JsonSerializer.Serialize(new
        {
            code = "STAGING_ACCESS_DENIED",
            message = $"Staging access by invite only — contact {_contactEmail} to request access.",
            contactEmail = _contactEmail
        });
        await context.Response.WriteAsync(payload).ConfigureAwait(false);
    }
}

internal static class StagingAccessMiddlewareExtensions
{
    public static IApplicationBuilder UseStagingAccessGuard(this IApplicationBuilder app)
    {
        return app.UseMiddleware<StagingAccessMiddleware>();
    }
}
