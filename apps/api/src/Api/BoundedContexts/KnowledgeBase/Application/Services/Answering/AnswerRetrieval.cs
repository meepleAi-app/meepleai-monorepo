using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;

#pragma warning disable MA0048 // File name must match type name - the step and its request
namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4168 (ADR-095 D3, passi 3 e 4): il recupero di una risposta, con i numeri della versione
/// pubblicata del profilo. Una sola implementazione per la chat in streaming e per la domanda singola.
/// </summary>
/// <remarks>
/// La ricerca ibrida applica il TopK a ciascun braccio e restituisce l'unione, che può arrivare al
/// doppio: il pool del profilo è il numero di candidati prima del reranking (D1), quindi si taglia qui,
/// nell'ordine della fusione. Poi il reranker porta il pool ai passaggi finali; senza reranker
/// restano i primi della fusione.
/// </remarks>
internal sealed class AnswerRetrieval
{
    private readonly SearchQueryHandler _searchQueryHandler;
    private readonly IMultiGameHybridSearchService _multiGameSearch;
    private readonly ICrossEncoderReranker _reranker;
    private readonly IIntentClassifierService _intentClassifier;
    private readonly ILogger<AnswerRetrieval> _logger;

    public AnswerRetrieval(
        SearchQueryHandler searchQueryHandler,
        IMultiGameHybridSearchService multiGameSearch,
        ICrossEncoderReranker reranker,
        IIntentClassifierService intentClassifier,
        ILogger<AnswerRetrieval> logger)
    {
        _searchQueryHandler = searchQueryHandler ?? throw new ArgumentNullException(nameof(searchQueryHandler));
        _multiGameSearch = multiGameSearch ?? throw new ArgumentNullException(nameof(multiGameSearch));
        _reranker = reranker ?? throw new ArgumentNullException(nameof(reranker));
        _intentClassifier = intentClassifier ?? throw new ArgumentNullException(nameof(intentClassifier));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <returns>I passaggi da mettere nel prompt, nell'ordine; una lista vuota se non c'è niente.</returns>
    public async Task<List<SearchResultDto>> RetrieveAsync(
        AnswerRetrievalRequest request,
        AgentProfileContent profile,
        string language,
        CancellationToken cancellationToken)
    {
        var retrieved = await RetrieveWithPoolAsync(request, profile, language, cancellationToken).ConfigureAwait(false);
        return retrieved.FinalResults();
    }

    /// <summary>
    /// #4482: lo stesso recupero di <see cref="RetrieveAsync"/>, mostrato per intero: il pool che va al
    /// reranker, nell'ordine della fusione, e i passaggi finali come posizioni in quel pool. La sonda di
    /// recupero lo restituisce così com'è; la risposta ne usa solo i finali.
    /// </summary>
    public async Task<RetrievedPassages> RetrieveWithPoolAsync(
        AnswerRetrievalRequest request,
        AgentProfileContent profile,
        string language,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);

        // Issue #3270: classify user intent into GameBookRole tag(s) so the hybrid re-ranker can boost
        // chunks whose role_tags overlap. The classifier is rule-based, sync, and cheap (~µs).
        var queryRoleHint = _intentClassifier.ClassifyIntent(request.Question);

        var search = await _searchQueryHandler.HandleWithOutcomeAsync(
            new SearchQuery(
                GameId: request.GameId,
                Query: request.Question,
                // Whoever uses this class passes the RAG gate first: AnswerPipeline always, and
                // AskQuestionQueryHandler whenever the question names a user. Its senders without a
                // user sit behind admin routes: the game wizard's auto-test and the retrieval probe (#4482).
                Access: SearchAccess.AlreadyChecked,
                TopK: profile.CandidatePoolSize,
                MinScore: (double)profile.MinScore,
                SearchMode: request.SearchMode,
                Language: language,
                PdfDocumentIds: request.PdfDocumentIds, // Issue #2051, #4243
                QueryVector: request.QueryVector, // Issue #563: reuse the vector computed for the semantic cache
                QueryRoleHint: queryRoleHint,
                // #4168 (5e-c-1): i pesi della fusione per gioco vengono dal profilo
                VectorWeight: (float)profile.VectorWeight,
                KeywordWeight: (float)profile.KeywordWeight),
            cancellationToken).ConfigureAwait(false);

        var searchResults = search.Results;
        var noReranking = profile.RerankerEnabled ? RerankStatus.Skipped : RerankStatus.Disabled;
        if (searchResults is null || searchResults.Count == 0)
        {
            _logger.LogInformation("No results for the question in game {GameId}", request.GameId);
            return new RetrievedPassages([], [], 0, queryRoleHint, noReranking, search.VectorArm);
        }

        var pool = searchResults.Take(profile.CandidatePoolSize).ToList();
        if (!profile.RerankerEnabled)
        {
            var firstOfPool = Enumerable.Range(0, Math.Min(profile.FinalTopK, pool.Count))
                .Select(i => new RerankPick(i, null))
                .ToList();
            return new RetrievedPassages(pool, firstOfPool, searchResults.Count, queryRoleHint, RerankStatus.Disabled, search.VectorArm);
        }

        // Issue #2708: rerank the pool down to the final top-K (graceful degradation to raw top-K).
        var reranked = await SearchResultReranker.RerankIndicesAsync(
            _reranker, request.Question, pool, profile.FinalTopK, _logger, cancellationToken).ConfigureAwait(false);
        return new RetrievedPassages(
            pool, reranked.Picks, searchResults.Count, queryRoleHint, reranked.Status, search.VectorArm, reranked.Model);
    }

    /// <summary>
    /// Il recupero fra i giochi accessibili (la ricerca cross-game). La fusione fra giochi resta
    /// quella di <see cref="IMultiGameHybridSearchService"/> (#3735): qui il profilo decide quanti
    /// candidati chiedere e se e fin dove passarli al reranker, che prima su questo percorso non c'era.
    /// </summary>
    /// <remarks>
    /// #4168 (5e-c-2): il passo per gioco usa lo stesso recupero della risposta per gioco, quindi la
    /// soglia del profilo vale sul suo braccio vettoriale (è una soglia di coseno) e i pesi del
    /// profilo sulla sua fusione. Il filtro <c>minScore</c> della ricerca multi-gioco è un'altra cosa
    /// — guarda il punteggio ibrido per gioco, su un'altra scala — e qui resta spento.
    /// </remarks>
    public async Task<List<MultiGameSearchResultItem>> RetrieveAcrossGamesAsync(
        string question,
        IReadOnlyList<Guid> gameIds,
        AgentProfileContent profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gameIds);
        ArgumentNullException.ThrowIfNull(profile);

        var pool = await _multiGameSearch.SearchAsync(
            question,
            gameIds,
            limit: profile.CandidatePoolSize,
            perGame: PerGameRetrievalParameters.From(profile),
            mode: SearchMode.Hybrid,
            minScore: 0.0,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (pool.Count == 0)
        {
            return [];
        }

        return profile.RerankerEnabled
            ? await SearchResultReranker.RerankAsync(
                _reranker, question, pool, profile.FinalTopK, _logger, cancellationToken).ConfigureAwait(false)
            : pool.Take(profile.FinalTopK).ToList();
    }
}

/// <summary>
/// #4482: un recupero per intero. <paramref name="Pool"/> sono i candidati dati al reranker, nell'ordine
/// della fusione; <paramref name="Final"/> i passaggi scelti, come indici nel pool e nell'ordine finale,
/// con il punteggio del cross-encoder quando c'è.
/// </summary>
/// <param name="Pool">I candidati prima del reranker: i primi <c>CandidatePoolSize</c> della fusione.</param>
/// <param name="Final">I passaggi finali, come indici in <paramref name="Pool"/>.</param>
/// <param name="FusedCount">Quanti candidati ha restituito la ricerca ibrida, prima del taglio al pool.</param>
/// <param name="QueryRoleHint">I ruoli che il classificatore ha letto nella domanda, passati alla fusione.</param>
/// <param name="Reranking">Come sono stati scelti i finali.</param>
/// <param name="VectorArm">Com'è andato il braccio vettoriale: fallito, la ricerca è stata solo lessicale.</param>
/// <param name="RerankerModel">Il modello che il servizio di reranking dice di aver usato, quando ha risposto.</param>
internal sealed record RetrievedPassages(
    IReadOnlyList<SearchResultDto> Pool,
    IReadOnlyList<RerankPick> Final,
    int FusedCount,
    GameBookRole QueryRoleHint,
    RerankStatus Reranking,
    VectorArmOutcome VectorArm,
    string? RerankerModel = null)
{
    /// <summary>I passaggi finali, nell'ordine: quelli che vanno nel prompt.</summary>
    public List<SearchResultDto> FinalResults() => Final.Select(p => Pool[p.Index]).ToList();
}

/// <summary>Cosa cercare: il gioco, la domanda, ed eventualmente i documenti e il vettore già calcolato.</summary>
internal sealed record AnswerRetrievalRequest(
    Guid GameId,
    string Question,
    IReadOnlyList<Guid>? PdfDocumentIds = null,
    string SearchMode = "hybrid",
    float[]? QueryVector = null);
