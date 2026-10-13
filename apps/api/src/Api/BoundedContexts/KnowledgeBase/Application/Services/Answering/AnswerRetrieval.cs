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
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);

        // Issue #3270: classify user intent into GameBookRole tag(s) so the hybrid re-ranker can boost
        // chunks whose role_tags overlap. The classifier is rule-based, sync, and cheap (~µs).
        var queryRoleHint = _intentClassifier.ClassifyIntent(request.Question);

        var searchResults = await _searchQueryHandler.Handle(
            new SearchQuery(
                GameId: request.GameId,
                Query: request.Question,
                TopK: profile.CandidatePoolSize,
                MinScore: (double)profile.MinScore,
                SearchMode: request.SearchMode,
                Language: language,
                PdfDocumentIds: request.PdfDocumentIds, // Issue #2051, #4243
                QueryVector: request.QueryVector, // Issue #563: reuse the vector computed for the semantic cache
                QueryRoleHint: queryRoleHint),
            cancellationToken).ConfigureAwait(false);

        if (searchResults is null || searchResults.Count == 0)
        {
            _logger.LogInformation("No results for the question in game {GameId}", request.GameId);
            return [];
        }

        var pool = searchResults.Take(profile.CandidatePoolSize).ToList();
        return profile.RerankerEnabled
            // Issue #2708: rerank the pool down to the final top-K (graceful degradation to raw top-K).
            ? await SearchResultReranker.RerankAsync(
                _reranker, request.Question, pool, profile.FinalTopK, _logger, cancellationToken).ConfigureAwait(false)
            : pool.Take(profile.FinalTopK).ToList();
    }

    /// <summary>
    /// Il recupero fra i giochi accessibili (la ricerca cross-game). La fusione fra giochi resta
    /// quella di <see cref="IMultiGameHybridSearchService"/> (#3735): qui il profilo decide quanti
    /// candidati chiedere e se e fin dove passarli al reranker, che prima su questo percorso non c'era.
    /// </summary>
    /// <remarks>
    /// La soglia del profilo non si applica: è una soglia di coseno, mentre la ricerca multi-gioco
    /// filtra sul punteggio ibrido per gioco, che ha un'altra scala.
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

/// <summary>Cosa cercare: il gioco, la domanda, ed eventualmente i documenti e il vettore già calcolato.</summary>
internal sealed record AnswerRetrievalRequest(
    Guid GameId,
    string Question,
    IReadOnlyList<Guid>? PdfDocumentIds = null,
    string SearchMode = "hybrid",
    float[]? QueryVector = null);
