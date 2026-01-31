namespace Api.Infrastructure.Entities.SystemConfiguration;

/// <summary>
/// Database entity for user-specific rate limit overrides.
/// Maps to UserRateLimitOverride domain aggregate.
/// </summary>
public sealed class UserRateLimitOverrideEntity
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User this override applies to.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Custom maximum pending requests. Null means use tier default.
    /// </summary>
    public int? MaxPendingRequests { get; set; }

    /// <summary>
    /// Custom maximum requests per month. Null means use tier default.
    /// </summary>
    public int? MaxRequestsPerMonth { get; set; }

    /// <summary>
    /// Custom cooldown period (in seconds). Null means use tier default.
    /// </summary>
    public long? CooldownAfterRejectionSeconds { get; set; }

    /// <summary>
    /// When this override expires. Null means permanent.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Admin note explaining this override.
    /// </summary>
    public string Reason { get; set; } = default!;

    /// <summary>
    /// Admin who created this override.
    /// </summary>
    public Guid CreatedByAdminId { get; set; }

    /// <summary>
    /// When this override was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this override was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
