using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to perform semantic vector search over pgvector embeddings.
/// Supports single-game and cross-game search.
/// Task 4: Qdrant → pgvector migration.
/// </summary>
internal sealed record VectorSemanticSearchQuery(
    string Query,
    int Limit,
    Guid? GameId) : IQuery<VectorSemanticSearchResultDto>;

/// <summary>
/// Result DTO for VectorSemanticSearchQuery.
/// </summary>
internal sealed record VectorSemanticSearchResultDto(
    List<VectorSearchResultItem> Results,
    string? ErrorMessage);

/// <summary>
/// A single item in the semantic search result set.
/// </summary>
internal sealed record VectorSearchResultItem(
    Guid DocumentId,
    string Text,
    int ChunkIndex,
    int PageNumber);
