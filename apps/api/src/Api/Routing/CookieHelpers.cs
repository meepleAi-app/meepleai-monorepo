using Api.Configuration;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Options;

namespace Api.Routing;

/// <summary>
/// Helper methods for session and API key cookie management shared across endpoints.
/// Extracted from Program.cs to support modular routing architecture.
/// </summary>
public static class CookieHelpers
{
    // Session Cookie Methods

    public static void WriteSessionCookie(HttpContext context, string token, DateTime expiresAt)
    {
        var options = CreateSessionCookieOptions(context, expiresAt);
        var sessionCookieName = GetSessionCookieName(context);
        context.Response.Cookies.Append(sessionCookieName, token, options);
    }

    public static void RemoveSessionCookie(HttpContext context)
    {
        var options = BuildSessionCookieOptions(context);
        options.Expires = DateTimeOffset.UnixEpoch;

        var sessionCookieName = GetSessionCookieName(context);
        context.Response.Cookies.Delete(sessionCookieName, options);
    }

    // User Role Cookie Methods

    public static void WriteUserRoleCookie(HttpContext context, string role, DateTime expiresAt)
    {
        var options = CreateUserRoleCookieOptions(context, expiresAt);
        const string roleCookieName = "meepleai_user_role";
        context.Response.Cookies.Append(roleCookieName, role, options);
    }

    public static void RemoveUserRoleCookie(HttpContext context)
    {
        var options = BuildUserRoleCookieOptions(context);
        options.Expires = DateTimeOffset.UnixEpoch;

        const string roleCookieName = "meepleai_user_role";
        context.Response.Cookies.Delete(roleCookieName, options);
    }

    // API Key Cookie Methods

    public static void WriteApiKeyCookie(HttpContext context, string apiKey, DateTime? expiresAt = null)
    {
        var options = CreateApiKeyCookieOptions(context, expiresAt);
        const string apiKeyCookieName = "meeple_apikey";
        context.Response.Cookies.Append(apiKeyCookieName, apiKey, options);
    }

    public static void RemoveApiKeyCookie(HttpContext context)
    {
        var options = BuildApiKeyCookieOptions(context);
        options.Expires = DateTimeOffset.UnixEpoch;

        const string apiKeyCookieName = "meeple_apikey";
        context.Response.Cookies.Delete(apiKeyCookieName, options);
    }

    public static string GetSessionCookieName(HttpContext context)
    {
        var configuration = GetSessionCookieConfiguration(context);
        return string.IsNullOrWhiteSpace(configuration.Name)
            ? "meepleai_session" // Default session cookie name (matches external documentation/spec)
            : configuration.Name;
    }

    private static CookieOptions CreateSessionCookieOptions(HttpContext context, DateTime expiresAt)
    {
        var options = BuildSessionCookieOptions(context);
        options.Expires = expiresAt;
        return options;
    }

    private static CookieOptions BuildSessionCookieOptions(HttpContext context)
    {
        var configuration = GetSessionCookieConfiguration(context);

        var isHttps = context.Request.IsHttps;

        if (!isHttps && configuration.UseForwardedProto &&
            context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto))
        {
            foreach (var proto in forwardedProto)
            {
                if (string.Equals(proto, "https", StringComparison.OrdinalIgnoreCase))
                {
                    isHttps = true;
                    break;
                }
            }
        }

        var secure = configuration.Secure ?? isHttps;
        var secureForced = false;

        // CRITICAL FIX: For localhost development with Secure=false
        // Force SameSite=None BEFORE any other logic modifies it
        SameSiteMode sameSite;
        if (configuration.Secure == false)
        {
            // Explicitly configured for development (HTTP)
            // Force SameSite=None for cross-port cookies
            sameSite = SameSiteMode.None;
            secure = false; // Keep secure=false as configured
        }
        else
        {
            // Default logic for production/HTTPS
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
        var options = BuildApiKeyCookieOptions(context);
        if (expiresAt.HasValue)
        {
            options.Expires = expiresAt.Value;
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
        var configuration = GetSessionCookieConfiguration(context);
        var isHttps = context.Request.IsHttps;

        if (!isHttps && configuration.UseForwardedProto &&
            context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto))
        {
            foreach (var proto in forwardedProto)
            {
                if (string.Equals(proto, "https", StringComparison.OrdinalIgnoreCase))
                {
                    isHttps = true;
                    break;
                }
            }
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

    private static CookieOptions CreateUserRoleCookieOptions(HttpContext context, DateTime expiresAt)
    {
        var options = BuildUserRoleCookieOptions(context);
        options.Expires = expiresAt;
        return options;
    }

    private static CookieOptions BuildUserRoleCookieOptions(HttpContext context)
    {
        var configuration = GetSessionCookieConfiguration(context);
        var isHttps = context.Request.IsHttps;

        if (!isHttps && configuration.UseForwardedProto &&
            context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto))
        {
            foreach (var proto in forwardedProto)
            {
                if (string.Equals(proto, "https", StringComparison.OrdinalIgnoreCase))
                {
                    isHttps = true;
                    break;
                }
            }
        }

        var secure = configuration.Secure ?? isHttps;
        var secureForced = false;

        if (!secure && !configuration.Secure.HasValue)
        {
            secure = true;
            secureForced = true;
        }

        var sameSite = configuration.SameSite ?? (secure ? SameSiteMode.None : SameSiteMode.Lax);

        if (secureForced && sameSite != SameSiteMode.None)
        {
            sameSite = SameSiteMode.None;
        }
        
        var path = string.IsNullOrWhiteSpace(configuration.Path) ? "/" : configuration.Path;

        var options = new CookieOptions
        {
            HttpOnly = false, // Allow JavaScript to read for client-side routing
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
}
