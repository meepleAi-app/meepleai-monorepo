using System.Text.RegularExpressions;
using Api.SharedKernel.Domain.Results;

namespace Api.SharedKernel.Domain.Validation;

/// <summary>
/// Common domain-specific validators for frequently used patterns.
/// </summary>
internal static class CommonValidators
{
    // Email validation regex (RFC 5322 simplified) - matches Email.cs implementation
    // FIX MA0009: Add timeout to prevent ReDoS attacks
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Validates that a string is a valid email address.
    /// </summary>
    /// <param name="value">The email address to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated email or an error.</returns>
    public static Result<string> IsValidEmail(
        this string value,
        string parameterName = "Email",
        string? message = null)
    {
        var notEmptyResult = value.NotNullOrWhiteSpace(parameterName, message);
        if (notEmptyResult.IsFailure)
        {
            return notEmptyResult;
        }

        if (!EmailRegex.IsMatch(value))
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} must be a valid email address"));
        }

        return Result<string>.Success(value);
    }
    /// <summary>
    /// Validates that a string is a valid URL.
    /// </summary>
    /// <param name="value">The URL to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated URL or an error.</returns>
    public static Result<string> IsValidUrl(
        this string value,
        string parameterName = "URL",
        string? message = null)
    {
        var notEmptyResult = value.NotNullOrWhiteSpace(parameterName, message);
        if (notEmptyResult.IsFailure)
        {
            return notEmptyResult;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} must be a valid HTTP or HTTPS URL"));
        }

        return Result<string>.Success(value);
    }

    /// <summary>
    /// Validates that a string is a valid absolute URL (any scheme).
    /// </summary>
    /// <param name="value">The URL to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated URL or an error.</returns>
    public static Result<string> IsValidAbsoluteUrl(
        this string value,
        string parameterName = "URL",
        string? message = null)
    {
        var notEmptyResult = value.NotNullOrWhiteSpace(parameterName, message);
        if (notEmptyResult.IsFailure)
        {
            return notEmptyResult;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} must be a valid absolute URL"));
        }

        return Result<string>.Success(value);
    }
    // FIX MA0009: Add timeout to prevent ReDoS attacks
    // FIX MA0023: Add ExplicitCapture for performance
    private static readonly Regex ApiKeyRegex = new(
        @"^mpl_(dev|staging|prod)_[A-Za-z0-9+/]{32,}={0,2}$",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Validates that a string matches the MeepleAI API key format (mpl_{env}_{base64}).
    /// </summary>
    /// <param name="value">The API key to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated API key or an error.</returns>
    public static Result<string> IsValidApiKey(
        this string value,
        string parameterName = "ApiKey",
        string? message = null)
    {
        var notEmptyResult = value.NotNullOrWhiteSpace(parameterName, message);
        if (notEmptyResult.IsFailure)
        {
            return notEmptyResult;
        }

        if (!ApiKeyRegex.IsMatch(value))
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} must match the format 'mpl_{{env}}_{{base64}}'"));
        }

        return Result<string>.Success(value);
    }
    /// <summary>
    /// Validates that a password meets basic security requirements.
    /// </summary>
    /// <param name="value">The password to validate.</param>
    /// <param name="minLength">Minimum password length (default: 8).</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated password or an error.</returns>
    public static Result<string> IsValidPassword(
        this string value,
        int minLength = 8,
        string parameterName = "Password",
        string? message = null)
    {
        var notEmptyResult = value.NotNullOrWhiteSpace(parameterName, message);
        if (notEmptyResult.IsFailure)
        {
            return notEmptyResult;
        }

        if (value.Length < minLength)
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} must be at least {minLength} characters long"));
        }

        // Check for at least one digit, one lowercase, one uppercase, and one special character
        var hasDigit = value.Any(char.IsDigit);
        var hasLower = value.Any(char.IsLower);
        var hasUpper = value.Any(char.IsUpper);
        var hasSpecial = value.Any(ch => !char.IsLetterOrDigit(ch));

        if (!hasDigit || !hasLower || !hasUpper || !hasSpecial)
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} must contain at least one uppercase letter, one lowercase letter, one digit, and one special character"));
        }

        return Result<string>.Success(value);
    }
    // Cross-platform invalid filename characters (union of Windows and Unix restrictions)
    // Windows: \ / : * ? " < > | (plus control chars)
    // Unix: / and \0 (null character)
    // We use the union to ensure consistent validation across all platforms
    private static readonly char[] CrossPlatformInvalidFileNameChars = new[]
    {
        '\\', '/', ':', '*', '?', '"', '<', '>', '|', '\0'
    };

    /// <summary>
    /// Validates that a file name has a valid format (cross-platform compatible).
    /// Uses a union of Windows and Unix invalid characters to ensure consistent validation.
    /// </summary>
    /// <param name="value">The file name to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated file name or an error.</returns>
    public static Result<string> IsValidFileName(
        this string value,
        string parameterName = "FileName",
        string? message = null)
    {
        var notEmptyResult = value.NotNullOrWhiteSpace(parameterName, message);
        if (notEmptyResult.IsFailure)
        {
            return notEmptyResult;
        }

        // Use cross-platform character set for consistent validation
        if (value.Any(ch => CrossPlatformInvalidFileNameChars.Contains(ch)))
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} contains invalid characters"));
        }

        return Result<string>.Success(value);
    }

    /// <summary>
    /// Validates that a file path has a valid format.
    /// </summary>
    /// <param name="value">The file path to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated file path or an error.</returns>
    public static Result<string> IsValidFilePath(
        this string value,
        string parameterName = "FilePath",
        string? message = null)
    {
        var notEmptyResult = value.NotNullOrWhiteSpace(parameterName, message);
        if (notEmptyResult.IsFailure)
        {
            return notEmptyResult;
        }

        var invalidChars = Path.GetInvalidPathChars();
        if (value.Any(ch => invalidChars.Contains(ch)))
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} contains invalid characters"));
        }

        return Result<string>.Success(value);
    }

    /// <summary>
    /// Validates that a file has one of the allowed extensions.
    /// </summary>
    /// <param name="value">The file name or path to validate.</param>
    /// <param name="allowedExtensions">Allowed file extensions (e.g., ".pdf", ".txt").</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated file name or an error.</returns>
    public static Result<string> HasAllowedExtension(
        this string value,
        string[] allowedExtensions,
        string parameterName = "FileName",
        string? message = null)
    {
        var notEmptyResult = value.NotNullOrWhiteSpace(parameterName, message);
        if (notEmptyResult.IsFailure)
        {
            return notEmptyResult;
        }

        var extension = Path.GetExtension(value).ToLowerInvariant();
        if (!allowedExtensions.Any(ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} must have one of the following extensions: {string.Join(", ", allowedExtensions)}"));
        }

        return Result<string>.Success(value);
    }
    /// <summary>
    /// Validates that a string is valid JSON.
    /// </summary>
    /// <param name="value">The JSON string to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated JSON string or an error.</returns>
    public static Result<string> IsValidJson(
        this string value,
        string parameterName = "Json",
        string? message = null)
    {
        var notEmptyResult = value.NotNullOrWhiteSpace(parameterName, message);
        if (notEmptyResult.IsFailure)
        {
            return notEmptyResult;
        }

        try
        {
            System.Text.Json.JsonDocument.Parse(value);
            return Result<string>.Success(value);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} must be valid JSON: {ex.Message}"));
        }
    }
    // FIX MA0009: Add timeout to prevent ReDoS attacks
    // FIX MA0023: Add ExplicitCapture to prevent capturing unneeded groups
    private static readonly Regex VersionRegex = new(
        @"^\d+\.\d+(\.\d+)?$",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Validates that a string is a valid semantic version (e.g., "1.0" or "1.0.0").
    /// </summary>
    /// <param name="value">The version string to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated version string or an error.</returns>
    public static Result<string> IsValidVersion(
        this string value,
        string parameterName = "Version",
        string? message = null)
    {
        var notEmptyResult = value.NotNullOrWhiteSpace(parameterName, message);
        if (notEmptyResult.IsFailure)
        {
            return notEmptyResult;
        }

        if (!VersionRegex.IsMatch(value))
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} must be in the format 'major.minor' or 'major.minor.patch'"));
        }

        return Result<string>.Success(value);
    }
    // FIX MA0009: Add timeout to prevent ReDoS attacks
    // FIX MA0023: Add ExplicitCapture to prevent capturing unneeded groups
    private static readonly Regex ConfigKeyRegex = new(
        @"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)*$",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Validates that a string is a valid configuration key (e.g., "Section.SubSection.Key").
    /// </summary>
    /// <param name="value">The configuration key to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated configuration key or an error.</returns>
    public static Result<string> IsValidConfigKey(
        this string value,
        string parameterName = "ConfigKey",
        string? message = null)
    {
        var notEmptyResult = value.NotNullOrWhiteSpace(parameterName, message);
        if (notEmptyResult.IsFailure)
        {
            return notEmptyResult;
        }

        if (!ConfigKeyRegex.IsMatch(value))
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} must contain only letters, digits, underscores, and dots, and must start with a letter"));
        }

        return Result<string>.Success(value);
    }
    /// <summary>
    /// Validates that a DateTime is not in the future.
    /// </summary>
    /// <param name="value">The DateTime to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated DateTime or an error.</returns>
    public static Result<DateTime> NotInFuture(
        this DateTime value,
        string parameterName = "DateTime",
        string? message = null)
    {
        if (value > DateTime.UtcNow)
        {
            return Result<DateTime>.Failure(Error.Validation(
                message ?? $"{parameterName} cannot be in the future"));
        }

        return Result<DateTime>.Success(value);
    }

    /// <summary>
    /// Validates that a DateTime is not in the past.
    /// </summary>
    /// <param name="value">The DateTime to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated DateTime or an error.</returns>
    public static Result<DateTime> NotInPast(
        this DateTime value,
        string parameterName = "DateTime",
        string? message = null)
    {
        if (value < DateTime.UtcNow)
        {
            return Result<DateTime>.Failure(Error.Validation(
                message ?? $"{parameterName} cannot be in the past"));
        }

        return Result<DateTime>.Success(value);
    }

    /// <summary>
    /// Validates that a DateTime is within a specific range.
    /// </summary>
    /// <param name="value">The DateTime to validate.</param>
    /// <param name="minDate">The minimum allowed date.</param>
    /// <param name="maxDate">The maximum allowed date.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated DateTime or an error.</returns>
    public static Result<DateTime> InDateRange(
        this DateTime value,
        DateTime minDate,
        DateTime maxDate,
        string parameterName = "DateTime",
        string? message = null)
    {
        if (value < minDate || value > maxDate)
        {
            return Result<DateTime>.Failure(Error.Validation(
                message ?? $"{parameterName} must be between {minDate:yyyy-MM-dd} and {maxDate:yyyy-MM-dd}"));
        }

        return Result<DateTime>.Success(value);
    }
    /// <summary>
    /// Validates that a string is a valid enum value.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="value">The string value to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the parsed enum value or an error.</returns>
    public static Result<TEnum> IsValidEnum<TEnum>(
        this string value,
        string parameterName = "Value",
        string? message = null) where TEnum : struct, Enum
    {
        var notEmptyResult = value.NotNullOrWhiteSpace(parameterName, message);
        if (notEmptyResult.IsFailure)
        {
            return Result<TEnum>.Failure(notEmptyResult.Error!);
        }

        if (!Enum.TryParse<TEnum>(value, true, out var enumValue) || !Enum.IsDefined(enumValue))
        {
            var validValues = string.Join(", ", Enum.GetNames<TEnum>());
            return Result<TEnum>.Failure(Error.Validation(
                message ?? $"{parameterName} must be one of: {validValues}"));
        }

        return Result<TEnum>.Success(enumValue);
    }
}
