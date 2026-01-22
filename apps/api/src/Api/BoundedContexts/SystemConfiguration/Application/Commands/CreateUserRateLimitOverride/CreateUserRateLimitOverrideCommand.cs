using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.CreateUserRateLimitOverride;

/// <summary>
/// Command to create a user-specific rate limit override.
/// Allows admins to grant exceptions to tier-based limits for specific users.
/// </summary>
public sealed record CreateUserRateLimitOverrideCommand : IRequest<Unit>
{
    /// <summary>
    /// The admin user ID creating this override.
    /// </summary>
    public required Guid AdminId { get; init; }

    /// <summary>
    /// The user ID to apply the override to.
    /// </summary>
    public required Guid UserId { get; init; }

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
    /// When this override expires. Null means permanent override.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Admin-provided reason for this override (required for audit trail).
    /// </summary>
    public required string Reason { get; init; }
}
