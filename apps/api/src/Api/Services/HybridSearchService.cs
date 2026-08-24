using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Helpers;
using Api.Infrastructure;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using KbEntities = Api.BoundedContexts.KnowledgeBase.Domain.Entities;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// Hybrid search service combining vector similarity (pgvector) with keyword matching (PostgreSQL FTS).
/// Implements Reciprocal Rank Fusion (RRF) algorithm for score merging.
/// Part of AI-14 implementation.
/// </summary>
internal class HybridSearchService : IHybridSearchService
{
    private readonly IKeywordSearchService _keywordSearchService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreAdapter _vectorStore;
    private readonly ILogger<HybridSearchService> _logger;
    private readonly HybridSearchConfiguration _config;

    public HybridSearchService(
        IKeywordSearchService keywordSearchService,
        IEmbeddingService embeddingService,
        IVectorStoreAdapter vectorStore,
        ILogger<HybridSearchService> logger,
        IOptions<HybridSearchConfiguration> config)
    {
        _keywordSearchService = keywordSearchService;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<List<HybridSearchResult>> SearchAsync(
        string query,
        Guid gameId,
        SearchMode mode = SearchMode.Hybrid,
        int limit = 10,
        List<Guid>? documentIds = null,
        float vectorWeight = 0.7f,
        float keywordWeight = 0.3f,
        double keywordMinScore = 0.0,
        GameBookRole queryRoleHint = GameBookRole.None,
        CancellationToken cancellationToken = default)
    {
        // Issue #1445: Use centralized query validation
        var queryError = QueryValidator.ValidateQuery(query);
        if (queryError != null)
        {
            _logger.LogWarning("Invalid query provided to HybridSearchService: {Error}", queryError);
            // Return empty results for invalid queries (maintains existing behavior)
            return new List<HybridSearchResult>();
        }

        // Security: Cap limit parameter to prevent resource exhaustion
        var safeLimit = Math.Min(Math.Max(limit, 1), 100); // Min: 1, Max: 100

        _logger.LogInformation(
            "Hybrid search started: query='{Query}', gameId={GameId}, mode={Mode}, documentFilter={HasFilter}, vectorWeight={VectorWeight}, keywordWeight={KeywordWeight}, limit={Limit}, roleHint={RoleHint}",
            query, gameId, mode, documentIds != null, vectorWeight, keywordWeight, limit, queryRoleHint);

        try
        {
            switch (mode)
            {
                case SearchMode.Semantic:
                    return await SearchSemanticOnlyAsync(query, gameId, safeLimit, documentIds, queryRoleHint, cancellationToken).ConfigureAwait(false);

                case SearchMode.Keyword:
                    return await SearchKeywordOnlyAsync(query, gameId, safeLimit, documentIds, keywordMinScore, queryRoleHint, cancellationToken).ConfigureAwait(false);

                case SearchMode.Hybrid:
                    return await SearchHybridAsync(query, gameId, safeLimit, vectorWeight, keywordWeight, documentIds, keywordMinScore, queryRoleHint, cancellationToken).ConfigureAwait(false);

                default:
                    throw new ArgumentException($"Unsupported search mode: {mode}", nameof(mode));
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY: Coordinates vector (pgvector) and keyword (PostgreSQL FTS) search with centralized exception logging
#pragma warning restore S125
        catch (Exception ex)
        {
            // Issue #1444: Use centralized exception handling (log and re-throw pattern)
            // Service entry point that coordinates vector and keyword searches
            RagExceptionHandler.LogAndRethrow(ex, _logger, "hybrid search", query, mode);
            throw; // Unreachable, but required for compiler
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    /// <summary>
    /// Performs vector-only semantic search using pgvector.
    /// Issue #1391: applies the same role-match boost as the keyword path when
    /// <paramref name="queryRoleHint"/> overlaps the chunk's denormalized RoleTags
    /// (pgvector_embeddings now carries role_tags via the AddRoleTagsToPgVectorEmbeddings
    /// migration + PgVectorStoreAdapter ingestion writes). Re-sorts by HybridScore so
    /// boosted chunks float to the top instead of staying anchored to the raw vector rank.
    /// </summary>
    private async Task<List<HybridSearchResult>> SearchSemanticOnlyAsync(
        string query,
        Guid gameId,
        int limit,
        List<Guid>? documentIds,
        GameBookRole queryRoleHint,
        CancellationToken cancellationToken)
    {
        var vectorResults = await ExecuteVectorSearchAsync(
            query, gameId, limit, documentIds, cancellationToken).ConfigureAwait(false);

        var results = vectorResults.Select((r, index) =>
        {
            var embedding = r.Embedding;
            var chunkRoleTags = (GameBookRole)embedding.RoleTags;
            var baseScore = 1.0f / (index + 1); // normalized rank score
            var roleBoost = FusionSignals.ComputeRoleMatchBoost(queryRoleHint, chunkRoleTags);
            return new HybridSearchResult
            {
                // RRF fusion-key fix: key on the resolved PdfDocumentId (now populated by the scored
                // pgvector search) so Semantic-mode results share the Hybrid/keyword identity and
                // surface the real pdf id in citations + the global-KB-search enrichment join.
                ChunkId = $"{embedding.PdfDocumentId}_{embedding.ChunkIndex}",
                Content = embedding.TextContent,
                PdfDocumentId = embedding.PdfDocumentId.ToString(),
                GameId = gameId,
                ChunkIndex = embedding.ChunkIndex,
                PageNumber = embedding.PageNumber,
                // HybridScore stays the rank-based base (+ role boost) so within-game ordering
                // is unchanged. VectorScore carries the RAW cosine (#2568) so the cross-game
                // merge can break rank-only ties by true query relevance.
                HybridScore = baseScore + roleBoost,
                VectorScore = (float)r.Score,
                KeywordScore = null,
                VectorRank = index + 1,
                KeywordRank = null,
                MatchedTerms = new List<string>(),
                Mode = SearchMode.Semantic,
                RoleTags = chunkRoleTags,
                Language = embedding.Language
            };
        }).ToList();

        // Re-sort if the role boost actually moved anything (no-op when queryRoleHint == None).
        if (queryRoleHint != GameBookRole.None)
        {
            results = results
                .OrderByDescending(r => r.HybridScore)
                .ToList();
        }

        return results;
    }

    /// <summary>
    /// Performs keyword-only search using PostgreSQL full-text search.
    /// Phase D (D6): applies the role-match boost on top of the raw ts_rank_cd score when
    /// <paramref name="queryRoleHint"/> overlaps a chunk's RoleTags.
    /// </summary>
    private async Task<List<HybridSearchResult>> SearchKeywordOnlyAsync(
        string query,
        Guid gameId,
        int limit,
        List<Guid>? documentIds,
        double keywordMinScore,
        GameBookRole queryRoleHint,
        CancellationToken cancellationToken)
    {
        var keywordResults = await _keywordSearchService.SearchAsync(
            query,
            gameId,
            limit,
            phraseSearch: query.Contains('"'), // Enable phrase search if query has quotes
            boostTerms: _config.BoostTerms,
            minScore: keywordMinScore,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Issue #2051: Filter by document IDs if specified
        var filteredResults = documentIds == null
            ? keywordResults
            : keywordResults.Where(r => documentIds.Any(id => string.Equals(id.ToString(), r.PdfDocumentId, StringComparison.Ordinal))).ToList();

        _logger.LogInformation(
            "Keyword search: {TotalResults} results from PostgreSQL, {FilteredResults} after document filter",
            keywordResults.Count, filteredResults.Count);

        // Phase D (D6): build hybrid results then re-rank by role-boosted score.
        var hybridResults = filteredResults.Select((r, index) =>
        {
            var roleBoost = FusionSignals.ComputeRoleMatchBoost(queryRoleHint, r.RoleTags);
            return new HybridSearchResult
            {
                ChunkId = r.ChunkId,
                Content = r.Content,
                PdfDocumentId = r.PdfDocumentId,
                GameId = r.GameId,
                ChunkIndex = r.ChunkIndex,
                PageNumber = r.PageNumber,
                HybridScore = r.RelevanceScore + roleBoost, // role-aware re-ranking
                VectorScore = null,
                KeywordScore = r.RelevanceScore,
                VectorRank = null,
                KeywordRank = index + 1,
                MatchedTerms = r.MatchedTerms,
                Mode = SearchMode.Keyword,
                RoleTags = r.RoleTags
            };
        }).ToList();

        // Re-sort if the role boost actually moved anything (no-op when queryRoleHint == None).
        if (queryRoleHint != GameBookRole.None)
        {
            hybridResults = hybridResults
                .OrderByDescending(r => r.HybridScore)
                .ToList();
        }

        return hybridResults;
    }

    /// <summary>
    /// Performs hybrid search combining pgvector semantic and keyword results with RRF fusion.
    /// Vector and keyword searches run in parallel for optimal latency.
    /// Phase D (D6): role-match boost is applied per chunk during fusion when <paramref name="queryRoleHint"/> is not None.
    /// </summary>
    private async Task<List<HybridSearchResult>> SearchHybridAsync(
        string query,
        Guid gameId,
        int limit,
        float vectorWeight,
        float keywordWeight,
        List<Guid>? documentIds,
        double keywordMinScore,
        GameBookRole queryRoleHint,
        CancellationToken cancellationToken)
    {
        var fetchLimit = Math.Max(limit * 2, 20);

        // #3338 WP1c: resolve the per-game FTS config for the heading-term synonym expansion below.
        // SearchAsync resolves the same config internally (one accepted extra GameId-indexed query per
        // hybrid search); threading it in would churn ~15 keyword-mock setups for a non-blocking finding.
        var ftsConfig = await _keywordSearchService
            .ResolveFtsConfigAsync(gameId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // #3768: il titolo serve a ESCLUDERE i suoi token dai termini di heading-match, non a
        // cercarli — questa ricerca gira gia' filtrata per gameId. Seconda query indicizzata per
        // GameId sullo stesso percorso di ResolveFtsConfigAsync; il costo per-gioco di questo
        // percorso e' tracciato in #3786.
        var gameTitle = await _keywordSearchService
            .ResolveGameTitleAsync(gameId, cancellationToken)
            .ConfigureAwait(false);

        // #3786: i due bracci girano in SEQUENZA, non con Task.WhenAll.
        //
        // Entrambi risolvono dallo stesso scope — quello creato per gioco da
        // MultiGameHybridSearchService.SearchGameSafeAsync — quindi condividono la stessa istanza
        // di MeepleAiDbContext: il vettoriale via GetDbConnection() in PgVectorStoreAdapter, il
        // lessicale via SqlQueryRaw in KeywordSearchService. DbContext non e' thread-safe, e
        // sovrapporli produceva:
        //
        //   System.InvalidOperationException: A second operation was started on this context
        //   instance before a previous operation completed.
        //
        // Misurato su staging in una sola raccolta di 11 query: 267 eccezioni e 428 ricerche
        // per-gioco con vectorCount=0 su 1759. Il vettoriale eccepiva, l'eccezione veniva catturata
        // (vedi ExecuteVectorSearchAsync) e la ricerca proseguiva SOLO LESSICALE, senza alcun
        // segnale nel risultato: dall'esterno indistinguibile da un gioco senza corrispondenze.
        //
        // Il parallelismo qui non comprava niente. Il percorso e' dominato dall'embedding della
        // query (~1,4 s di HTTP dentro ExecuteVectorSearchAsync); le due query al DB sono
        // millisecondi, quindi si passa da max(1,4s, ms) a 1,4s + ms. Uno scope per braccio
        // conserverebbe quei millisecondi al prezzo di raddoppiare DbContext e connessioni su un
        // percorso che ne apre gia' uno per gioco (~160 per richiesta cross-gioco).
        //
        // #2480 aveva gia' corretto la stessa classe di errore FRA giochi, dando a ciascuno il
        // proprio scope; questo e' il caso residuo DENTRO un singolo gioco.
        var vectorEmbeddings = await ExecuteVectorSearchAsync(
            query, gameId, fetchLimit, documentIds, cancellationToken).ConfigureAwait(false);

        var keywordResults = await _keywordSearchService.SearchAsync(
            query,
            gameId,
            fetchLimit,
            phraseSearch: query.Contains('"'),
            boostTerms: _config.BoostTerms,
            minScore: keywordMinScore,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Apply document filter to keyword results
        var filteredKeywordResults = documentIds == null
            ? keywordResults
            : keywordResults.Where(r => documentIds.Any(id =>
                string.Equals(id.ToString(), r.PdfDocumentId, StringComparison.Ordinal))).ToList();

        // Convert pgvector results to SearchResultItem for RRF fusion.
        // #2568: carry the raw cosine similarity (was hard-coded 1.0f) so the fused
        // VectorScore reflects true relevance for the cross-game tiebreak. RRF still ranks
        // by list position (Rank), so this does NOT change within-game ordering.
        var vectorItems = vectorEmbeddings.Select(se => new SearchResultItem
        {
            Score = (float)se.Score,
            Text = se.Embedding.TextContent,
            // RRF fusion-key fix: key on the owning PdfDocumentId (resolved by the scored pgvector
            // search) so a chunk found by BOTH arms fuses on the same {PdfDocumentId}_{ChunkIndex}
            // identity the keyword arm uses. (Was VectorDocumentId, which also wrongly surfaced as
            // HybridSearchResult.PdfDocumentId for vector-only citations.)
            PdfId = se.Embedding.PdfDocumentId.ToString(),
            ChunkIndex = se.Embedding.ChunkIndex,
            Page = se.Embedding.PageNumber,
            // Slice C: carry role_tags through so vector-only chunks get the role-match boost in
            // fusion (same cast as the semantic-only path). pgvector already SELECTs role_tags.
            RoleTags = (GameBookRole)se.Embedding.RoleTags,
            // #3270: carry the chunk heading (JOIN-resolved) so vector-arm chunks get the heading boost.
            Heading = se.Embedding.Heading,
            // #3740: carry the chunk language now that the adapter actually SELECTs it.
            Language = se.Embedding.Language
        }).ToArray();

        _logger.LogInformation(
            "Hybrid search: vectorCount={VectorCount}, keywordCount={KeywordCount} (post-filter: {FilteredKeyword})",
            vectorItems.Length, keywordResults.Count, filteredKeywordResults.Count);

        // RRF fusion with both vector AND keyword results
        // Phase D (D6): queryRoleHint enables role-match boost during fusion.
        // #3338 WP1c: expand the #3270 heading-match terms with the game's FTS-language intent synonyms
        // (setup -> preparazione/allestimento) so an English-loanword query boosts a native-lexeme heading.
        // Reuses the ftsConfig resolved once at the top of this method.
        var headingTerms = KeywordSearchService.ExpandHeadingMatchTerms(
            FusionSignals.ExtractHeadingMatchTerms(query), ftsConfig, gameTitle);

        var fusedResults = FuseSearchResults(
            vectorItems,
            filteredKeywordResults,
            gameId,
            vectorWeight,
            keywordWeight,
            _config.RrfConstant ?? FusionSignals.DefaultRrfK,
            queryRoleHint,
            headingTerms);

        LogPerGameArenaForTuning(query, gameId, ftsConfig, headingTerms, queryRoleHint, fusedResults);

        var topResults = fusedResults
            .OrderByDescending(r => r.HybridScore)
            .Take(limit)
            .ToList();

        _logger.LogInformation(
            "Hybrid search completed: returned {ResultCount} fused results (from {TotalFused} total)",
            topResults.Count, fusedResults.Count);

        return topResults;
    }

    /// <summary>
    /// Prefisso stabile della riga di diagnostica per-gioco. Il consumatore offline
    /// (<c>infra/scripts/rag-fusion-bench.py</c>) filtra su questo con <c>grep -F</c>: cambiarlo
    /// rompe l'estrazione.
    /// </summary>
    internal const string PerGameTuningLogPrefix = "[RAG-TUNE-GAME]";

    private static readonly JsonSerializerOptions PerGameTuningJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Emette la scomposizione del punteggio di OGNI candidato fuso di questo gioco, prima della
    /// troncatura a <c>limit</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Perché esiste.</b> Il dump <c>[RAG-TUNE]</c> di <c>MultiGameHybridSearchService</c> mostra
    /// i candidati che <i>arrivano</i> alla fusione globale, mai quelli che questo stadio ha
    /// <i>rifiutato</i>. Su staging, per <c>catan-setup-it</c>, il chunk con le regole di setup è
    /// rango 1 del braccio vettoriale dentro Catan (cosine 0.80544) e non arriva: al suo posto
    /// arriva il colophon <c>catan.com ®</c> (cosine 0.77997, rango 14). Senza questa riga la
    /// diagnosi ha richiesto una lettura del DB per ipotesi, e le prime due — le penalità
    /// moltiplicative — erano entrambe a zero su quel chunk.
    /// </para>
    /// <para>
    /// <b>Perché la scomposizione e non il solo punteggio.</b> `HybridScore` da solo non distingue
    /// una cosine alta da un boost additivo, e i due boost valgono <c>0.15</c> contro un
    /// <c>rrfSum</c> che satura a <c>1/61 = 0.0164</c>: quando uno si attiva, l'ordinamento per
    /// rilevanza smette di contare. I due termini vanno quindi visti separati.
    /// </para>
    /// <para>
    /// <b>Perché tutti i candidati e non il top-K.</b> Il chunk pertinente è, per definizione del
    /// difetto, fuori dal risultato: un dump troncato non mostrerebbe mai ciò che serve. Il volume
    /// è limitato dal <c>fetchLimit</c> dei due bracci (≤ 40 candidati per gioco).
    /// </para>
    /// <para>
    /// <b>Cosa NON porta.</b> Il testo dei chunk: è il grosso del volume e i due fattori che se ne
    /// derivano (<c>lg</c>, <c>nn</c>) sono già qui, precalcolati. Una variante che volesse tararli
    /// deve estendere questo payload.
    /// </para>
    /// <para>
    /// I fattori sono ricalcolati con le stesse funzioni pure usate dalla fusione, sugli stessi
    /// input, quindi coincidono con quelli applicati. Il campo <c>s</c> è invece il punteggio
    /// <i>reale</i> letto dal risultato fuso: è l'àncora con cui la replica offline si valida, e
    /// ricalcolarlo qui la farebbe confermare se stessa.
    /// </para>
    /// </remarks>
    private void LogPerGameArenaForTuning(
        string query,
        Guid gameId,
        string? ftsConfig,
        IReadOnlyList<string>? headingTerms,
        GameBookRole queryRoleHint,
        IReadOnlyList<HybridSearchResult> fused)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
            return;

        var payload = new
        {
            q = query,
            g = gameId,
            f = ftsConfig,
            t = headingTerms,
            n = fused.Count,
            c = fused.Select(r => new
            {
                d = r.PdfDocumentId,
                i = r.ChunkIndex,
                vr = r.VectorRank,
                kr = r.KeywordRank,
                v = r.VectorScore,
                k = r.KeywordScore,
                lg = FusionSignals.ComputeLegendPenaltyFactor(r.Content),
                nn = FusionSignals.ComputeNumberNoiseFactor(r.Content),
                rb = FusionSignals.ComputeRoleMatchBoost(queryRoleHint, r.RoleTags),
                hb = FusionSignals.ComputeHeadingMatchBoost(headingTerms, r.Heading),
                h = r.Heading,
                // #3740: omessa quando è null (candidato solo-lessicale). La porta anche il dump
                // globale: averla qui rende questo payload sufficiente da solo a pilotare la
                // replica offline di FuseGlobally, senza doverlo ri-unire all'altro.
                l = r.Language,
                s = r.HybridScore
            }).ToList()
        };

        _logger.LogDebug(
            "{Prefix} {Payload}",
            PerGameTuningLogPrefix,
            JsonSerializer.Serialize(payload, PerGameTuningJsonOptions));
    }

    /// <summary>
    /// Generates query embedding and performs pgvector cosine similarity search.
    /// Falls back to empty results if embedding generation or search fails (graceful degradation).
    /// </summary>
    private async Task<List<KbEntities.ScoredEmbedding>> ExecuteVectorSearchAsync(
        string query,
        Guid gameId,
        int limit,
        List<Guid>? documentIds,
        CancellationToken cancellationToken)
    {
        try
        {
            // #3737: this is the retrieval question, not indexed text, so it must carry the
            // e5 "query:" prefix. With "passage:" the best chunk of the manual named by the
            // canonical `catan-setup` query sat at cosine rank 10 instead of 1 on the real
            // 56k-chunk corpus — and the vector arm is the signal that distinguishes manuals
            // from one another (see MultiGameHybridSearchService.FuseGlobally, weight 0.7).
            var embeddingResult = await _embeddingService
                .GenerateEmbeddingAsync(query, EmbeddingPurpose.Query, cancellationToken)
                .ConfigureAwait(false);

            if (!embeddingResult.Success || embeddingResult.Embeddings is not { Count: > 0 })
            {
                _logger.LogWarning(
                    "Query embedding generation failed: {Error}. Falling back to keyword-only.",
                    embeddingResult.ErrorMessage);
                return new List<KbEntities.ScoredEmbedding>();
            }

            var queryVector = new Vector(embeddingResult.Embeddings[0]);

            // #2568: use the scored variant so the raw cosine similarity is preserved on each
            // hit. The cross-game merge (MultiGameHybridSearchService) needs a globally-
            // comparable signal to break rank-only RRF ties; the cosine is that signal.
            // Same SQL / ordering / minScore as SearchAsync — additive method (#1653).
            var results = await _vectorStore.SearchWithScoresAsync(
                gameId,
                queryVector,
                topK: limit,
                minScore: 0.3,
                documentIds: documentIds,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "pgvector search returned {Count} results for gameId={GameId}",
                results.Count, gameId);

            return results;
        }
#pragma warning disable CA1031 // Graceful degradation: vector search failure must not break hybrid search
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Vector search failed, falling back to keyword-only for gameId={GameId}",
                gameId);
            return new List<KbEntities.ScoredEmbedding>();
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Fuses vector and keyword search results using Reciprocal Rank Fusion (RRF).
    /// Thin adapter (#3270 Task 3): maps each arm's I/O-specific items into the neutral
    /// <see cref="FusionCandidate"/> shape, delegates the actual RRF + legend-demotion +
    /// role-boost scoring to <see cref="HybridFusionCore.Fuse"/> (the single canonical
    /// implementation shared with the primary chat path), then re-joins by composite key
    /// to rebuild <see cref="HybridSearchResult"/> with all its I/O-specific fields
    /// (MatchedTerms, GameId, PdfDocumentId, ChunkIndex, PageNumber).
    /// </summary>
    /// <remarks>
    /// RRF formula: score = sum_over_all_rankings(weight / (k + rank)), rank is 1-based position.
    /// RRF advantages:
    /// - No score normalization needed (works with heterogeneous scoring systems)
    /// - Emphasizes top-ranked results from both systems
    /// - Robust to score scale differences between vector (0-1) and keyword (unbounded) scores
    /// Reference: Cormack et al. "Reciprocal Rank Fusion outperforms Condorcet and individual rank learning methods" (SIGIR 2009)
    /// </remarks>
    private List<HybridSearchResult> FuseSearchResults(
        IReadOnlyList<SearchResultItem> vectorResults,
        IReadOnlyList<KeywordSearchResult> keywordResults,
        Guid gameId,
        float vectorWeight,
        float keywordWeight,
        int rrfK,
        GameBookRole queryRoleHint,
        IReadOnlyList<string>? queryTerms)
    {
        static string VectorKeyOf(SearchResultItem r) => $"{r.PdfId}_{r.ChunkIndex}";
        static string KeywordKeyOf(KeywordSearchResult r) => $"{r.PdfDocumentId}_{r.ChunkIndex}";

        var vectorArm = vectorResults
            .Select((r, index) => new FusionCandidate(VectorKeyOf(r), r.Text, r.RoleTags, r.Heading, index + 1, r.Score))
            .ToList();

        // RRF fusion-key fix: key the keyword arm on the SAME {PdfDocumentId}_{ChunkIndex} composite
        // as the vector arm (was the raw text_chunks.Id, which never matched the vector key — so a
        // doubly-retrieved chunk was emitted as two half-strength duplicates instead of being fused).
        var keywordArm = keywordResults
            .Select((r, index) => new FusionCandidate(KeywordKeyOf(r), r.Content, r.RoleTags, r.Heading, index + 1, r.RelevanceScore))
            .ToList();

        _logger.LogDebug(
            "Fusing results: {VectorCount} vector, {KeywordCount} keyword",
            vectorResults.Count, keywordResults.Count);

        var fused = HybridFusionCore.Fuse(
            vectorArm,
            keywordArm,
            new FusionOptions(vectorWeight, keywordWeight, rrfK, queryRoleHint, queryTerms));

        // Re-join by composite key to recover the I/O-specific fields the core doesn't carry.
        // The composite key is backed by a NON-unique index on the keyword side, so use
        // ToLookup + FirstOrDefault (keep the best-ranked/first row) instead of a Dictionary,
        // which would throw on an abnormal duplicate (PdfDocumentId, ChunkIndex).
        var vLookup = vectorResults.ToLookup(VectorKeyOf, StringComparer.Ordinal);
        var kLookup = keywordResults.ToLookup(KeywordKeyOf, StringComparer.Ordinal);

        var fusedResults = new List<HybridSearchResult>(fused.Count);

        foreach (var f in fused)
        {
            var v = vLookup[f.Key].FirstOrDefault();
            var k = kLookup[f.Key].FirstOrDefault();

            // Use data from whichever result has it (prefer vector for metadata consistency)
            var matchedTerms = k != null ? k.MatchedTerms : new List<string>();
            var pdfDocumentId = v != null ? v.PdfId : (k?.PdfDocumentId ?? string.Empty);
            var chunkGameId = k != null ? k.GameId : gameId; // keyword arm else fall back to query gameId
            var chunkIndex = v != null ? v.ChunkIndex : (k?.ChunkIndex ?? 0);
            var pageNumber = v != null ? v.Page : (k?.PageNumber ?? 0);

            fusedResults.Add(new HybridSearchResult
            {
                ChunkId = f.Key,
                Content = f.Content,
                PdfDocumentId = pdfDocumentId,
                GameId = chunkGameId,
                ChunkIndex = chunkIndex,
                PageNumber = pageNumber,
                HybridScore = f.HybridScore,
                VectorScore = f.VectorScore,
                KeywordScore = f.KeywordScore,
                VectorRank = f.VectorRank,
                KeywordRank = f.KeywordRank,
                MatchedTerms = matchedTerms,
                Mode = SearchMode.Hybrid,
                RoleTags = f.RoleTags,
                Heading = f.Heading,
                // #3740: only the vector arm knows the chunk language — null for keyword-only hits.
                Language = v?.Language
            });
        }

        return fusedResults;
    }
}

/// <summary>
/// Configuration for hybrid search.
/// Loaded from appsettings.json HybridSearch section.
/// </summary>
internal class HybridSearchConfiguration
{
    /// <summary>
    /// Weight for vector search results (default: 0.7).
    /// Higher weight emphasizes semantic similarity.
    /// </summary>
    public float VectorWeight { get; set; } = 0.7f;

    /// <summary>
    /// Weight for keyword search results (default: 0.3).
    /// Higher weight emphasizes exact term matching.
    /// </summary>
    public float KeywordWeight { get; set; } = 0.3f;

    /// <summary>
    /// RRF constant k (default: 60).
    /// Higher k reduces impact of rank differences.
    /// Standard value from research: 60 (Cormack et al. 2009).
    /// </summary>
    public int? RrfConstant { get; set; } = 60;

    /// <summary>
    /// Game-specific terms to boost in keyword search.
    /// Examples: "castling", "en passant", "check", "checkmate"
    /// </summary>
    public List<string> BoostTerms { get; set; } = new List<string>();
}