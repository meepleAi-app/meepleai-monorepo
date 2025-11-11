using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to perform vector/hybrid search.
/// </summary>
public record SearchQuery(
    Guid GameId,
    string Query,
    int TopK = 5,
    double MinScore = 0.7,
    string SearchMode = "hybrid", // "vector", "keyword", "hybrid"
    string Language = "en"
) : IQuery<List<SearchResultDto>>;
