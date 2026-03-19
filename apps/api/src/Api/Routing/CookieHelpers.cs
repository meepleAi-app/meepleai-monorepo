using Api.Configuration;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Options;

namespace Api.Routing;

/// <summary>
/// Helper methods for session and API key cookie management shared across endpoints.
/// Extracted from Program.cs to support modular routing architecture.
/// </summary>
internal static class CookieHelpers
{
    // Session Cookie Methods

    private const string UserRoleCookieName = "meepleai_user_role";

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
    /// Next.js middleware can read HttpOnly cookies on the server side.
    /// Role should be exposed to client-side JS via the /auth/me endpoint instead.
    /// </summary>
    public static void WriteUserRoleCookie(HttpContext context, string role, DateTime expiresAt)
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
        var path = string.IsNullOrWhiteSpace(configuration.Path) ? "/" : configuration.Path;

        // SEC-I5: Use same SameSite as session cookie — Lax for CSRF protection
        var sameSite = configuration.SameSite ?? SameSiteMode.Lax;

        var options = new CookieOptions
        {
            HttpOnly = true, // SEC-07: Prevent XSS from reading admin role
            Secure = secure,
            SameSite = sameSite,
            Path = path,
            Expires = new DateTimeOffset(expiresAt, TimeSpan.Zero)
        };

        if (!string.IsNullOrWhiteSpace(configuration.Domain))
        {
            options.Domain = configuration.Domain;
        }

        // BGAI-081: Development workaround for SameSite=None without Secure
        if (context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() &&
            sameSite == SameSiteMode.None && !secure)
        {
            // SEC-07: Include HttpOnly in manual Set-Cookie header
            var cookieValue = $"{UserRoleCookieName}={role.ToLowerInvariant()}; " +
                            $"Path={path}; " +
                            $"Expires={expiresAt:R}; " +
                            $"HttpOnly; " +
                            $"SameSite=None";

            if (!string.IsNullOrWhiteSpace(configuration.Domain))
            {
                cookieValue += $"; Domain={configuration.Domain}";
            }

            context.Response.Headers.Append("Set-Cookie", cookieValue);
        }
        else
        {
            context.Response.Cookies.Append(UserRoleCookieName, role.ToLowerInvariant(), options);
        }
    }

    /// <summary>
    /// Removes the user role cookie during logout.
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

        context.Response.Cookies.Delete(UserRoleCookieName, options);
    }

    // API Key Cookie Methods

    public static void WriteApiKeyCookie(HttpContext context, string apiKey, DateTime? expiresAt = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = CreateApiKeyCookieOptions(context, expiresAt);
        const string apiKeyCookieName = "meeple_apikey";
        context.Response.Cookies.Append(apiKeyCookieName, apiKey, options);
    }

    public static void RemoveApiKeyCookie(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = BuildApiKeyCookieOptions(context);
        options.Expires = DateTimeOffset.UnixEpoch;

        const string apiKeyCookieName = "meeple_apikey";
        context.Response.Cookies.Delete(apiKeyCookieName, options);
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

    private static CookieOptions CreateApiKeyCookieOptions(HttpContext context, DateTime? expiresAt)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = BuildApiKeyCookieOptions(context);
        if (expiresAt.HasValue)
        {
            options.Expires = new DateTimeOffset(expiresAt.Value, TimeSpan.Zero);
        }
        else
        {
            // Default: API key cookie expires in 90 days
            options.Expires = DateTimeOffset.UtcNow.AddDays(90);
        }
        return options;
    }

    private static CookieOptions BuildApiKeyCookieOptions(HttpContext context)
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
        var path = string.IsNullOrWhiteSpace(configuration.Path) ? "/" : configuration.Path;

        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = path
        };

        if (!string.IsNullOrWhiteSpace(configuration.Domain))
        {
            options.Domain = configuration.Domain;
        }

        return options;
    }

}
