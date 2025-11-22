using System.Text.RegularExpressions;
using Api.Infrastructure.Entities;

namespace Api.Infrastructure.Security;

/// <summary>
/// Utility class for masking sensitive information in logs and error messages.
/// Prevents exposure of PII, credentials, and other sensitive data.
/// </summary>
public static partial class DataMasking
{
    /// <summary>
    /// Masks sensitive string, keeping only first/last characters.
    /// </summary>
    /// <param name="value">The value to mask</param>
    /// <param name="visibleChars">Number of characters to keep visible at start/end</param>
    /// <returns>Masked string (e.g., "abcd...wxyz")</returns>
    public static string MaskString(string? value, int visibleChars = 4)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= visibleChars * 2)
            return "***";

        return $"{value[..visibleChars]}...{value[^visibleChars..]}";
    }

    /// <summary>
    /// Masks email address while preserving domain for debugging.
    /// </summary>
    /// <param name="email">Email address to mask</param>
    /// <returns>Masked email (e.g., "j***n@example.com")</returns>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            return "***@***.***";

        var parts = email.Split('@');
        var username = parts[0];
        var domain = parts[1];

        var maskedUsername = username.Length > 2
            ? $"{username[0]}***{username[^1]}"
            : "***";

        return $"{maskedUsername}@{domain}";
    }

    /// <summary>
    /// Masks JWT token (shows only algorithm/type from header).
    /// </summary>
    /// <param name="token">JWT token to mask</param>
    /// <returns>Masked token (e.g., "eyJhbGciOi...***")</returns>
    public static string MaskJwt(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return "***";

        // Show first 20 chars (header), hide rest
        return token.Length > 20
            ? $"{token[..20]}...***"
            : "***";
    }

    /// <summary>
    /// Masks credit card number (PCI-DSS compliant).
    /// </summary>
    /// <param name="cardNumber">Credit card number to mask</param>
    /// <returns>Masked card (e.g., "****-****-****-1234")</returns>
    public static string MaskCreditCard(string? cardNumber)
    {
        if (string.IsNullOrEmpty(cardNumber))
            return "****-****-****-****";

        var digitsOnly = new string(cardNumber.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length < 4)
            return "****-****-****-****";

        var lastFour = digitsOnly[^4..];
        return $"****-****-****-{lastFour}";
    }

    /// <summary>
    /// Redacts password from connection string.
    /// </summary>
    /// <param name="connectionString">Connection string to redact</param>
    /// <returns>Connection string with password redacted</returns>
    public static string RedactConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return string.Empty;

        // Handle various formats
        var result = connectionString;
        result = PasswordInConnectionStringPattern1().Replace(result, "$1=***REDACTED***");
        result = PasswordInConnectionStringPattern2().Replace(result, "$1=***REDACTED***");
        result = PasswordInConnectionStringPattern3().Replace(result, "$1:***REDACTED***");

        return result;
    }

    /// <summary>
    /// Sanitizes user object for logging (removes sensitive fields).
    /// </summary>
    /// <param name="user">User entity to sanitize</param>
    /// <returns>Anonymous object with only safe fields</returns>
    public static object SanitizeUser(UserEntity user)
    {
        return new
        {
            user.Id,
            user.Role,
            Email = MaskEmail(user.Email),
            user.CreatedAt,
            // Exclude: PasswordHash, TotpSecretEncrypted, etc.
        };
    }

    /// <summary>
    /// Masks API response body to prevent sensitive data exposure.
    /// Truncates to prevent log bloat and redacts common sensitive patterns.
    /// </summary>
    /// <param name="responseBody">API response body</param>
    /// <param name="maxLength">Maximum length to include (default 500 chars)</param>
    /// <returns>Masked/truncated response</returns>
    public static string MaskResponseBody(string? responseBody, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(responseBody))
            return "[empty]";

        var masked = responseBody;

        // Redact common sensitive patterns
        masked = EmailPattern().Replace(masked, "***@***");
        masked = BearerTokenPattern().Replace(masked, "Bearer ***");
        masked = ApiKeyPattern().Replace(masked, "\"api_key\":\"***\"");
        masked = PasswordPattern().Replace(masked, "\"password\":\"***\"");
        masked = SecretPattern().Replace(masked, "\"secret\":\"***\"");

        // Truncate if too long
        if (masked.Length > maxLength)
        {
            return $"{masked[..maxLength]}... [truncated {masked.Length - maxLength} chars]";
        }

        return masked;
    }

    /// <summary>
    /// Masks IP address (GDPR-compliant anonymization).
    /// </summary>
    /// <param name="ipAddress">IP address to mask</param>
    /// <returns>Masked IP (e.g., "192.168.1.***" or "2001:db8::***")</returns>
    public static string MaskIpAddress(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress) || ipAddress == "unknown")
            return "***";

        // IPv4: mask last octet
        if (ipAddress.Contains('.'))
        {
            var parts = ipAddress.Split('.');
            if (parts.Length == 4)
            {
                return $"{parts[0]}.{parts[1]}.{parts[2]}.***";
            }
        }

        // IPv6: mask last segment
        if (ipAddress.Contains(':'))
        {
            var lastColon = ipAddress.LastIndexOf(':');
            if (lastColon > 0)
            {
                return $"{ipAddress[..lastColon]}:***";
            }
        }

        return "***";
    }

    // Regex patterns for connection string password detection
    [GeneratedRegex(@"(password|pwd)=[^;]+", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordInConnectionStringPattern1();

    [GeneratedRegex(@"(Password|PWD)=[^;]+")]
    private static partial Regex PasswordInConnectionStringPattern2();

    [GeneratedRegex(@"(password|pwd):\w+", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordInConnectionStringPattern3();

    // Regex patterns for response body sanitization
    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9_\-\.]+")]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(@"""api_key""\s*:\s*""[^""]+""", RegexOptions.IgnoreCase)]
    private static partial Regex ApiKeyPattern();

    [GeneratedRegex(@"""password""\s*:\s*""[^""]+""", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordPattern();

    [GeneratedRegex(@"""secret""\s*:\s*""[^""]+""", RegexOptions.IgnoreCase)]
    private static partial Regex SecretPattern();
}
