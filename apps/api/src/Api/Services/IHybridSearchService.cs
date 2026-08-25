using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// Interface for hybrid search combining vector similarity (pgvector) with keyword matching (PostgreSQL FTS).
/// Uses Reciprocal Rank Fusion (RRF) algorithm to merge and rank results.
/// Part of AI-14 implementation.
/// </summary>
internal interface IHybridSearchService
{
    /// <summary>
    /// Performs hybrid search using specified search mode.
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="gameId">Game ID to filter results</param>
    /// <param name="mode">Search mode: Semantic (vector only), Keyword (full-text only), or Hybrid (combined)</param>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="documentIds">Optional document IDs to filter sources (Issue #2051)</param>
    /// <param name="vectorWeight">Weight for vector search scores (default: 0.7)</param>
    /// <param name="keywordWeight">Weight for keyword search scores (default: 0.3)</param>
    /// <param name="keywordMinScore">Minimum ts_rank_cd score for keyword results to filter low-relevance matches like ToC entries (default: 0.0)</param>
    /// <param name="queryRoleHint">Phase D (D6): user intent role hint; chunks whose RoleTags overlap receive a fixed RRF score boost (default: None = no-op)</param>
    /// <param name="precomputedQueryEmbedding">
    /// #3786: l'embedding della query già calcolato dal chiamante. Lo passa solo chi fa un fan-out
    /// su più giochi, dove il vettore è lo stesso per tutti e ricalcolarlo per gioco costa una
    /// chiamata HTTP di ~1,4 s a testa. <c>null</c> (il default) = generalo qui, che è il
    /// comportamento giusto per una ricerca a gioco singolo.
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Hybrid search results with RRF-fused scores</returns>
    Task<List<HybridSearchResult>> SearchAsync(
        string query,
        Guid gameId,
        SearchMode mode = SearchMode.Hybrid,
        int limit = 10,
        List<Guid>? documentIds = null,
        float vectorWeight = 0.7f,
        float keywordWeight = 0.3f,
        double keywordMinScore = 0.0,
        GameBookRole queryRoleHint = GameBookRole.None,
        QueryEmbedding? precomputedQueryEmbedding = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// L'embedding di una query, calcolato <b>una volta sola</b> a monte di un fan-out cross-gioco
/// (#3786).
/// </summary>
/// <remarks>
/// <para>
/// Il vettore di una query è uno solo per l'intera richiesta, ma nasceva dentro
/// <c>ExecuteVectorSearchAsync</c>, cioè dentro il ciclo per-gioco: un <c>ask/global</c> su ~160
/// giochi lo ricalcolava ~160 volte. Misurate <b>1546 richieste al servizio di embedding per 11
/// query</b>, a ~1,4 s l'una, su un percorso che quelle chiamate dominano interamente.
/// </para>
/// <para>
/// I tre stati sono distinti di proposito, e il terzo è il motivo per cui questo non è un semplice
/// <c>float[]?</c>: se la generazione a monte fallisce, i giochi a valle devono registrare la
/// degradazione <b>senza riprovare</b>. Con un vettore nullo indistinguibile da «non fornito», un
/// servizio di embedding irraggiungibile avrebbe prodotto ~160 tentativi falliti per richiesta —
/// il comportamento che questa correzione esiste per togliere, riproposto nel caso peggiore.
/// </para>
/// <list type="bullet">
/// <item><c>null</c> (il parametro non passato) — nessun calcolo a monte: chi cerca lo genera da
/// sé. È il percorso a gioco singolo, dove una chiamata è il costo giusto.</item>
/// <item><see cref="Succeeded"/> — usa <see cref="Vector"/>, nessuna chiamata HTTP.</item>
/// <item><see cref="Failure"/> — a monte è già fallito: registra l'esito e fermati.</item>
/// </list>
/// </remarks>
internal sealed class QueryEmbedding
{
    private QueryEmbedding(IReadOnlyList<float>? vector) => Vector = vector;

    /// <summary>Il vettore della query, o <c>null</c> se la generazione è fallita.</summary>
    public IReadOnlyList<float>? Vector { get; }

    /// <summary>Se la generazione a monte è riuscita.</summary>
    public bool Succeeded => Vector is not null;

    /// <summary>Un embedding generato con successo a monte del fan-out.</summary>
    public static QueryEmbedding From(IReadOnlyList<float> vector) => new(vector);

    /// <summary>
    /// La generazione a monte è fallita. Chi cerca deve registrare l'esito e restituire vuoto,
    /// <b>senza</b> ritentare: il tentativo è già stato fatto una volta per l'intera richiesta.
    /// </summary>
    public static QueryEmbedding Failure { get; } = new(null);
}

/// <summary>
/// Search modes for hybrid search.
/// </summary>
internal enum SearchMode
{
    /// <summary>
    /// Vector similarity search only (semantic search via pgvector embeddings).
    /// Best for: Natural language questions, conceptual queries.
    /// </summary>
    Semantic,

    /// <summary>
    /// Keyword search only (PostgreSQL full-text search via tsvector).
    /// Best for: Exact terminology, specific rule names, phrase matching.
    /// </summary>
    Keyword,

    /// <summary>
    /// Hybrid search combining vector and keyword results using RRF fusion.
    /// Best for: General queries benefiting from both semantic understanding and exact matching.
    /// Default mode.
    /// </summary>
    Hybrid
}

/// <summary>
/// Result from hybrid search with RRF-fused scores.
/// </summary>
internal record HybridSearchResult
{
    public required string ChunkId { get; init; }
    public required string Content { get; init; }
    public required string PdfDocumentId { get; init; }
    public required Guid GameId { get; init; }
    public required int ChunkIndex { get; init; }
    public int? PageNumber { get; init; }

    /// <summary>
    /// Final hybrid score computed using Reciprocal Rank Fusion (RRF).
    /// Combines vector similarity rank and keyword relevance rank.
    /// Higher score = more relevant result.
    /// </summary>
    public required float HybridScore { get; init; }

    /// <summary>
    /// Vector similarity score from pgvector (0-1 range, cosine similarity).
    /// Null if SearchMode.Keyword used.
    /// </summary>
    public float? VectorScore { get; init; }

    /// <summary>
    /// Keyword relevance score from PostgreSQL ts_rank_cd.
    /// Null if SearchMode.Semantic used.
    /// </summary>
    public float? KeywordScore { get; init; }

    /// <summary>
    /// #3740: ISO 639-1 language of the chunk, sourced from the vector arm
    /// (<c>pgvector_embeddings.lang</c>). Null for a keyword-only candidate.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Vector rank position in vector-only results (1-based).
    /// Used for RRF calculation.
    /// </summary>
    public int? VectorRank { get; init; }

    /// <summary>
    /// Keyword rank position in keyword-only results (1-based).
    /// Used for RRF calculation.
    /// </summary>
    public int? KeywordRank { get; init; }

    /// <summary>
    /// Terms matched by keyword search for frontend highlighting.
    /// </summary>
    public List<string> MatchedTerms { get; init; } = new List<string>();

    /// <summary>
    /// Search mode used to produce this result.
    /// </summary>
    public required SearchMode Mode { get; init; }

    /// <summary>
    /// Phase D (D6): role classification of the underlying text chunk (multi-label bitflag).
    /// Sourced from text_chunks.role_tags (keyword arm) and the denormalized
    /// pgvector_embeddings.role_tags (vector arm); the two are unioned in fusion (Slice C).
    /// <see cref="GameBookRole.None"/> when the chunk is unclassified.
    /// </summary>
    public GameBookRole RoleTags { get; init; } = GameBookRole.None;

    /// <summary>#3270: merged chunk heading (prefers vector arm) for the heading-match boost (nullable).</summary>
    public string? Heading { get; init; }
}
