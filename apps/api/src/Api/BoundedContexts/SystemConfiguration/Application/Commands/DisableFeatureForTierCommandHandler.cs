using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Handler for disabling a feature flag for a specific tier.
/// Issue #3073: Tier-based feature flags (Free/Normal/Premium).
/// </summary>
internal class DisableFeatureForTierCommandHandler : ICommandHandler<DisableFeatureForTierCommand, MediatR.Unit>
{
    private readonly IFeatureFlagService _featureFlagService;

    public DisableFeatureForTierCommandHandler(IFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
    }

    public async Task<MediatR.Unit> Handle(DisableFeatureForTierCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await _featureFlagService.DisableFeatureForTierAsync(command.FeatureName, command.Tier, command.UserId).ConfigureAwait(false);
        return MediatR.Unit.Value;
    }
}
