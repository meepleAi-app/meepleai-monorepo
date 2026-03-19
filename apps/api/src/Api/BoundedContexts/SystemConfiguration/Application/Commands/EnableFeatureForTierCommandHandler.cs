using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Handler for enabling a feature flag for a specific tier.
/// Issue #3073: Tier-based feature flags (Free/Normal/Premium).
/// </summary>
internal class EnableFeatureForTierCommandHandler : ICommandHandler<EnableFeatureForTierCommand, MediatR.Unit>
{
    private readonly IFeatureFlagService _featureFlagService;

    public EnableFeatureForTierCommandHandler(IFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
    }

    public async Task<MediatR.Unit> Handle(EnableFeatureForTierCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await _featureFlagService.EnableFeatureForTierAsync(command.FeatureName, command.Tier, command.UserId).ConfigureAwait(false);
        return MediatR.Unit.Value;
    }
}
