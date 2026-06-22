using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to perform vector/hybrid search.
/// Issue #2051: Supports document filtering via DocumentIds.
/// Issue #563: Optional <see cref="QueryVector"/> lets callers pass a pre-computed
/// embedding to skip the duplicate embedding call inside <c>SearchQueryHandler</c>.
/// When null, the handler computes the embedding itself (legacy/default path).
/// Phase D (D6): Optional <see cref="QueryRoleHint"/> lets the retrieval layer bias the
/// re-ranker toward chunks whose <c>role_tags</c> overlap with the user's classified intent.
/// Default <see cref="GameBookRole.None"/> = no-op (back-compat).
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
    string? UserRole = null,
    // Issue #563: Pre-computed query embedding to avoid duplicate generation.
    // Caller is responsible for ensuring the vector matches Query + Language and was produced
    // by the same embedding model the search handler would use. Null = handler computes its own.
    IReadOnlyList<float>? QueryVector = null,
    // Phase D — RAG role-aware (D6): user intent classification routed to retrieval re-ranker.
    // Chunks whose role_tags overlap with this hint receive a fixed score boost during RRF fusion.
    // None = legacy/no-op behavior.
    GameBookRole QueryRoleHint = GameBookRole.None
) : IQuery<List<SearchResultDto>>;
