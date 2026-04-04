namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Distribution breakdowns for AI usage (model, provider, operation).
/// Issue #94: C3 Editor Self-Service AI Usage Page
/// </summary>
public record AiUsageDistributionsDto(
    IReadOnlyList<DistributionItemDto> Models,
    IReadOnlyList<DistributionItemDto> Providers,
    IReadOnlyList<DistributionItemDto> Operations
);

/// <summary>
/// A single distribution item (name + count + percentage).
/// </summary>
public record DistributionItemDto(
    string Name,
    int Count,
    double Percentage
);
