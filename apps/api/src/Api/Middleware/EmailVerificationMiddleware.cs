using Api.Extensions;
using System.Text.Json;

namespace Api.Middleware;

/// <summary>
/// Middleware that enforces email verification for authenticated users.
/// Issue #3672: Email verification flow with 7-day grace period.
///
/// <para><strong>Behavior:</strong></para>
/// <list type="bullet">
/// <item><description>Skips unauthenticated requests (handled by auth middleware)</description></item>
/// <item><description>Skips exempt endpoints (login, register, verify-email, etc.)</description></item>
/// <item><description>Skips users in grace period (7 days for existing users)</description></item>
/// <item><description>Skips Admin/Editor roles (always exempt)</description></item>
/// <item><description>Blocks unverified users past grace period with 403</description></item>
/// <item><description>Fails open if verification check encounters errors (resilience)</description></item>
/// </list>
/// </summary>
internal class EmailVerificationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<EmailVerificationMiddleware> _logger;

    /// <summary>
    /// Exempt paths that don't require email verification.
    /// </summary>
    private static readonly HashSet<string> ExemptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/v1/auth/login",
        "/api/v1/auth/register",
        "/api/v1/auth/verify-email",
        "/api/v1/auth/resend-verification",
        "/api/v1/auth/email/verify",       // Alternative path
        "/api/v1/auth/email/resend",       // Alternative path
        "/api/v1/auth/oauth/google/login",     // OAuth login endpoints
        "/api/v1/auth/oauth/google/callback",
        "/api/v1/auth/oauth/github/login",
        "/api/v1/auth/oauth/github/callback",
        "/api/v1/auth/oauth/discord/login",
        "/api/v1/auth/oauth/discord/callback",
        "/api/v1/health",                  // Health checks
        "/api/v1/health/ready",
        "/api/v1/health/live"
    };

    public EmailVerificationMiddleware(RequestDelegate next, ILogger<EmailVerificationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, TimeProvider timeProvider)
    {
        // Evaluate verification logic in isolation — _next is called exactly once, outside the try/catch.
        // This prevents the catch from intercepting exceptions thrown by downstream middleware/endpoints,
        // which would cause double-invocation of _next and a 500 response.
        var blockResult = EvaluateVerification(context, timeProvider);

        if (blockResult is not null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(blockResult),
                context.RequestAborted).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Evaluates whether the request should be blocked for email verification.
    /// Returns the error payload to write if blocked, or null to allow the request through.
    /// Fails open: any unexpected error returns null (allow).
    /// </summary>
    private object? EvaluateVerification(HttpContext context, TimeProvider timeProvider)
    {
        try
        {
            // Skip exempt endpoints (login, register, verification endpoints, health)
            if (IsExemptPath(context.Request.Path))
                return null;

            // Extract user identity from active session
            var (authenticated, session, _) = context.TryGetActiveSession();

            if (!authenticated || session?.User is null)
                return null;

            var user = session.User;

            // Admin, SuperAdmin, and Editor roles are always exempt from email verification
            if (user.Role.Equals("superadmin", StringComparison.OrdinalIgnoreCase) ||
                user.Role.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                user.Role.Equals("editor", StringComparison.OrdinalIgnoreCase))
                return null;

            // Check if email is already verified
            if (user.EmailVerified)
                return null;

            // Check if user is in grace period using TimeProvider for testability
            var gracePeriodEndsAt = user.VerificationGracePeriodEndsAt;
            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (gracePeriodEndsAt.HasValue && gracePeriodEndsAt.Value > now)
            {
                // In grace period - allow request but add informational header
                context.Response.Headers["X-Email-Verification-Grace-Period"] =
                    gracePeriodEndsAt.Value.ToString("O"); // ISO 8601 format
                return null;
            }

            // User requires verification and is past grace period - block request
            _logger.LogWarning(
                "Blocking request for unverified user {UserId} (email: {Email}, grace period ended: {GracePeriodEnded})",
                user.Id,
                user.Email,
                gracePeriodEndsAt?.ToString("O") ?? "never set");

            return new
            {
                error = "Email verification required",
                message = "Please verify your email address to access this feature. " +
                         "Check your inbox for the verification email or request a new one.",
                verificationStatus = new
                {
                    isVerified = false,
                    emailAddress = user.Email,
                    gracePeriodEnded = gracePeriodEndsAt?.ToString("O"),
                    resendEndpoint = "/api/v1/auth/email/resend"
                }
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // MIDDLEWARE BOUNDARY PATTERN: Email verification middleware must fail-open to avoid self-DOS
            // Rationale: If verification check fails, don't block all API access
            // Trade-off: Temporary verification bypass vs complete system unavailability
            _logger.LogWarning(ex,
                "Email verification middleware encountered an error; allowing request (fail-open)");
            return null;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Determines if the current request path is exempt from email verification.
    /// </summary>
    private static bool IsExemptPath(PathString path)
    {
        return ExemptPaths.Contains(path.Value ?? string.Empty);
    }
}

internal static class EmailVerificationMiddlewareExtensions
{
    /// <summary>
    /// Adds email verification enforcement middleware to the pipeline.
    /// Issue #3672: Must be placed after authentication/authorization middleware.
    /// </summary>
    public static IApplicationBuilder UseEmailVerificationEnforcement(this IApplicationBuilder app)
    {
        return app.UseMiddleware<EmailVerificationMiddleware>();
    }
}
