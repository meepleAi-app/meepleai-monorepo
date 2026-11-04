using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.RagEnhancements;

/// <summary>
/// Reads the current state of the flag and writes the opposite one, with the admin as author. The reading
/// follows the same hierarchy the pipeline uses: for a tier, tier row, then global row, then "on"; for the
/// global flag, global row, then "off".
/// </summary>
internal sealed class ToggleRagEnhancementCommandHandler : ICommandHandler<ToggleRagEnhancementCommand, bool>
{
    private readonly IFeatureFlagService _featureFlags;

    public ToggleRagEnhancementCommandHandler(IFeatureFlagService featureFlags)
    {
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
    }

    public async Task<bool> Handle(ToggleRagEnhancementCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var adminId = command.AdminId.ToString();

        if (command.Tier is null)
        {
            var wasEnabled = await _featureFlags.IsEnabledAsync(command.FeatureKey).ConfigureAwait(false);
            if (wasEnabled)
                await _featureFlags.DisableFeatureAsync(command.FeatureKey, userId: adminId).ConfigureAwait(false);
            else
                await _featureFlags.EnableFeatureAsync(command.FeatureKey, userId: adminId).ConfigureAwait(false);

            return !wasEnabled;
        }

        var tierWasEnabled = await _featureFlags.IsEnabledForTierAsync(command.FeatureKey, command.Tier).ConfigureAwait(false);
        if (tierWasEnabled)
            await _featureFlags.DisableFeatureForTierAsync(command.FeatureKey, command.Tier, adminId).ConfigureAwait(false);
        else
            await _featureFlags.EnableFeatureForTierAsync(command.FeatureKey, command.Tier, adminId).ConfigureAwait(false);

        return !tierWasEnabled;
    }
}
