using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries.GetRateLimitConfig;

/// <summary>
/// Query to retrieve all rate limit configurations for all user tiers.
/// Returns tier-based limit configurations used for rate limiting share requests.
/// </summary>
public sealed record GetRateLimitConfigQuery : IRequest<IReadOnlyList<RateLimitConfigDto>>
{
    /// <summary>
    /// Optional filter to return only active configurations.
    /// Default is true.
    /// </summary>
    public bool ActiveOnly { get; init; } = true;
}
