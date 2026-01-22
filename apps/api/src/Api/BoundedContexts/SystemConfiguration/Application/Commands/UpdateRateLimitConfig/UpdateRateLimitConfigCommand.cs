using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateRateLimitConfig;

/// <summary>
/// Command to update rate limit configuration for a specific user tier.
/// Only admin users can execute this command.
/// </summary>
public sealed record UpdateRateLimitConfigCommand : IRequest<Unit>
{
    /// <summary>
    /// The admin user ID executing this command.
    /// </summary>
    public required Guid AdminId { get; init; }

    /// <summary>
    /// The user tier to update configuration for.
    /// </summary>
    public required UserTier Tier { get; init; }

    /// <summary>
    /// New maximum pending requests limit.
    /// </summary>
    public required int MaxPendingRequests { get; init; }

    /// <summary>
    /// New maximum requests per month limit.
    /// </summary>
    public required int MaxRequestsPerMonth { get; init; }

    /// <summary>
    /// New cooldown period after rejection.
    /// </summary>
    public required TimeSpan CooldownAfterRejection { get; init; }
}
