using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.ProbeRetrieval;

/// <summary>
/// #4482: il solo recupero che la chat farebbe per questa domanda su questo gioco, senza il modello.
/// Serve a misurare se il passaggio giusto arriva fra i candidati, e se il reranker lo tiene.
/// </summary>
/// <param name="GameId">Il gioco, come lo nomina la chat.</param>
/// <param name="Question">La domanda, com'è scritta.</param>
/// <param name="AcceptLanguage">
/// Il valore che la chat leggerebbe nell'header <c>Accept-Language</c> (per esempio <c>it-IT,it;q=0.9</c>).
/// Decide la lingua solo quando la domanda non basta a dirla. Assente vale «it».
/// </param>
internal sealed record ProbeRetrievalQuery(Guid GameId, string Question, string? AcceptLanguage = null)
    : IQuery<RetrievalProbeDto>;
