using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to update session tier limits configuration.
/// Creates configurations if they don't exist, updates if they do.
/// Issue #3070: Session limits backend implementation.
/// </summary>
/// <param name="FreeTierLimit">Maximum active sessions for free tier (must be >= 1)</param>
/// <param name="NormalTierLimit">Maximum active sessions for normal tier (must be >= 1)</param>
/// <param name="PremiumTierLimit">Maximum active sessions for premium tier (-1 = unlimited, or >= 1)</param>
/// <param name="UpdatedByUserId">User performing the update</param>
internal record UpdateSessionLimitsCommand(
    int FreeTierLimit,
    int NormalTierLimit,
    int PremiumTierLimit,
    Guid UpdatedByUserId
) : ICommand<SessionLimitsDto>;
