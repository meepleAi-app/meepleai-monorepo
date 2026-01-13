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
        var secureForced = false;

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
            // Production/HTTPS: Default to SameSite=None for cross-origin
            if (!secure && !configuration.Secure.HasValue)
            {
                secure = true;
                secureForced = true;
            }

            sameSite = configuration.SameSite ?? (secure ? SameSiteMode.None : SameSiteMode.Lax);

            if (secureForced && sameSite != SameSiteMode.None)
            {
                sameSite = SameSiteMode.None;
            }
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
