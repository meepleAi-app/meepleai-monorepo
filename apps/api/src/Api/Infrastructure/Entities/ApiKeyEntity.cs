namespace Api.Infrastructure.Entities;

/// <summary>
/// Represents an API key for external API authentication.
/// API keys provide an alternative authentication method to cookie-based sessions,
/// allowing programmatic access to the MeepleAI API.
/// </summary>
public class ApiKeyEntity
{
    /// <summary>
    /// Unique identifier for the API key.
    /// </summary>
    required public string Id { get; set; }

    /// <summary>
    /// ID of the user who owns this API key.
    /// </summary>
    required public string UserId { get; set; }

    /// <summary>
    /// Human-readable name for the API key (e.g., "Production Server", "Mobile App").
    /// </summary>
    required public string KeyName { get; set; }

    /// <summary>
    /// BCrypt hash of the API key. Never store the plaintext key.
    /// The full key is only returned once during creation.
    /// </summary>
    required public string KeyHash { get; set; }

    /// <summary>
    /// Prefix of the API key for display and identification (e.g., "mpl_xxxx").
    /// This allows users to identify which key was used without exposing the full key.
    /// </summary>
    required public string KeyPrefix { get; set; }

    /// <summary>
    /// Array of permission scopes granted to this key (e.g., ["read", "write", "admin"]).
    /// Allows fine-grained access control per key.
    /// </summary>
    public string[] Scopes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether the API key is currently active.
    /// Disabled keys cannot be used for authentication.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp when the API key was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the API key was last used for authentication.
    /// Updated asynchronously to avoid blocking requests.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Optional expiration timestamp for the API key.
    /// If set, the key cannot be used after this date.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Timestamp when the API key was revoked.
    /// If set, the key can no longer be used regardless of IsActive status.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// ID of the user who revoked this API key.
    /// Null if not revoked or revoked by system.
    /// </summary>
    public string? RevokedBy { get; set; }

    /// <summary>
    /// Optional JSON metadata for the API key (e.g., IP restrictions, usage limits).
    /// Stored as JSON text for flexibility.
    /// </summary>
    public string? Metadata { get; set; }

    // Navigation properties
    /// <summary>
    /// The user who owns this API key.
    /// </summary>
    required public UserEntity User { get; set; }

    /// <summary>
    /// The user who revoked this API key (if applicable).
    /// </summary>
    public UserEntity? RevokedByUser { get; set; }
}
