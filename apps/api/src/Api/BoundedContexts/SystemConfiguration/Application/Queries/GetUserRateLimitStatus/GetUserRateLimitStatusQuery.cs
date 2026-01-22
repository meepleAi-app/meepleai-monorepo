using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries.GetUserRateLimitStatus;

/// <summary>
/// Query to retrieve complete rate limit status for a specific user.
/// Returns current usage, effective limits, override information, and computed availability.
/// </summary>
public sealed record GetUserRateLimitStatusQuery : IRequest<UserRateLimitStatusDto>
{
    /// <summary>
    /// The user ID to get rate limit status for.
    /// </summary>
    public required Guid UserId { get; init; }
}
