namespace Api.Models;

/// <summary>
/// Result of API key validation operation.
/// Contains information about whether the key is valid and associated user details.
/// </summary>
public class ApiKeyValidationResult
{
    /// <summary>
    /// Whether the API key is valid and can be used for authentication.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// ID of the API key that was validated (only if valid).
    /// </summary>
    public string? ApiKeyId { get; init; }

    /// <summary>
    /// ID of the user who owns the API key (only if valid).
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Email of the user who owns the API key (only if valid).
    /// </summary>
    public string? UserEmail { get; init; }

    /// <summary>
    /// Display name of the user who owns the API key (only if valid).
    /// </summary>
    public string? UserDisplayName { get; init; }

    /// <summary>
    /// Role of the user who owns the API key (only if valid).
    /// </summary>
    public string? UserRole { get; init; }

    /// <summary>
    /// Permission scopes granted to this API key (only if valid).
    /// </summary>
    public string[] Scopes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Reason why the API key is invalid (only if not valid).
    /// </summary>
    public string? InvalidReason { get; init; }

    /// <summary>
    /// Creates a valid API key validation result.
    /// </summary>
    public static ApiKeyValidationResult Valid(
        string apiKeyId,
        string userId,
        string userEmail,
        string? userDisplayName,
        string userRole,
        string[] scopes)
    {
        return new ApiKeyValidationResult
        {
            IsValid = true,
            ApiKeyId = apiKeyId,
            UserId = userId,
            UserEmail = userEmail,
            UserDisplayName = userDisplayName,
            UserRole = userRole,
            Scopes = scopes
        };
    }

    /// <summary>
    /// Creates an invalid API key validation result.
    /// </summary>
    public static ApiKeyValidationResult Invalid(string reason = "Invalid or expired API key")
    {
        return new ApiKeyValidationResult
        {
            IsValid = false,
            InvalidReason = reason
        };
    }
}
