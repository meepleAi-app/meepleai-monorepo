using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.RemoveUserRateLimitOverride;

/// <summary>
/// Command to remove a user-specific rate limit override.
/// Reverts the user to tier-based rate limits.
/// </summary>
public sealed record RemoveUserRateLimitOverrideCommand : IRequest<Unit>
{
    /// <summary>
    /// The admin user ID executing this command.
    /// </summary>
    public required Guid AdminId { get; init; }

    /// <summary>
    /// The user ID to remove the override from.
    /// </summary>
    public required Guid UserId { get; init; }
}
