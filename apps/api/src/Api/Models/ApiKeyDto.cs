namespace Api.Models;

/// <summary>
/// Data transfer object for API key information (without sensitive data).
/// Used for listing and viewing API keys.
/// </summary>
public class ApiKeyDto
{
    /// <summary>
    /// Unique identifier for the API key.
    /// </summary>
    required public string Id { get; init; }

    /// <summary>
    /// Human-readable name for the API key.
    /// </summary>
    required public string KeyName { get; init; }

    /// <summary>
    /// Prefix of the API key for identification (e.g., "mpl_xxxx").
    /// </summary>
    required public string KeyPrefix { get; init; }

    /// <summary>
    /// Permission scopes granted to this key.
    /// </summary>
    public string[] Scopes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Whether the API key is currently active.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Timestamp when the API key was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the API key was last used.
    /// </summary>
    public DateTime? LastUsedAt { get; init; }

    /// <summary>
    /// Optional expiration timestamp for the API key.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Timestamp when the API key was revoked (if applicable).
    /// </summary>
    public DateTime? RevokedAt { get; init; }

    /// <summary>
    /// ID of the user who revoked this API key (if applicable).
    /// </summary>
    public string? RevokedBy { get; init; }

    /// <summary>
    /// Usage quota information for this key.
    /// </summary>
    public ApiKeyQuotaDto? Quota { get; init; }
}

/// <summary>
/// Data transfer object for API key quota information.
/// </summary>
public class ApiKeyQuotaDto
{
    /// <summary>
    /// Maximum number of requests allowed per day (null = unlimited).
    /// </summary>
    public int? MaxRequestsPerDay { get; init; }

    /// <summary>
    /// Maximum number of requests allowed per hour (null = unlimited).
    /// </summary>
    public int? MaxRequestsPerHour { get; init; }

    /// <summary>
    /// Current request count for today.
    /// </summary>
    public int RequestsToday { get; init; }

    /// <summary>
    /// Current request count for this hour.
    /// </summary>
    public int RequestsThisHour { get; init; }

    /// <summary>
    /// Timestamp when the quota resets.
    /// </summary>
    public DateTime ResetsAt { get; init; }
}

/// <summary>
/// Request to create a new API key.
/// </summary>
public class CreateApiKeyRequest
{
    /// <summary>
    /// Human-readable name for the API key.
    /// </summary>
    required public string KeyName { get; init; }

    /// <summary>
    /// Permission scopes to grant to this key.
    /// </summary>
    public string[] Scopes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Optional expiration date for the key.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Environment for the key (live or test).
    /// </summary>
    public string Environment { get; init; } = "live";

    /// <summary>
    /// Maximum number of requests allowed per day (null = unlimited).
    /// </summary>
    public int? MaxRequestsPerDay { get; init; }

    /// <summary>
    /// Maximum number of requests allowed per hour (null = unlimited).
    /// </summary>
    public int? MaxRequestsPerHour { get; init; }
}

/// <summary>
/// Response containing a newly created API key with the plaintext key value.
/// The plaintext key is only shown once and cannot be retrieved later.
/// </summary>
public class CreateApiKeyResponse
{
    /// <summary>
    /// The newly created API key information.
    /// </summary>
    required public ApiKeyDto ApiKey { get; init; }

    /// <summary>
    /// The plaintext API key value. Store this securely - it cannot be retrieved later.
    /// </summary>
    required public string PlaintextKey { get; init; }

    /// <summary>
    /// Warning message about the plaintext key.
    /// </summary>
    public string Warning { get; init; } = "Store this key securely. It will not be shown again.";
}

/// <summary>
/// Request to update an existing API key.
/// </summary>
public class UpdateApiKeyRequest
{
    /// <summary>
    /// New name for the API key (optional).
    /// </summary>
    public string? KeyName { get; init; }

    /// <summary>
    /// New scopes for the API key (optional).
    /// </summary>
    public string[]? Scopes { get; init; }

    /// <summary>
    /// New expiration date (optional).
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Whether to activate or deactivate the key (optional).
    /// </summary>
    public bool? IsActive { get; init; }

    /// <summary>
    /// New maximum requests per day (optional, null = unlimited).
    /// </summary>
    public int? MaxRequestsPerDay { get; init; }

    /// <summary>
    /// New maximum requests per hour (optional, null = unlimited).
    /// </summary>
    public int? MaxRequestsPerHour { get; init; }
}

/// <summary>
/// Request to rotate an API key (generates new key, revokes old one).
/// </summary>
public class RotateApiKeyRequest
{
    /// <summary>
    /// Optional new expiration date for the new key.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// Response for API key rotation.
/// </summary>
public class RotateApiKeyResponse
{
    /// <summary>
    /// The newly created API key information.
    /// </summary>
    required public ApiKeyDto NewApiKey { get; init; }

    /// <summary>
    /// The plaintext value of the new API key. Store this securely.
    /// </summary>
    required public string PlaintextKey { get; init; }

    /// <summary>
    /// ID of the old (revoked) API key.
    /// </summary>
    required public string RevokedKeyId { get; init; }

    /// <summary>
    /// Warning message about the key rotation.
    /// </summary>
    public string Warning { get; init; } = "The old key has been revoked. Update all applications to use the new key.";
}

/// <summary>
/// Response for paginated list of API keys.
/// </summary>
public class ApiKeyListResponse
{
    /// <summary>
    /// List of API keys.
    /// </summary>
    public List<ApiKeyDto> Keys { get; init; } = new();

    /// <summary>
    /// Total count of keys (before pagination).
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; init; }
}
