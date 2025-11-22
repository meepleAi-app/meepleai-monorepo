using System.Security;
using System.Text.RegularExpressions;

namespace Api.Infrastructure.Security;

/// <summary>
/// Path security utility for preventing path traversal and injection attacks
/// Implements defense-in-depth validation for file system operations
/// </summary>
public static partial class PathSecurity
{
    /// <summary>
    /// Validates that resolved path is within allowed directory (prevents directory traversal)
    /// </summary>
    /// <param name="basePath">The base directory path (must be absolute)</param>
    /// <param name="filename">The filename or relative path to validate</param>
    /// <returns>The validated full path</returns>
    /// <exception cref="ArgumentException">Thrown if parameters are invalid</exception>
    /// <exception cref="SecurityException">Thrown if path escapes allowed directory</exception>
    public static string ValidatePathIsInDirectory(string basePath, string filename)
    {
        if (string.IsNullOrWhiteSpace(basePath))
            throw new ArgumentException("Base path cannot be empty", nameof(basePath));

        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentException("Filename cannot be empty", nameof(filename));

        // Detect path traversal patterns before path resolution
        // Check for relative traversal sequences, not absolute paths
        // Avoid false positives on Windows absolute paths (C:\Users\...)
        if (filename.Contains("../") || filename.Contains("..\\") ||
            filename.StartsWith("../") || filename.StartsWith("..\\") ||
            filename.EndsWith("/..") || filename.EndsWith("\\..") ||
            filename.Contains("/..") || filename.Contains("\\..") ||
            filename.Contains("....") ||
            filename.Contains("//") ||
            filename.Contains("\\\\"))
        {
            throw new SecurityException(
                $"Path traversal pattern detected in filename: '{filename}'");
        }

        // Combine and resolve to absolute path
        var combinedPath = Path.Combine(basePath, filename);
        var fullPath = Path.GetFullPath(combinedPath);

        // Get canonical base directory path and ensure it ends with directory separator
        // This prevents sibling directory bypasses (e.g., /storage/games matching /storage/games_backup)
        var baseDirectory = Path.GetFullPath(basePath);
        if (!baseDirectory.EndsWith(Path.DirectorySeparatorChar))
        {
            baseDirectory += Path.DirectorySeparatorChar;
        }

        // Verify resolved path is within base directory
        // Use OrdinalIgnoreCase for case-insensitive file systems (Windows, macOS by default)
        if (!fullPath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                $"Path traversal detected: '{filename}' resolves outside allowed directory");
        }

        return fullPath;
    }

    /// <summary>
    /// Sanitizes filename by removing dangerous characters
    /// </summary>
    /// <param name="filename">The filename to sanitize</param>
    /// <returns>Sanitized filename</returns>
    /// <exception cref="ArgumentException">Thrown if filename is invalid</exception>
    public static string SanitizeFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentException("Filename cannot be empty", nameof(filename));

        // Remove path separators and dangerous characters
        var invalidChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' })
            .Distinct()
            .ToArray();

        var sanitized = new string(filename
            .Where(c => !invalidChars.Contains(c))
            .ToArray());

        // Remove leading/trailing dots and spaces
        // Leading/trailing dots can be security risks (.hidden files, "..") or cause filesystem issues
        // Internal dots are safe and needed for extensions (file.name.pdf)
        sanitized = sanitized.Trim('.', ' ');

        if (string.IsNullOrWhiteSpace(sanitized))
            throw new ArgumentException("Filename contains only invalid characters", nameof(filename));

        // Limit length to prevent filesystem issues
        const int MaxFilenameLength = 255;
        if (sanitized.Length > MaxFilenameLength)
            sanitized = sanitized.Substring(0, MaxFilenameLength);

        return sanitized;
    }

    /// <summary>
    /// Validates file extension against whitelist
    /// </summary>
    /// <param name="filename">The filename to check</param>
    /// <param name="allowedExtensions">Allowed extensions (e.g., ".pdf", ".png")</param>
    /// <exception cref="ArgumentException">Thrown if extension is not allowed</exception>
    public static void ValidateFileExtension(string filename, params string[] allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentException("Filename cannot be empty", nameof(filename));

        if (allowedExtensions == null || allowedExtensions.Length == 0)
            throw new ArgumentException("At least one allowed extension must be specified", nameof(allowedExtensions));

        var extension = Path.GetExtension(filename).ToLowerInvariant();

        // Normalize allowed extensions to lowercase
        var normalizedAllowed = allowedExtensions.Select(e => e.ToLowerInvariant()).ToArray();

        if (!normalizedAllowed.Contains(extension))
        {
            throw new ArgumentException(
                $"Invalid file extension: '{extension}'. Allowed: {string.Join(", ", allowedExtensions)}");
        }
    }

    /// <summary>
    /// Generates a safe random filename with original extension
    /// </summary>
    /// <param name="originalFilename">Original filename to extract extension from</param>
    /// <returns>Safe filename with GUID and original extension</returns>
    public static string GenerateSafeFilename(string originalFilename)
    {
        if (string.IsNullOrWhiteSpace(originalFilename))
            throw new ArgumentException("Original filename cannot be empty", nameof(originalFilename));

        var extension = Path.GetExtension(originalFilename);  // Includes leading dot (e.g., ".pdf")

        // If no extension, return GUID only
        if (string.IsNullOrWhiteSpace(extension))
        {
            return $"{Guid.NewGuid():N}";
        }

        // Remove leading dot, sanitize extension, then re-add dot explicitly
        var sanitizedExtension = SanitizeFilename(extension.TrimStart('.'));
        return $"{Guid.NewGuid():N}.{sanitizedExtension}";
    }

    /// <summary>
    /// Checks if path exists and is within allowed directory
    /// </summary>
    /// <param name="basePath">The base directory path</param>
    /// <param name="filename">The filename to check</param>
    /// <returns>True if file exists and is within allowed directory, false otherwise</returns>
    public static bool SafeFileExists(string basePath, string filename)
    {
        try
        {
            var safePath = ValidatePathIsInDirectory(basePath, filename);
            return File.Exists(safePath);
        }
        catch (SecurityException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if directory exists and is within allowed base path
    /// </summary>
    /// <param name="basePath">The base directory path</param>
    /// <param name="directoryName">The directory name to check</param>
    /// <returns>True if directory exists and is within allowed path, false otherwise</returns>
    public static bool SafeDirectoryExists(string basePath, string directoryName)
    {
        try
        {
            var safePath = ValidatePathIsInDirectory(basePath, directoryName);
            return Directory.Exists(safePath);
        }
        catch (SecurityException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Regex pattern for validating identifiers (alphanumeric, hyphens, underscores only)
    /// </summary>
    [GeneratedRegex("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled)]
    private static partial Regex IdentifierPattern();

    /// <summary>
    /// Validates that a given identifier (e.g., gameId, templateId) is safe to use in file paths
    /// Ensures it contains only alphanumeric characters, hyphens, and underscores
    /// </summary>
    /// <param name="identifier">The identifier to validate</param>
    /// <param name="parameterName">The parameter name for error messages</param>
    /// <exception cref="ArgumentException">Thrown if identifier contains unsafe characters</exception>
    public static void ValidateIdentifier(string identifier, string parameterName = "identifier")
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Identifier cannot be empty", parameterName);

        // Allow only alphanumeric, hyphens, and underscores
        // This prevents path traversal sequences like ".." or "/"
        if (!IdentifierPattern().IsMatch(identifier))
        {
            throw new ArgumentException(
                $"Identifier contains invalid characters. Only alphanumeric, hyphens, and underscores are allowed.",
                parameterName);
        }

        // Additional check: reject identifiers that are just dots
        if (identifier.All(c => c == '.'))
        {
            throw new ArgumentException("Identifier cannot consist only of dots", parameterName);
        }
    }
}
