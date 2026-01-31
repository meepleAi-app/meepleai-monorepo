using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to disable a feature flag for a specific tier.
/// Issue #3073: Tier-based feature flags (Free/Normal/Premium).
/// </summary>
internal record DisableFeatureForTierCommand(
    string FeatureName,
    UserTier Tier,
    string? UserId = null
) : ICommand<MediatR.Unit>;
