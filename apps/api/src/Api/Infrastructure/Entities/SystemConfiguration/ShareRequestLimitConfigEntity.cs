namespace Api.Infrastructure.Entities.SystemConfiguration;

/// <summary>
/// Database entity for tier-based rate limit configuration.
/// Maps to ShareRequestLimitConfig domain aggregate.
/// </summary>
public sealed class ShareRequestLimitConfigEntity
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User tier this configuration applies to (0=Free, 1=Premium, 2=Pro, 3=Admin).
    /// </summary>
    public int Tier { get; set; }

    /// <summary>
    /// Maximum number of pending share requests allowed simultaneously.
    /// </summary>
    public int MaxPendingRequests { get; set; }

    /// <summary>
    /// Maximum number of share requests allowed per calendar month.
    /// </summary>
    public int MaxRequestsPerMonth { get; set; }

    /// <summary>
    /// Cooldown period (in seconds) after a request is rejected.
    /// </summary>
    public long CooldownAfterRejectionSeconds { get; set; }

    /// <summary>
    /// Whether this configuration is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// When this configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this configuration was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
