namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// One row of <c>GET /api/v1/admin/rag-enhancements</c>: a RAG enhancement, its global state and what it costs
/// in extra credits with the balanced and the fast auxiliary model. The property order is the order of the
/// JSON fields the admin tab reads.
/// </summary>
internal sealed record RagEnhancementListItemDto(
    string Name,
    string FlagKey,
    bool IsGloballyEnabled,
    int ExtraCreditsBalanced,
    int ExtraCreditsFast);

/// <summary>
/// Body of <c>GET /api/v1/rag/enhancements/estimate</c>: the enhancements active for a tier and what a query
/// with them costs in extra credits.
/// </summary>
internal sealed record RagEnhancementCostEstimateDto(
    string ActiveEnhancements,
    IReadOnlyList<string> ActiveFlags,
    int ExtraCreditsPerQuery,
    bool UseBalancedAuxModel);
