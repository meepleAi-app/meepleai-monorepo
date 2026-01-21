using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Configuration entity defining rate limits for share requests based on user tier.
/// Each tier has different maximum pending requests, monthly limits, and cooldown periods.
/// </summary>
public sealed class ShareRequestLimitConfig : AggregateRoot<Guid>
{
    private readonly UserTier _tier;
    private int _maxPendingRequests;
    private int _maxRequestsPerMonth;
    private TimeSpan _cooldownAfterRejection;
    private bool _isActive;
    private readonly DateTime _createdAt;
    private DateTime _updatedAt;

    /// <summary>
    /// The user tier this configuration applies to.
    /// </summary>
    public UserTier Tier => _tier;

    /// <summary>
    /// Maximum number of pending share requests allowed simultaneously.
    /// </summary>
    public int MaxPendingRequests => _maxPendingRequests;

    /// <summary>
    /// Maximum number of share requests allowed per calendar month.
    /// </summary>
    public int MaxRequestsPerMonth => _maxRequestsPerMonth;

    /// <summary>
    /// Cooldown period after a request is rejected before the user can submit a new request.
    /// </summary>
    public TimeSpan CooldownAfterRejection => _cooldownAfterRejection;

    /// <summary>
    /// Whether this configuration is currently active.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// When this configuration was created.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    /// When this configuration was last updated.
    /// </summary>
    public DateTime UpdatedAt => _updatedAt;

#pragma warning disable CS8618
    private ShareRequestLimitConfig() : base() { }
#pragma warning restore CS8618

    private ShareRequestLimitConfig(
        Guid id,
        UserTier tier,
        int maxPendingRequests,
        int maxRequestsPerMonth,
        TimeSpan cooldownAfterRejection) : base(id)
    {
        _tier = tier;
        _maxPendingRequests = maxPendingRequests;
        _maxRequestsPerMonth = maxRequestsPerMonth;
        _cooldownAfterRejection = cooldownAfterRejection;
        _isActive = true;
        _createdAt = DateTime.UtcNow;
        _updatedAt = _createdAt;
    }

    /// <summary>
    /// Internal constructor for persistence and testing.
    /// </summary>
    internal ShareRequestLimitConfig(
        Guid id,
        UserTier tier,
        int maxPendingRequests,
        int maxRequestsPerMonth,
        TimeSpan cooldownAfterRejection,
        bool isActive,
        DateTime createdAt,
        DateTime updatedAt) : base(id)
    {
        _tier = tier;
        _maxPendingRequests = maxPendingRequests;
        _maxRequestsPerMonth = maxRequestsPerMonth;
        _cooldownAfterRejection = cooldownAfterRejection;
        _isActive = isActive;
        _createdAt = createdAt;
        _updatedAt = updatedAt;
    }

    /// <summary>
    /// Creates a new rate limit configuration for a specific user tier.
    /// </summary>
    /// <param name="tier">The user tier this configuration applies to.</param>
    /// <param name="maxPendingRequests">Maximum pending requests allowed.</param>
    /// <param name="maxRequestsPerMonth">Maximum requests per month.</param>
    /// <param name="cooldownAfterRejection">Cooldown period after rejection.</param>
    /// <returns>A new ShareRequestLimitConfig instance.</returns>
    /// <exception cref="ArgumentException">Thrown when values are invalid.</exception>
    public static ShareRequestLimitConfig Create(
        UserTier tier,
        int maxPendingRequests,
        int maxRequestsPerMonth,
        TimeSpan cooldownAfterRejection)
    {
        // Admin tier has unlimited access
        if (tier != UserTier.Admin)
        {
            if (maxPendingRequests < 1)
                throw new ArgumentException("Maximum pending requests must be at least 1 for non-Admin tiers", nameof(maxPendingRequests));

            if (maxRequestsPerMonth < 1)
                throw new ArgumentException("Maximum requests per month must be at least 1 for non-Admin tiers", nameof(maxRequestsPerMonth));

            if (cooldownAfterRejection < TimeSpan.Zero)
                throw new ArgumentException("Cooldown cannot be negative", nameof(cooldownAfterRejection));
        }

        return new ShareRequestLimitConfig(
            Guid.NewGuid(),
            tier,
            maxPendingRequests,
            maxRequestsPerMonth,
            cooldownAfterRejection);
    }

    /// <summary>
    /// Updates the rate limit values for this configuration.
    /// </summary>
    /// <param name="maxPendingRequests">New maximum pending requests.</param>
    /// <param name="maxRequestsPerMonth">New maximum requests per month.</param>
    /// <param name="cooldownAfterRejection">New cooldown period.</param>
    /// <exception cref="ArgumentException">Thrown when values are invalid.</exception>
    public void Update(int maxPendingRequests, int maxRequestsPerMonth, TimeSpan cooldownAfterRejection)
    {
        // Admin tier has unlimited access
        if (Tier != UserTier.Admin)
        {
            if (maxPendingRequests < 1)
                throw new ArgumentException("Maximum pending requests must be at least 1 for non-Admin tiers", nameof(maxPendingRequests));

            if (maxRequestsPerMonth < 1)
                throw new ArgumentException("Maximum requests per month must be at least 1 for non-Admin tiers", nameof(maxRequestsPerMonth));

            if (cooldownAfterRejection < TimeSpan.Zero)
                throw new ArgumentException("Cooldown cannot be negative", nameof(cooldownAfterRejection));
        }

        _maxPendingRequests = maxPendingRequests;
        _maxRequestsPerMonth = maxRequestsPerMonth;
        _cooldownAfterRejection = cooldownAfterRejection;
        _updatedAt = DateTime.UtcNow;

        AddDomainEvent(new RateLimitConfigUpdatedEvent(Id, Tier));
    }

    /// <summary>
    /// Activates this configuration.
    /// </summary>
    public void Activate()
    {
        if (!_isActive)
        {
            _isActive = true;
            _updatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Deactivates this configuration.
    /// </summary>
    public void Deactivate()
    {
        if (_isActive)
        {
            _isActive = false;
            _updatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Checks if this tier has unlimited access (bypasses all rate limits).
    /// </summary>
    public bool HasUnlimitedAccess => Tier == UserTier.Admin;
}
