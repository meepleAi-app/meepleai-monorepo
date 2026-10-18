namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// I segnali grezzi di un passaggio recuperato, come li vede la fusione per gioco (#4168, 5e-c-2).
/// </summary>
/// <remarks>
/// Servono a chi fonde di nuovo sopra la fusione per gioco: la ricerca fra giochi
/// (<c>MultiGameHybridSearchService.FuseGlobally</c>) confronta coseni e punteggi lessicali GREZZI
/// fra giochi diversi, e il suo dump di taratura <c>[RAG-TUNE]</c> li riporta. Il punteggio
/// lessicale è il <c>ts_rank_cd</c> com'è, che supera 1: <see cref="Entities.SearchResult.RelevanceScore"/>
/// lo schiaccerebbe in [0,1] e cambierebbe la normalizzazione fra giochi.
/// </remarks>
internal sealed record RetrievalSignals(
    float? HybridScore = null,
    float? VectorScore = null,
    float? KeywordScore = null,
    int? VectorRank = null,
    int? KeywordRank = null,
    string? Language = null,
    IReadOnlyList<string>? MatchedTerms = null)
{
    /// <summary>Nessun segnale: un risultato costruito fuori dal recupero.</summary>
    public static readonly RetrievalSignals None = new();
}
