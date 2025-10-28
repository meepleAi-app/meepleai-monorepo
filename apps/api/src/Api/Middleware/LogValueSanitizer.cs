using Microsoft.AspNetCore.Http;

namespace Api.Middleware;

/// <summary>
/// Utility methods for removing characters that could enable log forging attacks.
/// </summary>
internal static class LogValueSanitizer
{
    /// <summary>
    /// Removes carriage returns and line feeds from a path before logging.
    /// </summary>
    public static string SanitizePath(PathString path) => Sanitize(path.ToString());

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
