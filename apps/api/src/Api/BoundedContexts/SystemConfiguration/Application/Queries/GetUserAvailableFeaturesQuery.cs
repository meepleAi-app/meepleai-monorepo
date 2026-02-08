using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to get all features available to a specific user based on role and tier.
/// Issue #3674: Feature Flags Verification - User features endpoint
/// </summary>
internal sealed record GetUserAvailableFeaturesQuery : IQuery<IReadOnlyList<UserFeatureDto>>
{
    /// <summary>
    /// User ID to check feature access for
    /// </summary>
    public required Guid UserId { get; init; }
}
