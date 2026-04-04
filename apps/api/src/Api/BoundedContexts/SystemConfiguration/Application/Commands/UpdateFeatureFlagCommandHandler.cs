using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Handler for updating a feature flag with optional role and tier restrictions.
/// Issue #3073: Extended to support tier-based feature flags.
/// </summary>
internal class UpdateFeatureFlagCommandHandler : ICommandHandler<UpdateFeatureFlagCommand, MediatR.Unit>
{
    private readonly IFeatureFlagService _featureFlagService;

    public UpdateFeatureFlagCommandHandler(IFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
    }

    public async Task<MediatR.Unit> Handle(UpdateFeatureFlagCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Tier-based update takes priority
        if (command.Tier != null)
        {
            if (command.Enabled)
            {
                await _featureFlagService.EnableFeatureForTierAsync(command.FeatureName, command.Tier, command.UserId).ConfigureAwait(false);
            }
            else
            {
                await _featureFlagService.DisableFeatureForTierAsync(command.FeatureName, command.Tier, command.UserId).ConfigureAwait(false);
            }
        }
        else
        {
            // Role-based or global update
            if (command.Enabled)
            {
                await _featureFlagService.EnableFeatureAsync(command.FeatureName, command.Role, command.UserId).ConfigureAwait(false);
            }
            else
            {
                await _featureFlagService.DisableFeatureAsync(command.FeatureName, command.Role, command.UserId).ConfigureAwait(false);
            }
        }

        return MediatR.Unit.Value;
    }
}
