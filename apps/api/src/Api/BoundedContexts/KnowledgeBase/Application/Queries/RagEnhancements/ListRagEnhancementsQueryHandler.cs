using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.RagEnhancements;

internal sealed class ListRagEnhancementsQueryHandler
    : IQueryHandler<ListRagEnhancementsQuery, IReadOnlyList<RagEnhancementListItemDto>>
{
    private readonly IFeatureFlagService _featureFlags;

    public ListRagEnhancementsQueryHandler(IFeatureFlagService featureFlags)
    {
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
    }

    public async Task<IReadOnlyList<RagEnhancementListItemDto>> Handle(
        ListRagEnhancementsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = new List<RagEnhancementListItemDto>();
        foreach (var enhancement in Enum.GetValues<RagEnhancement>())
        {
            if (enhancement == RagEnhancement.None) continue;

            var flagKey = enhancement.ToFeatureFlagKey();
            var isEnabled = await _featureFlags.IsEnabledAsync(flagKey).ConfigureAwait(false);
            result.Add(new RagEnhancementListItemDto(
                Name: enhancement.ToString(),
                FlagKey: flagKey,
                IsGloballyEnabled: isEnabled,
                ExtraCreditsBalanced: enhancement.GetExtraCredits(useBalancedForAux: true),
                ExtraCreditsFast: enhancement.GetExtraCredits(useBalancedForAux: false)));
        }

        return result;
    }
}
