using Microsoft.AspNetCore.Http;
using System.Web;

namespace Api.Middleware;

/// <summary>
/// Utility methods for removing characters that could enable log forging attacks.
/// </summary>
internal static class LogValueSanitizer
{
    /// <summary>
    /// Removes carriage returns and line feeds from a path before logging.
    /// First URL-decodes the path to handle encoded control characters (%0D, %0A).
    /// </summary>
    public static string SanitizePath(PathString path)
    {
        // URL decode first to handle %0D (%0A) → \r (\n)
        var decoded = HttpUtility.UrlDecode(path.ToString());
        return Sanitize(decoded);
    }

    /// <summary>
    /// Removes carriage returns and line feeds from an arbitrary string before logging.
    /// </summary>
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        return value.Replace("\r", string.Empty).Replace("\n", string.Empty);
    }
}
