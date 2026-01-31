using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// Extended DTO for listing rate limit overrides with user information.
/// Used by admin endpoints to display comprehensive override details.
/// </summary>
public sealed record RateLimitOverrideListDto
{
    /// <summary>
    /// Unique identifier for this override.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// User ID this override applies to.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// User's display name.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// User's email address.
    /// </summary>
    public required string UserEmail { get; init; }

    /// <summary>
    /// User's tier level.
    /// </summary>
    public required UserTier UserTier { get; init; }

    /// <summary>
    /// Override for maximum pending requests. Null means use tier default.
    /// </summary>
    public int? MaxPendingRequests { get; init; }

    /// <summary>
    /// Override for maximum requests per month. Null means use tier default.
    /// </summary>
    public int? MaxRequestsPerMonth { get; init; }

    /// <summary>
    /// Override for cooldown after rejection. Null means use tier default.
    /// </summary>
    public TimeSpan? CooldownAfterRejection { get; init; }

    /// <summary>
    /// When this override expires. Null means permanent.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Whether this override has expired.
    /// </summary>
    public required bool IsExpired { get; init; }

    /// <summary>
    /// Admin-provided reason for this override.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// When this override was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Display name of the admin who created this override.
    /// </summary>
    public required string CreatedByAdminName { get; init; }
}
