using System.Web;
using Microsoft.AspNetCore.Http;

namespace Api.Helpers;

/// <summary>
/// Canonical sanitizer for log-forging prevention (CWE-117, CodeQL cs/log-forging).
/// Strips carriage returns, line feeds, and tab characters that could inject fake log entries.
///
/// See ADR-058. For PII confidentiality masking use <see cref="Api.Infrastructure.Security.DataMasking"/>.
/// </summary>
public static class LogSanitizer
{
    /// <summary>
    /// Sanitize a string value for safe logging. Removes newlines and replaces tabs with spaces.
    /// </summary>
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\t", " ");
    }

    /// <summary>
    /// Sanitize and truncate for logging. Useful for potentially long user inputs.
    /// </summary>
    public static string Sanitize(string? value, int maxLength)
    {
        var sanitized = Sanitize(value);
        return sanitized.Length > maxLength
            ? sanitized[..maxLength] + "..."
            : sanitized;
    }

    /// <summary>
    /// Sanitize a request path before logging. URL-decodes first so encoded control characters
    /// (<c>%0D</c>, <c>%0A</c>) become detectable, then strips them via <see cref="Sanitize(string)"/>.
    /// </summary>
    public static string SanitizePath(PathString path)
    {
        var decoded = HttpUtility.UrlDecode(path.ToString());
        return Sanitize(decoded);
    }
}
