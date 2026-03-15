using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

internal interface IRagEnhancementService
{
    Task<RagEnhancement> GetActiveEnhancementsAsync(UserTier userTier, CancellationToken cancellationToken = default);
    Task<int> EstimateExtraCreditsAsync(RagEnhancement enhancements, CancellationToken cancellationToken = default);
    Task<bool> UseBalancedAuxModelAsync(CancellationToken cancellationToken = default);
}
