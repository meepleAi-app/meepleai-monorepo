using System.Text.RegularExpressions;

namespace Api.Helpers;

/// <summary>
/// Centralized string utility methods to eliminate code duplication.
/// </summary>
public static class StringHelper
{
    /// <summary>
    /// Truncates a string to a maximum length and appends an ellipsis if truncated.
    /// Useful for logging long strings or displaying preview text.
    /// </summary>
    /// <param name="text">The text to truncate</param>
    /// <param name="maxLength">Maximum length before truncation</param>
    /// <param name="ellipsis">Ellipsis string to append (default: "...")</param>
    /// <returns>Truncated string with ellipsis if needed</returns>
    /// <remarks>
    /// Common use cases:
    /// - Logging long API responses
    /// - Preview text in UI
    /// - Debugging output
    ///
    /// Examples:
    /// Truncate("Hello World", 5) → "Hello..."
    /// Truncate("Short", 10) → "Short"
    /// Truncate("Long text here", 10, "…") → "Long text …"
    /// </remarks>
    public static string Truncate(string? text, int maxLength, string ellipsis = "...")
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (text.Length <= maxLength)
        {
            return text;
        }

        var truncatedLength = Math.Max(0, maxLength - ellipsis.Length);
        return text.Substring(0, truncatedLength) + ellipsis;
    }

    /// <summary>
    /// Sanitizes a filename by removing or replacing invalid and dangerous characters.
    /// Provides comprehensive protection against path traversal, control characters, and OS-specific invalid chars.
    /// </summary>
    /// <param name="filename">The filename to sanitize</param>
    /// <param name="maxLength">Maximum length of the sanitized filename (default: 200)</param>
    /// <param name="fallbackName">Fallback name if sanitization results in empty string (default: "file")</param>
    /// <returns>Sanitized filename safe for file system operations</returns>
    /// <remarks>
    /// This method consolidates filename sanitization logic from:
    /// - ChatExportService.GenerateSafeFilename
    /// - RuleSpecService.SanitizeFileName
    /// - BlobStorageService.SanitizeFileName
    ///
    /// Security features:
    /// - Removes OS-specific invalid characters (Path.GetInvalidFileNameChars)
    /// - Blocks path traversal attempts (.., /, \)
    /// - Removes control characters (ASCII 0-31, 127)
    /// - Handles additional problematic characters (&lt;, &gt;, ?, *, |, ", :)
    /// - Limits filename length to prevent filesystem issues
    /// - Provides fallback for empty results
    /// </remarks>
    public static string SanitizeFilename(string filename, int maxLength = 200, string fallbackName = "file")
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return fallbackName;
        }

        // Step 1: Remove path traversal attempts
        var sanitized = filename
            .Replace("..", "")
            .Replace("/", "_")
            .Replace("\\", "_");

        // Step 2: Remove control characters (ASCII 0-31 and 127)
        // FIX MA0009: Add timeout to prevent ReDoS attacks
        sanitized = Regex.Replace(sanitized, @"[\x00-\x1F\x7F]", "", RegexOptions.None, TimeSpan.FromSeconds(1));

        // Step 3: Remove OS-specific invalid characters plus additional problematic chars
        var invalidChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { '<', '>', '?', '*', '|', '"', ':' })
            .Distinct()
            .ToArray();

        // Replace invalid characters with underscore
        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }

        // Step 4: Collapse multiple underscores and trim
        // FIX MA0009: Add timeout to prevent ReDoS attacks
        sanitized = Regex.Replace(sanitized, @"_+", "_", RegexOptions.None, TimeSpan.FromSeconds(1));
        sanitized = sanitized.Trim('_', ' ', '\t');

        // Step 5: Handle empty result
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return fallbackName;
        }

        // Step 6: Limit length
        return sanitized.Length > maxLength
            ? sanitized.Substring(0, maxLength).TrimEnd('_')
            : sanitized;
    }

    /// <summary>
    /// Generates a safe filename with optional prefix, suffix, and extension.
    /// Useful for creating unique filenames with timestamps or IDs.
    /// </summary>
    /// <param name="baseName">The base name to sanitize</param>
    /// <param name="extension">File extension (with or without leading dot)</param>
    /// <param name="suffix">Optional suffix (e.g., timestamp or ID)</param>
    /// <param name="maxLength">Maximum total length including extension (default: 200)</param>
    /// <returns>Safe filename with extension</returns>
    /// <example>
    /// GenerateSafeFilename("My Game", ".json", "abc123", 50)
    /// // Returns: "My_Game_abc123.json"
    /// </example>
    public static string GenerateSafeFilename(
        string baseName,
        string extension,
        string? suffix = null,
        int maxLength = 200)
    {
        // Ensure extension has leading dot
        if (!string.IsNullOrEmpty(extension) && !extension.StartsWith("."))
        {
            extension = "." + extension;
        }

        // Reserve space for extension and suffix
        var reservedLength = (extension?.Length ?? 0) + (suffix != null ? suffix.Length + 1 : 0);
        var availableLength = Math.Max(maxLength - reservedLength, 10); // At least 10 chars for base name

        // Sanitize base name
        var sanitizedBase = SanitizeFilename(baseName, availableLength, "file");

        // Build filename
        var filename = sanitizedBase;
        if (!string.IsNullOrWhiteSpace(suffix))
        {
            filename += "_" + suffix;
        }
        if (!string.IsNullOrWhiteSpace(extension))
        {
            filename += extension;
        }

        return filename;
    }
}
