using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Parallel orchestration wrapper for cross-game hybrid search.
///
/// Strategy (R3 verdict b):
/// 1. Early-exit when gameIds is empty (EC-1).
/// 2. Launch one <see cref="IHybridSearchService.SearchAsync"/> task per game. Each task runs in its
///    OWN DI scope so it gets its OWN <c>MeepleAiDbContext</c> — the per-game services are scoped and
///    a single request-scoped DbContext is NOT safe for concurrent use ("A second operation was
///    started on this context instance…"), which previously made every cross-game search throw and
///    return 0 results (#2480). A <see cref="SemaphoreSlim"/> caps concurrency so a user with many
///    accessible games (e.g. an admin) cannot exhaust the connection pool.
/// 3. Per-game exceptions are caught and logged as warnings; the query continues with the remaining
///    games (EC-2 / EC-7 resilience: a game with no indexed content should not abort the whole search).
/// 4. Per-game already-fused results are tagged with their origin gameId and aggregated.
/// 5. Apply minScore filter (sul punteggio per-gioco: è una soglia di rilevanza locale).
/// 6. <b>Rifondere con RRF GLOBALE</b> su ranking costruiti sull'insieme aggregato (#3735).
/// 7. Sort by HybridScore DESC + tiebreak deterministici (EC-4 stable ordering).
/// 8. Take the requested limit (hard cap, EC-7).
///
/// <para>
/// <b>Perché una seconda fusione</b> (#3735). Questa docstring diceva il contrario — «No second RRF
/// pass: scores are already RRF-fused within each game and share the same scale» — ed era la
/// premessa sbagliata da cui nasceva il difetto. <i>Stessa scala</i> non significa <i>confrontabili</i>:
/// il punteggio per-gioco è una somma di termini <c>1/(k+rank)</c> dove il rango è <b>locale</b>, quindi
/// il rank-1 di ogni gioco riceve lo stesso valore e un chunk presente in entrambe le liste locali batte
/// un chunk molto più pertinente presente in una sola.
/// </para>
/// <para>
/// Misurato su 11 query canoniche: 9 recuperavano il manuale di un gioco diverso da quello nominato
/// nella query. La fusione avviene ora <b>dopo</b> l'aggregazione, su segnali confrontabili fra giochi
/// (cosine grezza e ts_rank_cd) — vedi <c>FuseGlobally</c>.
/// </para>
/// </summary>
internal sealed class MultiGameHybridSearchService : IMultiGameHybridSearchService
{
    // Cap concurrent per-game searches. Each acquires its own DI scope (own DbContext +
    // DB connection), so the bound also bounds the connection-pool draw when a user has
    // many accessible games.
    private const int MaxConcurrentGameSearches = 4;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MultiGameHybridSearchService> _logger;

    public MultiGameHybridSearchService(
        IServiceScopeFactory scopeFactory,
        ILogger<MultiGameHybridSearchService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MultiGameSearchResultItem>> SearchAsync(
        string query,
        IReadOnlyList<Guid> gameIds,
        int limit,
        SearchMode mode = SearchMode.Hybrid,
        double minScore = 0.0,
        IReadOnlyList<Guid>? documentIds = null,
        CancellationToken cancellationToken = default)
    {
        // EC-1: no accessible games → return empty immediately
        if (gameIds.Count == 0)
            return Array.Empty<MultiGameSearchResultItem>();

        _logger.LogInformation(
            "MultiGameHybridSearch: query='{Query}', gameCount={GameCount}, limit={Limit}, mode={Mode}, minScore={MinScore}, documentIdsCount={DocCount}",
            query, gameIds.Count, limit, mode, minScore, documentIds?.Count);

        // Convert IReadOnlyList → List once (interface requires List<Guid>? on the per-game service).
        // Materialise outside the per-game loop so all parallel calls share the same instance.
        var documentIdsList = documentIds is null ? null : new List<Guid>(documentIds);

        // Step 1: Launch parallel per-game searches.
        // We request 'limit' results per game so each game contributes enough candidates
        // before the cross-game sort + truncation.  A cap of min(limit, 50) is applied to
        // prevent per-game over-fetching when limit is very large.
        var perGameLimit = Math.Min(Math.Max(limit, 1), 50);

        using var throttle = new SemaphoreSlim(MaxConcurrentGameSearches);
        var tasks = gameIds
            .Select(gameId => SearchGameSafeAsync(query, gameId, perGameLimit, mode, documentIdsList, throttle, cancellationToken))
            .ToList();

        // Task.WhenAll guarantees parallel execution (all tasks start before any is awaited);
        // the semaphore inside each task bounds how many run the DB work concurrently.
        var perGameResultArrays = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Step 2: Aggregate, project to MultiGameSearchResultItem (preserving origin gameId).
        // perGameResultArrays is (GameId, List<HybridSearchResult>)[] — we flatten while keeping gameId.
        var aggregated = perGameResultArrays
            .SelectMany(tuple => tuple.Results, (tuple, r) => ProjectItem(tuple.GameId, r))
            .ToList();

        _logger.LogInformation(
            "MultiGameHybridSearch aggregated {TotalRaw} results before minScore={MinScore} filter",
            aggregated.Count, minScore);

        // Step 3: Apply minScore filter.
        //
        // Filtra sul punteggio PER-GIOCO, non su quello globale calcolato allo step 4: minScore è
        // una soglia di rilevanza locale ("questo chunk vale qualcosa dentro il suo gioco?") e
        // applicarla al punteggio globale cambierebbe di significato — con 133 giochi il termine
        // RRF globale è per costruzione piccolo, e una soglia tarata sul per-gioco svuoterebbe il
        // risultato.
        if (minScore > 0.0)
            aggregated = aggregated.Where(r => r.HybridScore >= (float)minScore).ToList();

        // Step 4: RRF GLOBALE (#3735).
        //
        // Il punteggio arrivato dai per-game search è calcolato DENTRO ogni gioco: è una somma di
        // termini 1/(k+rank) dove `rank` è la posizione locale. Confrontarlo fra giochi non misura
        // la pertinenza ma il rango locale, e produce due effetti entrambi osservati:
        //
        //   a) il rank-1 di OGNI gioco riceve lo stesso valore → il ranking finiva per essere
        //      deciso dal tiebreak (#2568 lo aveva reso almeno query-dipendente, ma resta un
        //      ripiego che si attiva solo sui pareggi esatti);
        //   b) un chunk presente in ENTRAMBE le liste per-gioco (vettoriale e lessicale) somma due
        //      termini e batte un chunk molto più pertinente presente in una sola — anche con una
        //      cosine nettamente superiore. È il caso misurato in #3735: un manuale che contiene le
        //      parole generiche della query («setup», «board», «place», presenti in ogni
        //      regolamento) vinceva contro il manuale che la query nomina esplicitamente.
        //
        // La fusione va quindi rifatta DOPO l'aggregazione, su ranking GLOBALI costruiti da segnali
        // confrontabili fra giochi: la cosine grezza (VectorScore) e la rilevanza lessicale
        // (KeywordScore, ts_rank_cd). Il risultato sostituisce HybridScore, che sul percorso
        // cross-gioco è quindi il punteggio globale — vedi il contratto in
        // IMultiGameHybridSearchService.
        LogAggregateForTuning(query, aggregated);

        aggregated = FuseGlobally(aggregated);

        // Step 5: Hard-cap at limit (EC-7).
        if (aggregated.Count > limit)
            aggregated = aggregated.Take(limit).ToList();

        _logger.LogInformation(
            "MultiGameHybridSearch completed: returning {ResultCount} results",
            aggregated.Count);

        return aggregated;
    }

    /// <summary>
    /// Issues a single-game search in its OWN DI scope (own DbContext), catching and logging any
    /// exception so that one failing game does not abort the entire cross-game query (EC-2 resilience).
    /// The <paramref name="throttle"/> bounds how many per-game searches hit the DB concurrently.
    /// The <paramref name="documentIds"/> allowlist (Issue #1686) restricts the per-game hit set to a
    /// subset of PDF documents; <c>null</c> = no document filter.
    /// </summary>
    private async Task<(Guid GameId, List<HybridSearchResult> Results)> SearchGameSafeAsync(
        string query,
        Guid gameId,
        int limit,
        SearchMode mode,
        List<Guid>? documentIds,
        SemaphoreSlim throttle,
        CancellationToken cancellationToken)
    {
        await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Own scope → own scoped MeepleAiDbContext, so concurrent per-game searches never
            // share a DbContext instance (the root cause of the cross-game "second operation on
            // this context" failures that returned 0 results, #2480).
            using var scope = _scopeFactory.CreateScope();
            var hybridSearch = scope.ServiceProvider.GetRequiredService<IHybridSearchService>();

            var results = await hybridSearch.SearchAsync(
                query,
                gameId,
                mode,
                limit,
                documentIds: documentIds,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return (gameId, results);
        }
#pragma warning disable CA1031 // Resilience: per-game exception must not abort cross-game query
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "MultiGameHybridSearch: search failed for gameId={GameId}, skipping game. Query='{Query}'",
                gameId, query);
            return (gameId, new List<HybridSearchResult>());
        }
#pragma warning restore CA1031
        finally
        {
            throttle.Release();
        }
    }

    /// <summary>
    /// Prefisso stabile della riga di diagnostica. Il consumatore (il gate RAG smoke) filtra su
    /// questo: cambiarlo rompe lo script di estrazione.
    /// </summary>
    internal const string TuningLogPrefix = "[RAG-TUNE]";

    private static readonly JsonSerializerOptions TuningJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Emette l'aggregato PRIMA della fusione, per poter tarare <see cref="FuseGlobally"/> offline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Perché esiste.</b> Fra il 2026-08-17 e il 2026-08-18 tre configurazioni della fusione sono
    /// state provate contro il gate RAG smoke e hanno dato 10/11 → 8/11 → 5/11. Ogni ipotesi è
    /// costata ~45 minuti di CI e nessuna era verificabile prima: dall'esterno si vede solo il
    /// top-3 finale, mai i segnali su cui la fusione decide. Con questa riga l'aggregato diventa
    /// osservabile e la taratura si misura offline in secondi.
    /// </para>
    /// <para>
    /// <b>Perché dal codice e non ricostruito in SQL.</b> Riprodurre i due bracci con query proprie
    /// significherebbe tarare su un'approssimazione — ed è esattamente l'errore che ha prodotto
    /// quelle tre iterazioni: una misura fatta su un corpus e una pipeline diversi da quelli veri.
    /// Qui il dato è quello che la fusione riceve davvero, non una sua imitazione.
    /// </para>
    /// <para>
    /// <b>Costo a runtime: nullo quando disattivata.</b> La guardia <c>IsEnabled(Debug)</c> precede
    /// qualunque allocazione, quindi in produzione — dove la categoria sta a Information — non si
    /// serializza nulla. Il gate alza il livello solo per questa categoria.
    /// </para>
    /// <para>
    /// Il documento è identificato dal suo id: la risoluzione a nome file la fa già il consumatore
    /// (<c>rag-smoke-assert.sh</c> costruisce la mappa id → fileName), e duplicarla qui
    /// significherebbe una query in più sul percorso caldo.
    /// </para>
    /// </remarks>
    private void LogAggregateForTuning(string query, List<MultiGameSearchResultItem> aggregated)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
            return;

        var payload = new
        {
            q = query,
            n = aggregated.Count,
            c = aggregated.Select(r => new
            {
                d = r.PdfDocumentId,
                i = r.ChunkIndex,
                g = r.GameId,
                v = r.VectorScore,
                k = r.KeywordScore,
                // #3740: la lingua del chunk. Omessa quando è null (candidato solo-lessicale), per
                // WhenWritingNull — il consumatore distingue «lingua ignota» da «lingua nota».
                l = r.Language
            }).ToList()
        };

        _logger.LogDebug(
            "{Prefix} {Payload}", TuningLogPrefix, JsonSerializer.Serialize(payload, TuningJsonOptions));
    }

    /// <summary>
    /// Pesi della fusione globale (#3735). Il vettoriale pesa più del lessicale perché è il segnale
    /// che distingue i manuali fra loro: le parole di una query di regolamento («setup», «board»,
    /// «turn») compaiono in ogni manuale, quindi il lessicale da solo non discrimina il gioco — è
    /// il modo esatto in cui il difetto si manifestava. Stessi pesi del percorso per-gioco
    /// (<c>GenerateToolkitFromKbHandler</c> usa 0.7/0.3) per non introdurre una seconda taratura.
    /// </summary>
    private const float GlobalVectorWeight = 0.7f;
    private const float GlobalKeywordWeight = 0.3f;

    /// <summary>
    /// Ricalcola il punteggio su base GLOBALE, unica per tutti i giochi (#3735).
    ///
    /// <para>
    /// <b>Perché una somma pesata e non un secondo RRF.</b> L'RRF fonde <i>ranghi</i>, ed è la
    /// scelta giusta quando i segnali vivono su scale non confrontabili. Qui non è il caso: la
    /// cosine è già normalizzata in [0,1] e confrontabile fra giochi per costruzione. Fondere i
    /// ranghi butterebbe via proprio la magnitudine, cioè l'informazione che distingue il manuale
    /// pertinente — misurato: fra due posizioni contigue il divario RRF vale
    /// <c>0.7/61 − 0.7/62 ≈ 0.000185</c>, mentre un match lessicale ne aggiunge
    /// <c>0.3/61 ≈ 0.0049</c>, trenta volte tanto. Un chunk con cosine 0.93 perderebbe contro uno
    /// a 0.71 che capita di contenere le parole della query.
    /// </para>
    /// <para>
    /// Il lessicale (<c>ts_rank_cd</c>) non ha invece una scala assoluta, quindi viene normalizzato
    /// sul massimo dell'insieme aggregato prima di entrare nella somma. Un chunk privo di un
    /// segnale contribuisce 0 da quel lato, senza essere penalizzato due volte.
    /// </para>
    /// </summary>
    private static List<MultiGameSearchResultItem> FuseGlobally(List<MultiGameSearchResultItem> aggregated)
    {
        if (aggregated.Count <= 1)
            return aggregated;

        // Min-max su ENTRAMBI i segnali, sull'insieme aggregato.
        //
        // Normalizzare solo il lessicale (il segnale che non ha scala assoluta) sembra sufficiente
        // ma non lo è, ed è un errore misurato: le cosine di `multilingual-e5-base` vivono in una
        // banda compressa — su una query reale i primi quattro candidati stavano fra 0.81 e 0.86 —
        // quindi una differenza semanticamente enorme vale pochi centesimi in valore assoluto,
        // mentre un lessicale normalizzato sul massimo spazia sull'intero [0,1]. Con pesi 0.7/0.3
        // il lessicale finirebbe per dominare qualunque divario di cosine realistico: il chunk
        // «giusto» perde contro uno che contiene le parole generiche della query.
        //
        // Portando entrambi i segnali su [0,1] rispetto ai candidati effettivi, i pesi tornano a
        // significare ciò che dichiarano.
        var (vectorMin, vectorMax) = Extent(aggregated, r => r.VectorScore);
        var (keywordMin, keywordMax) = Extent(aggregated, r => r.KeywordScore);

        var rescored = aggregated
            .Select(r => r with
            {
                HybridScore =
                    (GlobalVectorWeight * Normalise(r.VectorScore, vectorMin, vectorMax)) +
                    (GlobalKeywordWeight * Normalise(r.KeywordScore, keywordMin, keywordMax))
            })
            .ToList();

        // Ordinamento finale. Il criterio primario è ora davvero discriminante, ma i tiebreak
        // restano: il contratto EC-4 richiede un cursore stabile anche a parità di punteggio.
        rescored.Sort(static (a, b) =>
        {
            var scoreCmp = b.HybridScore.CompareTo(a.HybridScore); // DESC
            if (scoreCmp != 0) return scoreCmp;

            // null cosine (keyword-only hit) sorts below any real cosine match.
            var vecCmp = (b.VectorScore ?? float.MinValue).CompareTo(a.VectorScore ?? float.MinValue); // DESC
            if (vecCmp != 0) return vecCmp;

            var kwCmp = (b.KeywordScore ?? float.MinValue).CompareTo(a.KeywordScore ?? float.MinValue); // DESC
            if (kwCmp != 0) return kwCmp;

            var chunkCmp = a.ChunkIndex.CompareTo(b.ChunkIndex);   // ASC (deterministic fallback)
            if (chunkCmp != 0) return chunkCmp;

            return string.Compare(a.PdfDocumentId, b.PdfDocumentId, StringComparison.Ordinal); // ASC
        });

        return rescored;
    }

    /// <summary>
    /// Estremi di un segnale sui candidati che lo possiedono. <c>(0,0)</c> quando nessuno ce l'ha —
    /// il segnale è assente e <see cref="Normalise"/> lo neutralizza.
    /// </summary>
    private static (float Min, float Max) Extent(
        List<MultiGameSearchResultItem> items, Func<MultiGameSearchResultItem, float?> selector)
    {
        var present = items.Select(selector).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return present.Count == 0 ? (0f, 0f) : (present.Min(), present.Max());
    }

    /// <summary>
    /// Porta un segnale in [0,1] rispetto agli estremi osservati. Un segnale assente vale 0 (non
    /// contribuisce, non penalizza due volte); un intervallo degenere — tutti i candidati con lo
    /// stesso valore — vale 1, perché quel segnale non discrimina e non deve nemmeno azzerare il
    /// contributo di chi lo possiede.
    /// </summary>
    private static float Normalise(float? value, float min, float max)
    {
        if (value is null) return 0f;
        var range = max - min;
        return range <= float.Epsilon ? 1f : (value.Value - min) / range;
    }

    /// <summary>
    /// Projects a per-game <see cref="HybridSearchResult"/> to a <see cref="MultiGameSearchResultItem"/>,
    /// using the explicitly passed <paramref name="gameId"/> (the origin game from the parallel task)
    /// rather than <c>r.GameId</c> which may be the query-default rather than the actual game.
    /// </summary>
    private static MultiGameSearchResultItem ProjectItem(Guid gameId, HybridSearchResult r) =>
        new()
        {
            GameId = gameId,
            ChunkId = r.ChunkId,
            PdfDocumentId = r.PdfDocumentId,
            ChunkIndex = r.ChunkIndex,
            PageNumber = r.PageNumber,
            Content = r.Content,
            HybridScore = r.HybridScore,
            VectorScore = r.VectorScore,
            KeywordScore = r.KeywordScore,
            MatchedTerms = r.MatchedTerms,
            Mode = r.Mode,
            Language = r.Language
        };
}
