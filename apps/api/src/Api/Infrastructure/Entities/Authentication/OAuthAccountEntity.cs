namespace Api.Infrastructure.Entities;

/// <summary>
/// Represents a linked OAuth account for a user (Google, Discord, GitHub).
/// DDD-PHASE2: Converted to Guid IDs for domain alignment.
/// </summary>
internal class OAuthAccountEntity
{
    /// <summary>
    /// Primary key (UUID)
    /// DDD-PHASE2: Converted to Guid.
    /// </summary>
    required public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to users table
    /// DDD-PHASE2: Converted to Guid.
    /// </summary>
    required public Guid UserId { get; set; }

    /// <summary>
    /// OAuth provider name (google, discord, github)
    /// </summary>
    required public string Provider { get; set; }

    /// <summary>
    /// User ID from the OAuth provider (unique per provider)
    /// </summary>
    required public string ProviderUserId { get; set; }

    /// <summary>
    /// Encrypted OAuth access token
    /// </summary>
    required public string AccessTokenEncrypted { get; set; }

    /// <summary>
    /// Encrypted OAuth refresh token (nullable - not all providers support refresh)
    /// </summary>
    public string? RefreshTokenEncrypted { get; set; }

    /// <summary>
    /// Expiration timestamp of the access token (nullable if provider doesn't specify)
    /// </summary>
    public DateTime? TokenExpiresAt { get; set; }

    /// <summary>
    /// Timestamp when the OAuth account was linked
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of the last token update
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property to the user entity
    /// </summary>
    public UserEntity? User { get; set; }
}
