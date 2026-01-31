using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Allows per-user overrides to the tier-based rate limits.
/// Admins can grant temporary or permanent exceptions to specific users.
/// Null values mean the user should use their tier's default limits.
/// </summary>
public sealed class UserRateLimitOverride : AggregateRoot<Guid>
{
    private readonly Guid _userId;
    private int? _maxPendingRequests;
    private int? _maxRequestsPerMonth;
    private TimeSpan? _cooldownAfterRejection;
    private DateTime? _expiresAt;
    private string _reason;
    private readonly Guid _createdByAdminId;
    private readonly DateTime _createdAt;
    private DateTime _updatedAt;

    /// <summary>
    /// The user this override applies to.
    /// </summary>
    public Guid UserId => _userId;

    /// <summary>
    /// Custom maximum pending requests for this user.
    /// Null means use the tier's default value.
    /// </summary>
    public int? MaxPendingRequests => _maxPendingRequests;

    /// <summary>
    /// Custom maximum requests per month for this user.
    /// Null means use the tier's default value.
    /// </summary>
    public int? MaxRequestsPerMonth => _maxRequestsPerMonth;

    /// <summary>
    /// Custom cooldown period for this user.
    /// Null means use the tier's default value.
    /// </summary>
    public TimeSpan? CooldownAfterRejection => _cooldownAfterRejection;

    /// <summary>
    /// When this override expires.
    /// Null means the override is permanent.
    /// </summary>
    public DateTime? ExpiresAt => _expiresAt;

    /// <summary>
    /// Admin note explaining why this override was created.
    /// </summary>
    public string Reason => _reason;

    /// <summary>
    /// The admin who created this override.
    /// </summary>
    public Guid CreatedByAdminId => _createdByAdminId;

    /// <summary>
    /// When this override was created.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    /// When this override was last updated.
    /// </summary>
    public DateTime UpdatedAt => _updatedAt;

#pragma warning disable CS8618
    private UserRateLimitOverride() : base() { }
#pragma warning restore CS8618

    private UserRateLimitOverride(
        Guid id,
        Guid userId,
        Guid createdByAdminId,
        string reason,
        DateTime? expiresAt) : base(id)
    {
        _userId = userId;
        _createdByAdminId = createdByAdminId;
        _reason = reason;
        _expiresAt = expiresAt;
        _createdAt = DateTime.UtcNow;
        _updatedAt = _createdAt;
    }

    /// <summary>
    /// Internal constructor for persistence and testing.
    /// </summary>
    internal UserRateLimitOverride(
        Guid id,
        Guid userId,
        int? maxPendingRequests,
        int? maxRequestsPerMonth,
        TimeSpan? cooldownAfterRejection,
        DateTime? expiresAt,
        string reason,
        Guid createdByAdminId,
        DateTime createdAt,
        DateTime updatedAt) : base(id)
    {
        _userId = userId;
        _maxPendingRequests = maxPendingRequests;
        _maxRequestsPerMonth = maxRequestsPerMonth;
        _cooldownAfterRejection = cooldownAfterRejection;
        _expiresAt = expiresAt;
        _reason = reason;
        _createdByAdminId = createdByAdminId;
        _createdAt = createdAt;
        _updatedAt = updatedAt;
    }

    /// <summary>
    /// Creates a new rate limit override for a specific user.
    /// </summary>
    /// <param name="userId">The user this override applies to.</param>
    /// <param name="createdByAdminId">The admin creating this override.</param>
    /// <param name="reason">The reason for creating this override.</param>
    /// <param name="expiresAt">When this override expires (null for permanent).</param>
    /// <returns>A new UserRateLimitOverride instance.</returns>
    /// <exception cref="ArgumentException">Thrown when required values are invalid.</exception>
    public static UserRateLimitOverride Create(
        Guid userId,
        Guid createdByAdminId,
        string reason,
        DateTime? expiresAt = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        if (createdByAdminId == Guid.Empty)
            throw new ArgumentException("CreatedByAdminId cannot be empty", nameof(createdByAdminId));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required", nameof(reason));

        if (expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow)
            throw new ArgumentException("Expiration date must be in the future", nameof(expiresAt));

        var rateLimitOverride = new UserRateLimitOverride(
            Guid.NewGuid(),
            userId,
            createdByAdminId,
            reason.Trim(),
            expiresAt);

        rateLimitOverride.AddDomainEvent(new UserRateLimitOverrideCreatedEvent(
            rateLimitOverride.Id,
            userId,
            createdByAdminId));

        return rateLimitOverride;
    }

    /// <summary>
    /// Updates the rate limit values for this override.
    /// Pass null for any value to use the tier's default.
    /// </summary>
    /// <param name="maxPendingRequests">New max pending requests (null = use tier default).</param>
    /// <param name="maxRequestsPerMonth">New max requests per month (null = use tier default).</param>
    /// <param name="cooldownAfterRejection">New cooldown period (null = use tier default).</param>
    /// <exception cref="ArgumentException">Thrown when values are invalid.</exception>
    public void UpdateLimits(int? maxPendingRequests, int? maxRequestsPerMonth, TimeSpan? cooldownAfterRejection)
    {
        if (maxPendingRequests.HasValue && maxPendingRequests.Value < 0)
            throw new ArgumentException("Maximum pending requests cannot be negative", nameof(maxPendingRequests));

        if (maxRequestsPerMonth.HasValue && maxRequestsPerMonth.Value < 0)
            throw new ArgumentException("Maximum requests per month cannot be negative", nameof(maxRequestsPerMonth));

        if (cooldownAfterRejection.HasValue && cooldownAfterRejection.Value < TimeSpan.Zero)
            throw new ArgumentException("Cooldown cannot be negative", nameof(cooldownAfterRejection));

        _maxPendingRequests = maxPendingRequests;
        _maxRequestsPerMonth = maxRequestsPerMonth;
        _cooldownAfterRejection = cooldownAfterRejection;
        _updatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the reason for this override.
    /// </summary>
    /// <param name="reason">The new reason.</param>
    /// <exception cref="ArgumentException">Thrown when reason is empty.</exception>
    public void UpdateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required", nameof(reason));

        _reason = reason.Trim();
        _updatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the expiration date for this override.
    /// </summary>
    /// <param name="expiresAt">The new expiration date (null for permanent).</param>
    /// <exception cref="ArgumentException">Thrown when expiration date is in the past.</exception>
    public void UpdateExpiration(DateTime? expiresAt)
    {
        if (expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow)
            throw new ArgumentException("Expiration date must be in the future", nameof(expiresAt));

        _expiresAt = expiresAt;
        _updatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if this override has expired.
    /// </summary>
    public bool IsExpired() => _expiresAt.HasValue && DateTime.UtcNow > _expiresAt.Value;

    /// <summary>
    /// Checks if this override is currently active (not expired).
    /// </summary>
    public bool IsActive() => !IsExpired();

    /// <summary>
    /// Checks if this override has any custom limits set.
    /// </summary>
    public bool HasCustomLimits() =>
        _maxPendingRequests.HasValue ||
        _maxRequestsPerMonth.HasValue ||
        _cooldownAfterRejection.HasValue;
}
