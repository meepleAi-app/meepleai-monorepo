namespace Api.Helpers;

/// <summary>
/// Sanitizes user-provided values before logging to prevent log-forging attacks (CodeQL cs/log-forging).
/// Strips newlines, carriage returns, and control characters that could inject fake log entries.
/// </summary>
public static class LogSanitizer
{
    /// <summary>
    /// Sanitize a string value for safe logging. Removes newlines and control characters.
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
}
