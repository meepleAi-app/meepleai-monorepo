namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>Cost breakdown by model.</summary>
public sealed record ModelCostDto(
    string ModelId,
    decimal CostUsd,
    int Requests,
    int TotalTokens
);

/// <summary>Cost breakdown by request source.</summary>
public sealed record SourceCostDto(
    string Source,
    decimal CostUsd,
    int Requests
);

/// <summary>Cost breakdown by user tier/role.</summary>
public sealed record TierCostDto(
    string Tier,
    decimal CostUsd,
    int Requests
);

/// <summary>
/// Aggregated cost breakdown for the admin usage dashboard.
/// Issue #5080: Admin usage page — cost breakdown panel.
/// </summary>
public sealed record UsageCostsDto(
    IReadOnlyList<ModelCostDto> ByModel,
    IReadOnlyList<SourceCostDto> BySource,
    IReadOnlyList<TierCostDto> ByTier,
    decimal TotalCostUsd,
    int TotalRequests,
    string Period
);
