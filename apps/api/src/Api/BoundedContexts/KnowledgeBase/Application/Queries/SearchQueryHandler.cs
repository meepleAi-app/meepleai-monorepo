using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers;
using Api.Infrastructure.Entities;
using Api.Observability;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for SearchQuery.
/// Orchestrates vector/hybrid search using domain services.
/// </summary>
internal class SearchQueryHandler : IQueryHandler<SearchQuery, List<SearchResultDto>>
{
    private readonly IEmbeddingRepository _embeddingRepository;
    private readonly VectorSearchDomainService _vectorSearchService;
    private readonly RrfFusionDomainService _rrfFusionService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IKeywordSearchService _keywordSearchService;
    private readonly IRagAccessService _ragAccessService;
    private readonly ILogger<SearchQueryHandler> _logger;

    public SearchQueryHandler(
        IEmbeddingRepository embeddingRepository,
        VectorSearchDomainService vectorSearchService,
        RrfFusionDomainService rrfFusionService,
        IEmbeddingService embeddingService,
        IKeywordSearchService keywordSearchService,
        IRagAccessService ragAccessService,
        ILogger<SearchQueryHandler> logger)
    {
        _embeddingRepository = embeddingRepository ?? throw new ArgumentNullException(nameof(embeddingRepository));
        _vectorSearchService = vectorSearchService ?? throw new ArgumentNullException(nameof(vectorSearchService));
        _rrfFusionService = rrfFusionService ?? throw new ArgumentNullException(nameof(rrfFusionService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _keywordSearchService = keywordSearchService ?? throw new ArgumentNullException(nameof(keywordSearchService));
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<SearchResultDto>> Handle(
        SearchQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var access = query.Access
            ?? throw new ArgumentException("SearchQuery.Access is required", nameof(query));

        // RAG access enforcement. #4294: the sender states who is asking; only one that has
        // already passed the gate (SearchAccess.AlreadyChecked) is not checked again here.
        if (access.UserId is { } userId)
        {
            var userRole = Enum.TryParse<UserRole>(access.UserRole, ignoreCase: true, out var parsedRole)
                ? parsedRole : UserRole.User;
            await _ragAccessService.EnsureRagAccessAsync(
                userId, query.GameId, userRole, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "[SearchQueryHandler] ENTRY - Processing SearchQuery: GameId={GameId}, Query={Query}, Mode={SearchMode}, TopK={TopK}",
            query.GameId, query.Query, query.SearchMode, query.TopK);

        // Validate search parameters
        _vectorSearchService.ValidateSearchParameters(query.TopK, query.MinScore);

        // Step 1: Resolve query embedding.
        // Issue #563: When the caller supplies a pre-computed QueryVector (e.g., AskQuestionQueryHandler
        // already produced one for semantic cache lookup), reuse it to avoid a duplicate embedding call
        // (~50–200ms savings per cache miss). Otherwise fall back to generating one here.
        // #4168 (5e-c-2): la ricerca solo lessicale non ha un braccio vettoriale, quindi nemmeno un
        // embedding da calcolare. La chiede la ricerca fra giochi quando l'embedding della domanda è
        // fallito a monte: calcolarlo qui, per ogni gioco, sarebbe una chiamata per gioco.
        var searchMode = query.SearchMode.ToLowerInvariant();
        Vector queryVector = null!;
        if (string.Equals(searchMode, "keyword", StringComparison.Ordinal))
        {
            _logger.LogDebug("[SearchQueryHandler] Step 1: keyword-only search, no query embedding");
        }
        else if (query.QueryVector is { Count: > 0 } precomputed)
        {
            _logger.LogDebug(
                "[SearchQueryHandler] Step 1: Reusing caller-supplied query vector (Dim={Dim}) — skipping embedding call",
                precomputed.Count);
            // Materialize to float[] (Vector ctor requires array). ToArray() is a no-op when caller passes float[].
            queryVector = new Vector(precomputed as float[] ?? precomputed.ToArray());
        }
        else
        {
            _logger.LogDebug("[SearchQueryHandler] Step 1: Generating embedding via IEmbeddingService...");
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            // #3737: retrieval question -> e5 "query:" prefix.
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                query.Query,
                query.Language,
                EmbeddingPurpose.Query,
                cancellationToken).ConfigureAwait(false);
            sw1.Stop();
            _logger.LogInformation("[SearchQueryHandler] Step 1 DONE: Embedding generated in {ElapsedMs}ms - Success: {Success}",
                sw1.ElapsedMilliseconds, queryEmbedding.Success);

            queryVector = new Vector(queryEmbedding.ToFloatArray());
        }

        // #4243: an empty filter means no filter, for BOTH arms. The vector arm already read it that
        // way and the keyword arm did not (it kept nothing), so the two disagreed on an empty list.
        var pdfDocumentIds = query.PdfDocumentIds is { Count: > 0 } ? query.PdfDocumentIds : null;

        // Execute search based on mode
        _logger.LogDebug("[SearchQueryHandler] Step 2: Executing {SearchMode} search...", query.SearchMode);
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var searchResults = searchMode switch
        {
            "keyword" => await SearchKeywordArmAsync(
                query.Query, query.GameId, query.TopK, pdfDocumentIds, cancellationToken).ConfigureAwait(false),

            "vector" => await PerformVectorSearchAsync(
                query.GameId, queryVector, query.TopK, query.MinScore, pdfDocumentIds, cancellationToken).ConfigureAwait(false),

            // Phase D (D6): forward QueryRoleHint to the hybrid search re-ranker.
            "hybrid" => await PerformHybridSearchAsync(
                query.GameId, queryVector, query.Query, query.TopK, query.MinScore, pdfDocumentIds, query.QueryRoleHint,
                query.VectorWeight, query.KeywordWeight, cancellationToken).ConfigureAwait(false),

            _ => throw new ArgumentException($"Invalid search mode: {query.SearchMode}", nameof(query))
        } ?? new List<Domain.Entities.SearchResult>();
        sw2.Stop();
        _logger.LogInformation("[SearchQueryHandler] Step 2 DONE: {SearchMode} search completed in {ElapsedMs}ms - {ResultCount} results",
            query.SearchMode, sw2.ElapsedMilliseconds, searchResults.Count);

        // Map to DTOs. #4168 (5e-c-2): con l'identità del chunk e i segnali della fusione.
        var dtos = searchResults.Select(SearchResultDto.FromDomain).ToList();

        _logger.LogInformation(
            "[SearchQueryHandler] COMPLETE - SearchQuery completed: Found {ResultCount} results",
            dtos.Count);

        return dtos;
    }

    private async Task<List<Domain.Entities.SearchResult>> PerformVectorSearchAsync(
        Guid gameId,
        Vector queryVector,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? documentIds,
        CancellationToken cancellationToken)
    {
        // Issue #2712: use SearchByVectorWithScoresAsync so each result carries the REAL cosine
        // similarity computed by pgvector in SQL (1 - (vector <=> query)). The previous call
        // (SearchByVectorAsync) returned placeholder vectors and forced a rank-based score
        // (1.0 - index*0.05), which made the downstream confidence degenerate (always ≈0.53) and
        // hid grounded answers behind the "Non sono certo" card.
        IReadOnlyList<Domain.Entities.ScoredEmbedding> scoredEmbeddings;
        try
        {
            scoredEmbeddings = await _embeddingRepository.SearchByVectorWithScoresAsync(
                gameId, queryVector, topK, minScore, documentIds, cancellationToken).ConfigureAwait(false)
                ?? (IReadOnlyList<Domain.Entities.ScoredEmbedding>)Array.Empty<Domain.Entities.ScoredEmbedding>();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // #3786: la ricerca vettoriale NON è stata eseguita. Senza questo segnale un guasto è
            // indistinguibile da «nessun risultato». Chi chiama decide se ripiegare (l'ibrido) o no.
            MeepleAiMetrics.RecordVectorArmOutcome(MeepleAiMetrics.RagArmOutcome.Failed);
            throw;
        }

        // #3786: `empty` è un esito legittimo (gioco senza contenuto indicizzato, o nessun chunk
        // sopra la soglia), tenuto distinto da `failed`.
        MeepleAiMetrics.RecordVectorArmOutcome(scoredEmbeddings.Count > 0
            ? MeepleAiMetrics.RagArmOutcome.Hit
            : MeepleAiMetrics.RagArmOutcome.Empty);

        _logger.LogInformation(
            "Vector search returned {Count} scored embeddings for gameId={GameId}",
            scoredEmbeddings.Count, gameId);

        // Results come pre-ordered by pgvector (ORDER BY distance), so the list index is the rank.
        var results = scoredEmbeddings.Select((scored, index) =>
        {
            var cosine = Math.Clamp(scored.Score, 0.0, 1.0);

            return new Domain.Entities.SearchResult(
                id: Guid.NewGuid(),
                vectorDocumentId: scored.Embedding.VectorDocumentId,
                textContent: scored.Embedding.TextContent,
                pageNumber: scored.Embedding.PageNumber,
                relevanceScore: new Confidence(cosine),
                rank: index + 1,
                searchMethod: "vector",
                pdfDocumentId: scored.Embedding.PdfDocumentId,                       // real (JOIN-resolved)
                chunkIndex: scored.Embedding.ChunkIndex,
                textChunkId: scored.Embedding.SourceChunkId,                         // #4288
                roleTags: (GameBookRole)scored.Embedding.RoleTags,                   // int → enum
                heading: scored.Embedding.Heading,                                    // #3270
                boundingBoxesJson: scored.Embedding.BoundingBoxesJson,               // SP-C (#3407)
                charStart: scored.Embedding.CharStart,
                charEnd: scored.Embedding.CharEnd,
                // #4168 (5e-c-2): il coseno grezzo, il rango e la lingua, per chi fonde fra giochi.
                signals: new RetrievalSignals(
                    HybridScore: (float)cosine,
                    VectorScore: (float)scored.Score,
                    VectorRank: index + 1,
                    Language: scored.Embedding.Language)
            );
        }).ToList();

        _logger.LogInformation(
            "Converted {Count} embeddings to SearchResults with rank-based scoring",
            results.Count);

        return results;
    }

    private async Task<List<Domain.Entities.SearchResult>> PerformHybridSearchAsync(
        Guid gameId,
        Vector queryVector,
        string query,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? documentIds,
        GameBookRole queryRoleHint,
        float vectorWeight,
        float keywordWeight,
        CancellationToken cancellationToken)
    {
        // Vector search. #4168 (5e-c-2): un guasto del braccio vettoriale degrada al solo lessicale
        // invece di far cadere la ricerca, come faceva HybridSearchService per la ricerca fra giochi
        // che ora passa da qui: altrimenti il gioco uscirebbe dal pool, lessicale compreso. L'esito
        // `failed` l'ha già contato PerformVectorSearchAsync (#3786).
        List<Domain.Entities.SearchResult> vectorResults;
        try
        {
            vectorResults = await PerformVectorSearchAsync(
                gameId, queryVector, topK, minScore, documentIds, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Degradazione: un guasto del braccio vettoriale non deve far cadere la ricerca
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex,
                "[SearchQueryHandler] Vector search failed, falling back to keyword-only for gameId={GameId}",
                gameId);
            vectorResults = new List<Domain.Entities.SearchResult>();
        }
#pragma warning restore CA1031

        // #3338 WP1c: resolve the per-game FTS config for the heading-term synonym expansion below.
        // NOTE: SearchAsync resolves the same config internally, so this is one extra (fast, GameId-
        // indexed) language-detection query per hybrid search — an accepted minor cost; threading it
        // into SearchAsync would churn ~15 keyword-mock setups for a non-blocking perf finding.
        var ftsConfig = await _keywordSearchService
            .ResolveFtsConfigAsync(gameId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // #3768: stessa nota di sopra — una seconda query indicizzata per GameId. Il titolo serve a
        // ESCLUDERE i suoi token dai termini di heading-match, non a cercarli.
        var gameTitle = await _keywordSearchService
            .ResolveGameTitleAsync(gameId, cancellationToken)
            .ConfigureAwait(false);

        var keywordResults = await SearchKeywordArmAsync(query, gameId, topK, documentIds, cancellationToken)
            .ConfigureAwait(false);

        // Use RRF fusion domain service. #3270: derive heading-match query terms from the raw query
        // (the same string used for the keyword arm) and forward them so the heading boost can fire.
        // #3338 WP1c: expand those terms with the game's FTS-language intent synonyms (setup ->
        // preparazione/allestimento) so an English-loanword query boosts a native-lexeme heading.
        // #3768: stessa esclusione del braccio lessicale — questo percorso e' filtrato per gameId,
        // quindi il nome del gioco ha IDF nullo e premiarlo porta in cima colophon e copertine.
        var headingTerms = KeywordSearchService.ExpandHeadingMatchTerms(
            FusionSignals.ExtractHeadingMatchTerms(query), ftsConfig, gameTitle);

        var fused = _rrfFusionService.FuseResults(
            vectorResults,
            keywordResults,
            queryRoleHint: queryRoleHint,
            queryTerms: headingTerms,
            vectorWeight: vectorWeight,
            keywordWeight: keywordWeight);

        LogPerGameArenaForTuning(query, gameId, ftsConfig, headingTerms, queryRoleHint, fused);

        return fused;
    }

    /// <summary>
    /// Il braccio lessicale: i passaggi del gioco per <c>ts_rank_cd</c>, col filtro per documento.
    /// Lo usano la ricerca ibrida e quella solo lessicale (#4168, 5e-c-2: la ricerca fra giochi la
    /// chiede quando l'embedding della domanda non c'è, o quando la ricerca globale la sceglie).
    /// </summary>
    private async Task<List<Domain.Entities.SearchResult>> SearchKeywordArmAsync(
        string query,
        Guid gameId,
        int topK,
        IReadOnlyList<Guid>? documentIds,
        CancellationToken cancellationToken)
    {
        // Issue #423: ts_rank_cd scores ~0-0.3; 0.01 filters ToC noise.
        const double KeywordMinScore = 0.01;

        // Spec §6: RAW keyword arm sourced directly from IKeywordSearchService (un-boosted, raw ts_rank_cd
        // order) so HybridFusionCore applies role-boost + legend exactly once.
        var rawKeyword = await _keywordSearchService.SearchAsync(
            query,
            gameId,
            topK,
            phraseSearch: query.Contains('"'),            // reproduce HybridSearchService.cs:189 derivation
            boostTerms: null,                             // was _config.BoostTerms — a no-op on the tsquery
            minScore: KeywordMinScore,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Issue #2051: reproduce the documentIds post-filter that SearchAsync(Keyword) applied internally.
        var filteredKeyword = documentIds is null
            ? (IReadOnlyList<KeywordSearchResult>)rawKeyword
            : rawKeyword
                .Where(r => documentIds.Any(id => string.Equals(id.ToString(), r.PdfDocumentId, StringComparison.Ordinal)))
                .ToList();

        return filteredKeyword
            .Select((kr, index) => kr.ToDomainSearchResult(index + 1))
            .ToList();
    }

    private static readonly JsonSerializerOptions PerGameTuningJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// La scomposizione del punteggio di ogni candidato fuso di questo gioco, a livello Debug, con lo
    /// stesso prefisso e lo stesso formato di <c>HybridSearchService</c>
    /// (<see cref="Api.Services.HybridSearchService.PerGameTuningLogPrefix"/>).
    /// </summary>
    /// <remarks>
    /// #4168 (5e-c-2): il passo per gioco della ricerca fra giochi passa ora da qui, e questa riga è
    /// quella che mostra i candidati che il gioco ha RIFIUTATO prima della fusione globale — il dump
    /// <c>[RAG-TUNE]</c> mostra solo quelli che arrivano. I fattori sono ricalcolati con le stesse
    /// funzioni pure della fusione; <c>s</c> è il punteggio reale letto dal risultato fuso.
    /// </remarks>
    private void LogPerGameArenaForTuning(
        string query,
        Guid gameId,
        string? ftsConfig,
        IReadOnlyList<string>? headingTerms,
        GameBookRole queryRoleHint,
        IReadOnlyList<Domain.Entities.SearchResult> fused)
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
                d = r.PdfDocumentId.ToString(),
                i = r.ChunkIndex,
                vr = r.Signals.VectorRank,
                kr = r.Signals.KeywordRank,
                v = r.Signals.VectorScore,
                k = r.Signals.KeywordScore,
                lg = FusionSignals.ComputeLegendPenaltyFactor(r.TextContent),
                nn = FusionSignals.ComputeNumberNoiseFactor(r.TextContent),
                rb = FusionSignals.ComputeRoleMatchBoost(queryRoleHint, r.RoleTags),
                hb = FusionSignals.ComputeHeadingMatchBoost(headingTerms, r.Heading),
                h = r.Heading,
                l = r.Signals.Language,
                s = r.Signals.HybridScore
            }).ToList()
        };

        _logger.LogDebug(
            "{Prefix} {Payload}",
            Api.Services.HybridSearchService.PerGameTuningLogPrefix,
            JsonSerializer.Serialize(payload, PerGameTuningJsonOptions));
    }
}
