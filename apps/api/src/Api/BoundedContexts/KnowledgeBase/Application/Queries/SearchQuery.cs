using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to perform vector/hybrid search.
/// Issue #2051: Supports document filtering via DocumentIds
/// </summary>
internal record SearchQuery(
    Guid GameId,
    string Query,
    int TopK = 5,
    double MinScore = 0.55, // Adjusted for mxbai-embed-large
    string SearchMode = "hybrid", // "vector", "keyword", "hybrid"
    string Language = "en",
    IReadOnlyList<Guid>? DocumentIds = null, // Issue #2051: Filter by document IDs (null = all)
    Guid? UserId = null,
    string? UserRole = null
) : IQuery<List<SearchResultDto>>;
