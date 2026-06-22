using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.SearchDocumentChunks;

/// <summary>
/// A single scored hit from the per-document similarity search.
/// </summary>
internal sealed record DocChunkSearchHit(
    int ChunkIndex,
    int? PageNumber,
    double Score,
    string Snippet);

/// <summary>
/// Result DTO returned by <see cref="SearchDocumentChunksByVectorQuery"/>.
/// <c>Results</c> is ordered by <c>Score</c> descending.
/// <c>ErrorMessage</c> is non-null when the document is not indexed or embedding fails.
/// </summary>
internal sealed record SearchDocumentChunksResultDto(
    IReadOnlyList<DocChunkSearchHit> Results,
    string? ErrorMessage);

/// <summary>
/// Query: semantic similarity-search scoped to a single PDF document.
/// Returns chunks ranked by cosine-similarity score (descending).
/// Issue #1653: F3-FU-4 — per-document scored similarity-search.
/// </summary>
internal sealed record SearchDocumentChunksByVectorQuery(
    Guid PdfDocumentId,
    string Query,
    int TopK,
    double MinScore) : IQuery<SearchDocumentChunksResultDto>;
