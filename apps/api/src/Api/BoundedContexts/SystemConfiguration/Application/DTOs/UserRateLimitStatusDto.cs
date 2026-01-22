using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// Data transfer object representing complete rate limit status for a user.
/// Includes current usage, effective limits, override information, and computed availability.
/// </summary>
public sealed record UserRateLimitStatusDto
{
    /// <summary>
    /// User ID.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// User's display name.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// User's tier level.
    /// </summary>
    public required UserTier Tier { get; init; }

    // ===== Current Usage =====

    /// <summary>
    /// Number of currently pending share requests for this user.
    /// </summary>
    public required int CurrentPendingCount { get; init; }

    /// <summary>
    /// Number of share requests submitted this calendar month.
    /// </summary>
    public required int CurrentMonthlyCount { get; init; }

    /// <summary>
    /// When the user's last request was rejected, if any.
    /// </summary>
    public DateTime? LastRejectionAt { get; init; }

    // ===== Effective Limits (after applying override) =====

    /// <summary>
    /// Effective maximum pending requests (tier default or override value).
    /// </summary>
    public required int EffectiveMaxPending { get; init; }

    /// <summary>
    /// Effective maximum requests per month (tier default or override value).
    /// </summary>
    public required int EffectiveMaxPerMonth { get; init; }

    /// <summary>
    /// Effective cooldown period after rejection (tier default or override value).
    /// </summary>
    public required TimeSpan EffectiveCooldown { get; init; }

    // ===== Override Information =====

    /// <summary>
    /// Whether this user has an active override.
    /// </summary>
    public required bool HasOverride { get; init; }

    /// <summary>
    /// Override details, if any.
    /// </summary>
    public UserRateLimitOverrideDto? Override { get; init; }

    // ===== Computed Status =====

    /// <summary>
    /// Whether the user can create a new share request now.
    /// </summary>
    public required bool CanCreateRequest { get; init; }

    /// <summary>
    /// Reason why the user is blocked, if blocked.
    /// </summary>
    public string? BlockReason { get; init; }

    /// <summary>
    /// When the cooldown period ends, if in cooldown.
    /// </summary>
    public DateTime? CooldownEndsAt { get; init; }

    /// <summary>
    /// When the monthly counter resets (first day of next month).
    /// </summary>
    public required DateTime MonthResetAt { get; init; }

    // ===== Usage Percentages =====

    /// <summary>
    /// Percentage of pending requests used (0-100).
    /// </summary>
    public required decimal PendingUsagePercent { get; init; }

    /// <summary>
    /// Percentage of monthly requests used (0-100).
    /// </summary>
    public required decimal MonthlyUsagePercent { get; init; }
}
