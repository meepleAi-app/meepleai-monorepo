using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Orchestrates hybrid search across multiple games in parallel.
///
/// Rationale (R3 verdict b): the per-game retrieval accepts a single <c>gameId</c> — the
/// keyword/BM25 search is hard-wired per-game at the infrastructure level (PostgreSQL FTS
/// filtered by game). This wrapper issues one per-game retrieval per accessible game id in
/// parallel, then re-fuses the aggregated candidates globally (#3735, see
/// <see cref="MultiGameHybridSearchService"/>).
///
/// #4168 (5e-c-2): the per-game retrieval is the same one the single-game answer uses — a
/// <c>SearchQuery</c> handled by <c>SearchQueryHandler</c>, with the agent profile's vector threshold
/// and fusion weights (<see cref="PerGameRetrievalParameters"/>). Before, it was
/// <see cref="IHybridSearchService.SearchAsync"/> with its own threshold and fusion adapter.
///
/// Issue #1661 — cross-game KB search.
/// </summary>
internal interface IMultiGameHybridSearchService
{
    /// <summary>
    /// Searches across all specified games in parallel and returns a ranked, deduplicated list.
    /// </summary>
    /// <param name="query">The user's natural-language query.</param>
    /// <param name="gameIds">Accessible game IDs (pre-filtered by RBAC). Empty → returns empty immediately (EC-1).</param>
    /// <param name="limit">Maximum number of results to return (hard cap, EC-7).</param>
    /// <param name="perGame">
    /// #4168 (5e-c-2): i parametri del recupero per gioco, gli stessi della risposta per gioco (soglia
    /// del braccio vettoriale e pesi della fusione, dal profilo pubblicato).
    /// </param>
    /// <param name="mode">Search mode forwarded to each per-game call.</param>
    /// <param name="minScore">Minimum hybrid score; results below this threshold are discarded.</param>
    /// <param name="pdfDocumentIds">
    /// Optional document allowlist (Issue #1686, D-4). When non-null, each per-game search
    /// restricts its hit set to <c>PdfDocumentEntity.Id</c> values in this list. Forwarded to
    /// the per-game <c>SearchQuery</c> as <c>PdfDocumentIds</c>.
    /// When null, no document filter is applied (legacy behaviour, D-3).
    /// </param>
    /// <param name="cancellationToken">Propagated to all parallel per-game calls (EC-3 resource safety).</param>
    /// <returns>
    /// Ranked list of <see cref="MultiGameSearchResultItem"/> ordered by
    /// <c>HybridScore DESC, ChunkIndex ASC, PdfDocumentId ASC</c> (EC-4 stable cursor).
    /// </returns>
    Task<IReadOnlyList<MultiGameSearchResultItem>> SearchAsync(
        string query,
        IReadOnlyList<Guid> gameIds,
        int limit,
        PerGameRetrievalParameters perGame,
        SearchMode mode = SearchMode.Hybrid,
        double minScore = 0.0,
        IReadOnlyList<Guid>? pdfDocumentIds = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// I parametri del recupero per gioco dentro la ricerca fra giochi (#4168, 5e-c-2). Il passo per gioco
/// usa lo stesso recupero della risposta per gioco, quindi gli stessi numeri: la soglia del braccio
/// vettoriale e i pesi della fusione del profilo pubblicato.
/// </summary>
internal sealed record PerGameRetrievalParameters(double VectorMinScore, float VectorWeight, float KeywordWeight)
{
    public static PerGameRetrievalParameters From(AgentProfileContent profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new((double)profile.MinScore, (float)profile.VectorWeight, (float)profile.KeywordWeight);
    }
}

/// <summary>
/// A single search result from the cross-game hybrid search.
/// Carries the origin <see cref="GameId"/> so downstream handlers (Task 4 enrichment) can
/// batch-join <c>PdfDocument → SharedGame</c> in a single EF query without N+1 round-trips.
/// </summary>
internal sealed record MultiGameSearchResultItem
{
    /// <summary>Game this chunk belongs to (origin gameId from the per-game search call).</summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// Composite chunk identifier: "{PdfDocumentId}_{ChunkIndex}".
    /// Mirrors the <c>ChunkId</c> produced by <see cref="IHybridSearchService"/>.
    /// </summary>
    public required string ChunkId { get; init; }

    /// <summary>PDF document (VectorDocument) that contains this chunk.</summary>
    public required string PdfDocumentId { get; init; }

    /// <summary>Zero-based chunk position within the document.</summary>
    public required int ChunkIndex { get; init; }

    /// <summary>1-based page number; null when not available from the vector store.</summary>
    public int? PageNumber { get; init; }

    /// <summary>Raw text snippet of the chunk (used for RAG context assembly).</summary>
    public required string Content { get; init; }

    /// <summary>
    /// #3740: ISO 639-1 language of the chunk, from <c>pgvector_embeddings.lang</c>. Null when the
    /// candidate came from the keyword arm alone. Emitted in the [RAG-TUNE] dump so the offline
    /// bench can group candidates by language — the question the previous cycle could not answer,
    /// because the column was absent from the adapter projection and read back as "en" for every row.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Final hybrid score after RRF fusion within the game.
    /// All per-game scores share the same scale (sum of weighted 1/(k+rank) terms).
    /// Used for cross-game ranking via a single sort pass.
    /// </summary>
    public required float HybridScore { get; init; }

    /// <summary>Individual vector similarity score; null for Keyword-only mode.</summary>
    public float? VectorScore { get; init; }

    /// <summary>Individual keyword relevance score; null for Semantic-only mode.</summary>
    public float? KeywordScore { get; init; }

    /// <summary>Keyword terms matched (for frontend highlighting).</summary>
    public IReadOnlyList<string> MatchedTerms { get; init; } = Array.Empty<string>();

    /// <summary>Search mode used for this result.</summary>
    public required SearchMode Mode { get; init; }
}
