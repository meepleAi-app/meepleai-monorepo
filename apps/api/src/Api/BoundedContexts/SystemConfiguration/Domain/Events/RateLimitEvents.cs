using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Events;

/// <summary>
/// Event raised when a rate limit configuration is updated.
/// </summary>
internal sealed class RateLimitConfigUpdatedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the configuration that was updated.
    /// </summary>
    public Guid ConfigId { get; }

    /// <summary>
    /// Gets the tier this configuration applies to.
    /// </summary>
    public UserTier Tier { get; }

    public RateLimitConfigUpdatedEvent(Guid configId, UserTier tier)
    {
        ConfigId = configId;
        Tier = tier;
    }
}

/// <summary>
/// Event raised when a user-specific rate limit override is created.
/// </summary>
internal sealed class UserRateLimitOverrideCreatedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the override.
    /// </summary>
    public Guid OverrideId { get; }

    /// <summary>
    /// Gets the user this override applies to.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the admin who created this override.
    /// </summary>
    public Guid AdminId { get; }

    public UserRateLimitOverrideCreatedEvent(Guid overrideId, Guid userId, Guid adminId)
    {
        OverrideId = overrideId;
        UserId = userId;
        AdminId = adminId;
    }
}

/// <summary>
/// Event raised when a user-specific rate limit override is removed.
/// </summary>
internal sealed class UserRateLimitOverrideRemovedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the override that was removed.
    /// </summary>
    public Guid OverrideId { get; }

    /// <summary>
    /// Gets the user this override applied to.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the admin who removed this override.
    /// </summary>
    public Guid AdminId { get; }

    public UserRateLimitOverrideRemovedEvent(Guid overrideId, Guid userId, Guid adminId)
    {
        OverrideId = overrideId;
        UserId = userId;
        AdminId = adminId;
    }
}

/// <summary>
/// Event raised when a user reaches their rate limit.
/// Can be used to trigger notifications.
/// </summary>
internal sealed class UserRateLimitReachedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the user who reached the limit.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the type of limit reached (e.g., "pending", "monthly").
    /// </summary>
    public string LimitType { get; }

    /// <summary>
    /// Limit type for maximum pending requests reached.
    /// </summary>
    public const string PendingLimit = "pending";

    /// <summary>
    /// Limit type for maximum monthly requests reached.
    /// </summary>
    public const string MonthlyLimit = "monthly";

    public UserRateLimitReachedEvent(Guid userId, string limitType)
    {
        UserId = userId;
        LimitType = limitType;
    }
}
