#pragma warning disable MA0048 // File name must match type name - the probe's answer and its parts
namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.ProbeRetrieval;

/// <summary>
/// #4482: cosa ha visto il recupero della chat per una domanda. Nessun modello è stato chiamato.
/// </summary>
/// <param name="GameId">Il gioco su cui si è cercato.</param>
/// <param name="Question">La domanda, com'è arrivata.</param>
/// <param name="AcceptLanguage">L'<c>Accept-Language</c> usato: quello chiesto, o «it» se assente.</param>
/// <param name="Language">La lingua con cui si è cercato (ISO 639-1): guida l'embedding della domanda.</param>
/// <param name="LanguageSource">Da dove viene: <c>Detected</c>, <c>Requested</c>, <c>AcceptLanguage</c> o <c>DetectedUncertain</c>.</param>
/// <param name="ProfileVersionId">La versione pubblicata del profilo dell'agente, la stessa che legge la chat.</param>
/// <param name="ProfileVersionNumber">Il suo numero.</param>
/// <param name="Parameters">I numeri del recupero, presi da quella versione.</param>
/// <param name="Reranking">
/// Come sono stati scelti i finali: <c>Applied</c> (dal cross-encoder), <c>Skipped</c> (uno o nessun
/// candidato), <c>FellBack</c> (il cross-encoder ha fallito: i primi della fusione) o <c>Disabled</c>
/// (il profilo lo spegne). Su <c>FellBack</c> la misura non dice nulla del reranker.
/// </param>
/// <param name="FusedCount">Quanti candidati ha dato la ricerca ibrida prima del taglio al pool (fino al doppio del pool).</param>
/// <param name="Pool">I candidati dati al reranker, nell'ordine della fusione.</param>
/// <param name="Final">I passaggi che la chat metterebbe nel prompt, nell'ordine.</param>
internal sealed record RetrievalProbeDto(
    Guid GameId,
    string Question,
    string AcceptLanguage,
    string Language,
    string LanguageSource,
    Guid ProfileVersionId,
    int ProfileVersionNumber,
    RetrievalProbeParametersDto Parameters,
    string Reranking,
    int FusedCount,
    IReadOnlyList<RetrievalProbeChunkDto> Pool,
    IReadOnlyList<RetrievalProbeChunkDto> Final);

/// <summary>#4482: i numeri del recupero, dalla versione pubblicata del profilo.</summary>
/// <param name="CandidatePoolSize">Quanti candidati chiedere a ciascun braccio, e quanti passarne al reranker.</param>
/// <param name="FinalTopK">Quanti passaggi tenere.</param>
/// <param name="MinScore">La soglia di coseno del braccio vettoriale.</param>
/// <param name="VectorWeight">Il peso del braccio vettoriale nella fusione.</param>
/// <param name="KeywordWeight">Il peso del braccio lessicale nella fusione.</param>
/// <param name="RerankerEnabled">Se il profilo usa il reranker.</param>
/// <param name="SearchMode">Il modo della ricerca: quello della chat, «hybrid».</param>
/// <param name="QueryRoleHint">I ruoli che il classificatore ha letto nella domanda, passati alla fusione.</param>
internal sealed record RetrievalProbeParametersDto(
    int CandidatePoolSize,
    int FinalTopK,
    decimal MinScore,
    decimal VectorWeight,
    decimal KeywordWeight,
    bool RerankerEnabled,
    string SearchMode,
    string QueryRoleHint);

/// <summary>#4482: un chunk recuperato, con il testo intero e i punteggi che il recupero conosce.</summary>
/// <param name="Position">La posizione nella sua lista, da 1.</param>
/// <param name="PoolPosition">Solo nei finali: la posizione del chunk nel pool, da 1.</param>
/// <param name="TextChunkId">L'id del <c>text_chunks</c>, quando il braccio lo conosce.</param>
/// <param name="VectorDocumentId">Il documento: l'id del documento vettoriale dal braccio vettoriale, quello del PDF dal lessicale.</param>
/// <param name="PdfDocumentId">Il PDF.</param>
/// <param name="ChunkIndex">La posizione del chunk nel documento.</param>
/// <param name="PageNumber">La pagina.</param>
/// <param name="Language">La lingua del chunk, come è indicizzato.</param>
/// <param name="RelevanceScore">Il punteggio con cui il chunk esce dalla fusione.</param>
/// <param name="HybridScore">Il punteggio ibrido della fusione.</param>
/// <param name="VectorScore">Il coseno del braccio vettoriale; assente se il chunk viene solo dal lessicale.</param>
/// <param name="KeywordScore">Il punteggio del braccio lessicale; assente se il chunk viene solo dal vettoriale.</param>
/// <param name="RerankScore">Solo nei finali scelti dal cross-encoder: il suo punteggio.</param>
/// <param name="SearchMethod">Da quale ricerca viene.</param>
/// <param name="MatchedTerms">I termini della domanda trovati dal braccio lessicale.</param>
/// <param name="Text">Il testo intero del chunk.</param>
internal sealed record RetrievalProbeChunkDto(
    int Position,
    int? PoolPosition,
    Guid? TextChunkId,
    string VectorDocumentId,
    string? PdfDocumentId,
    int ChunkIndex,
    int PageNumber,
    string? Language,
    double RelevanceScore,
    float? HybridScore,
    float? VectorScore,
    float? KeywordScore,
    double? RerankScore,
    string? SearchMethod,
    IReadOnlyList<string>? MatchedTerms,
    string Text);
