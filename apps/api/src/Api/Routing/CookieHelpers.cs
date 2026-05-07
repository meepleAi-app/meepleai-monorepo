using Api.Configuration;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Api.Routing;

/// <summary>
/// Helper methods for session and API key cookie management shared across endpoints.
/// Extracted from Program.cs to support modular routing architecture.
/// </summary>
internal static class CookieHelpers
{
    // Session Cookie Methods

    // C4: legacy plaintext role cookie. Read-only during the grace period;
    // every WriteUserRoleCookie call lazy-deletes it.
    private const string UserRoleCookieNameV1 = "meepleai_user_role";

    // C4: HMAC-protected role cookie. Always issued on login / role change.
    private const string UserRoleCookieNameV2 = "meepleai_user_role_v2";

    // C4: DataProtection purpose. Changing this string invalidates all v2
    // cookies in the wild — bump it only as part of an intentional rotation.
    private const string UserRoleDataProtectionPurpose = "MeepleAi.UserRoleCookie.v2";

    // C4: hard sunset for the v1 grace period. After this date, even an
    // intact plaintext v1 cookie is ignored — clients must have re-logged in.
    // Aligned with the 7-day rollout window from the C4 spec.
    private static readonly DateTime UserRoleCookieV1SunsetUtc =
        new(2026, 5, 13, 0, 0, 0, DateTimeKind.Utc);

    public static void WriteSessionCookie(HttpContext context, string token, DateTime expiresAt)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = CreateSessionCookieOptions(context, expiresAt);
        var sessionCookieName = GetSessionCookieName(context);

        // BGAI-081: Development workaround for SameSite=None without Secure
        // ASP.NET Core blocks SameSite=None without Secure, so we write the header directly
        if (context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() &&
            options.SameSite == SameSiteMode.None &&
            !options.Secure)
        {
            // Build Set-Cookie header manually
            var cookieValue = $"{sessionCookieName}={token}; " +
                            $"Path={options.Path}; " +
                            $"Expires={expiresAt:R}; " +
                            $"HttpOnly; " +
                            $"SameSite=None";

            if (!string.IsNullOrWhiteSpace(options.Domain))
            {
                cookieValue += $"; Domain={options.Domain}";
            }

            context.Response.Headers.Append("Set-Cookie", cookieValue);
        }
        else
        {
            context.Response.Cookies.Append(sessionCookieName, token, options);
        }
    }

    public static void RemoveSessionCookie(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = BuildSessionCookieOptions(context);
        options.Expires = DateTimeOffset.UnixEpoch;

        var sessionCookieName = GetSessionCookieName(context);
        context.Response.Cookies.Delete(sessionCookieName, options);
    }

    /// <summary>
    /// Writes the user role cookie for middleware authorization checks.
    /// SEC-07: Cookie is HttpOnly to prevent XSS-based role discovery.
    /// C4: value is HMAC-protected via ASP.NET Data Protection
    /// (<see cref="UserRoleDataProtectionPurpose"/>) under the new
    /// <c>meepleai_user_role_v2</c> name. The legacy plaintext
    /// <c>meepleai_user_role</c> cookie is lazily expired on every write so
    /// existing browsers stop sending it within one round-trip.
    /// </summary>
    public static void WriteUserRoleCookie(HttpContext context, string role, DateTime expiresAt)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(role);

        var protector = context.RequestServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(UserRoleDataProtectionPurpose);
        var protectedValue = protector.Protect(role.ToLowerInvariant());

        var configuration = GetSessionCookieConfiguration(context);
        var options = BuildRoleCookieOptions(context, configuration, expiresAt);

        // C4: write v2 (HMAC-protected). The protected value is opaque base64,
        // not URL-encoded; pass through Cookies.Append which encodes safely.
        if (context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() &&
            options.SameSite == SameSiteMode.None && !options.Secure)
        {
            // BGAI-081: same dev-only manual Set-Cookie path as the session cookie.
            var cookieValue = $"{UserRoleCookieNameV2}={protectedValue}; " +
                              $"Path={options.Path}; " +
                              $"Expires={expiresAt:R}; " +
                              $"HttpOnly; " +
                              $"SameSite=None";
            if (!string.IsNullOrWhiteSpace(options.Domain))
            {
                cookieValue += $"; Domain={options.Domain}";
            }
            context.Response.Headers.Append("Set-Cookie", cookieValue);
        }
        else
        {
            context.Response.Cookies.Append(UserRoleCookieNameV2, protectedValue, options);
        }

        // C4: lazy-migrate v1 — emit a Set-Cookie that immediately expires it.
        // Mirror Path/Domain so the browser actually identifies the same cookie.
        var deleteOptions = new CookieOptions
        {
            Path = options.Path,
            Expires = DateTimeOffset.UnixEpoch,
        };
        if (!string.IsNullOrWhiteSpace(options.Domain))
        {
            deleteOptions.Domain = options.Domain;
        }
        context.Response.Cookies.Delete(UserRoleCookieNameV1, deleteOptions);
    }

    /// <summary>
    /// Reads the user's role from the role cookie. Tries the HMAC-protected
    /// v2 cookie first; falls back to the legacy plaintext v1 cookie during
    /// the grace window (until <see cref="UserRoleCookieV1SunsetUtc"/>) so
    /// existing user sessions don't all get logged out on the C4 deploy.
    /// Returns <c>null</c> on tampered, wrong-purpose, or expired payloads.
    /// </summary>
    public static string? ReadUserRoleCookie(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Prefer the v2 (HMAC-protected) cookie.
        if (context.Request.Cookies.TryGetValue(UserRoleCookieNameV2, out var v2Value) &&
            !string.IsNullOrWhiteSpace(v2Value))
        {
            try
            {
                var protector = context.RequestServices
                    .GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector(UserRoleDataProtectionPurpose);
                return protector.Unprotect(v2Value);
            }
            catch (CryptographicException)
            {
                // Tampered, wrong purpose, or rolled-out key — fail closed.
                return null;
            }
        }

        // C4: grace-period fallback to the legacy plaintext cookie. After the
        // sunset date, ignore v1 entirely and force a fresh login.
        if (DateTime.UtcNow < UserRoleCookieV1SunsetUtc &&
            context.Request.Cookies.TryGetValue(UserRoleCookieNameV1, out var v1Value) &&
            !string.IsNullOrWhiteSpace(v1Value))
        {
            return v1Value.ToLowerInvariant();
        }

        return null;
    }

    /// <summary>
    /// Removes both the v1 and v2 user role cookies during logout.
    /// </summary>
    public static void RemoveUserRoleCookie(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var configuration = GetSessionCookieConfiguration(context);
        var path = string.IsNullOrWhiteSpace(configuration.Path) ? "/" : configuration.Path;

        var options = new CookieOptions
        {
            HttpOnly = true, // SEC-07: Match write options
            Path = path,
            Expires = DateTimeOffset.UnixEpoch
        };

        if (!string.IsNullOrWhiteSpace(configuration.Domain))
        {
            options.Domain = configuration.Domain;
        }

        context.Response.Cookies.Delete(UserRoleCookieNameV1, options);
        context.Response.Cookies.Delete(UserRoleCookieNameV2, options);
    }

    private static CookieOptions BuildRoleCookieOptions(
        HttpContext context,
        SessionCookieConfiguration configuration,
        DateTime expiresAt)
    {
        var isHttps = context.Request.IsHttps;
        if (!isHttps && configuration.UseForwardedProto &&
            context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto) &&
            forwardedProto.Any(proto => string.Equals(proto, "https", StringComparison.OrdinalIgnoreCase)))
        {
            isHttps = true;
        }

        var secure = configuration.Secure ?? isHttps;
        var path = string.IsNullOrWhiteSpace(configuration.Path) ? "/" : configuration.Path;
        var sameSite = configuration.SameSite ?? SameSiteMode.Lax;

        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = sameSite,
            Path = path,
            Expires = new DateTimeOffset(expiresAt, TimeSpan.Zero)
        };

        if (!string.IsNullOrWhiteSpace(configuration.Domain))
        {
            options.Domain = configuration.Domain;
        }

        return options;
    }

    public static string GetSessionCookieName(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var configuration = GetSessionCookieConfiguration(context);
        return string.IsNullOrWhiteSpace(configuration.Name)
            ? "meepleai_session" // Default session cookie name (matches external documentation/spec)
            : configuration.Name;
    }

    private static CookieOptions CreateSessionCookieOptions(HttpContext context, DateTime expiresAt)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = BuildSessionCookieOptions(context);
        options.Expires = new DateTimeOffset(expiresAt, TimeSpan.Zero);
        return options;
    }

    private static CookieOptions BuildSessionCookieOptions(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var configuration = GetSessionCookieConfiguration(context);

        var isHttps = context.Request.IsHttps;

        if (!isHttps && configuration.UseForwardedProto &&
            context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto) &&
            forwardedProto.Any(proto => string.Equals(proto, "https", StringComparison.OrdinalIgnoreCase)))
        {
            isHttps = true;
        }

        var secure = configuration.Secure ?? isHttps;

        // Use configured SameSite or sensible defaults
        SameSiteMode sameSite;
        if (configuration.Secure is false)
        {
            // Development (HTTP): Use configured SameSite (Lax recommended for same-origin)
            sameSite = configuration.SameSite ?? SameSiteMode.Lax;
            secure = false; // Keep secure=false as configured
        }
        else
        {
            // SEC-I5: Production — default SameSite=Lax for CSRF protection
            // Lax allows same-site navigation (links, GET) but blocks cross-site POST.
            // Only use SameSite=None if API and frontend are on different domains.
            if (!secure && !configuration.Secure.HasValue)
            {
                secure = true;
            }

            sameSite = configuration.SameSite ?? SameSiteMode.Lax;
        }

        var path = string.IsNullOrWhiteSpace(configuration.Path) ? "/" : configuration.Path;

        var options = new CookieOptions
        {
            HttpOnly = configuration.HttpOnly,
            Secure = secure,
            SameSite = sameSite,
            Path = path
        };

        if (!string.IsNullOrWhiteSpace(configuration.Domain))
        {
            options.Domain = configuration.Domain;
        }

        return options;
    }

    private static SessionCookieConfiguration GetSessionCookieConfiguration(HttpContext context)
    {
        return context.RequestServices
            .GetRequiredService<IOptions<SessionCookieConfiguration>>()
            .Value;
    }

}
