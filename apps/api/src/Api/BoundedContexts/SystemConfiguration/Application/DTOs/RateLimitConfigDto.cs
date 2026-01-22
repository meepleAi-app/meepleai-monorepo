using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// Data transfer object for ShareRequestLimitConfig entity.
/// Represents tier-based rate limiting configuration.
/// </summary>
public sealed record RateLimitConfigDto
{
    /// <summary>
    /// Unique identifier for this configuration.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The user tier this configuration applies to.
    /// </summary>
    public required UserTier Tier { get; init; }

    /// <summary>
    /// Maximum number of pending share requests allowed simultaneously.
    /// </summary>
    public required int MaxPendingRequests { get; init; }

    /// <summary>
    /// Maximum number of share requests allowed per calendar month.
    /// </summary>
    public required int MaxRequestsPerMonth { get; init; }

    /// <summary>
    /// Cooldown period after a request is rejected before user can submit new request.
    /// </summary>
    public required TimeSpan CooldownAfterRejection { get; init; }

    /// <summary>
    /// Whether this configuration is currently active.
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// When this configuration was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}
