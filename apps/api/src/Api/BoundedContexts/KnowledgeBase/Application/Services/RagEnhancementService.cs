using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

internal sealed class RagEnhancementService : IRagEnhancementService
{
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<RagEnhancementService> _logger;

    public RagEnhancementService(
        IFeatureFlagService featureFlagService,
        ILogger<RagEnhancementService> logger)
    {
        _featureFlagService = featureFlagService;
        _logger = logger;
    }

    public async Task<RagEnhancement> GetActiveEnhancementsAsync(UserTier userTier, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userTier);

        var active = RagEnhancement.None;

        foreach (var flag in Enum.GetValues<RagEnhancement>())
        {
            if (flag == RagEnhancement.None) continue;
            cancellationToken.ThrowIfCancellationRequested();

            var key = flag.ToFeatureFlagKey();
            var enabled = await _featureFlagService.IsEnabledForTierAsync(key, userTier).ConfigureAwait(false);

            if (enabled)
            {
                active |= flag;
                _logger.LogDebug("RAG enhancement {Enhancement} enabled for tier {Tier}", flag, userTier.Value);
            }
        }

        _logger.LogInformation("Active RAG enhancements for tier {Tier}: {Enhancements}", userTier.Value, active);
        return active;
    }

    public async Task<int> EstimateExtraCreditsAsync(RagEnhancement enhancements, CancellationToken cancellationToken = default)
    {
        var useBalanced = await UseBalancedAuxModelAsync(cancellationToken).ConfigureAwait(false);

        var totalCredits = 0;
        foreach (var flag in enhancements.GetIndividualFlags())
        {
            totalCredits += flag.GetExtraCredits(useBalanced);
        }

        return totalCredits;
    }

    public async Task<bool> UseBalancedAuxModelAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _featureFlagService.IsEnabledAsync(FeatureFlagConstants.RagAuxModelKey).ConfigureAwait(false);
    }
}
