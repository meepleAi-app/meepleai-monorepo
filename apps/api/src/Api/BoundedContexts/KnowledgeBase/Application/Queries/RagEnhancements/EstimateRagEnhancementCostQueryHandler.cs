using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.RagEnhancements;

internal sealed class EstimateRagEnhancementCostQueryHandler
    : IQueryHandler<EstimateRagEnhancementCostQuery, RagEnhancementCostEstimateDto>
{
    private readonly IRagEnhancementService _enhancementService;

    public EstimateRagEnhancementCostQueryHandler(IRagEnhancementService enhancementService)
    {
        _enhancementService = enhancementService ?? throw new ArgumentNullException(nameof(enhancementService));
    }

    public async Task<RagEnhancementCostEstimateDto> Handle(
        EstimateRagEnhancementCostQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var active = await _enhancementService.GetActiveEnhancementsAsync(query.Tier, cancellationToken).ConfigureAwait(false);
        var extraCredits = await _enhancementService.EstimateExtraCreditsAsync(active, cancellationToken).ConfigureAwait(false);
        var useBalancedAuxModel = await _enhancementService.UseBalancedAuxModelAsync(cancellationToken).ConfigureAwait(false);

        return new RagEnhancementCostEstimateDto(
            ActiveEnhancements: active.ToString(),
            ActiveFlags: active.GetIndividualFlags().Select(f => f.ToString()).ToList(),
            ExtraCreditsPerQuery: extraCredits,
            UseBalancedAuxModel: useBalancedAuxModel);
    }
}
