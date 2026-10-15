using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

#pragma warning disable MA0048 // File name must match type name - the step, its port and what it returns
namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4410 (spec 2026-10-11 §9, E1): il passo «recupero → citazioni con tier risolto» di una risposta. Lo
/// usano la pipeline delle risposte e <c>cerca_regole</c> dell'assistente in partita: una correzione del
/// copyright si fa in un posto solo (ADR-090).
/// </summary>
internal interface ITieredPassageSearch
{
    /// <summary>
    /// I passaggi per la domanda e le loro citazioni, con il tier risolto per <paramref name="userId"/>.
    /// Non controlla l'accesso: chi chiama è già passato da <see cref="IAnswerPreconditions"/>.
    /// </summary>
    Task<TieredPassages> SearchAsync(
        AnswerRetrievalRequest request, AgentProfileContent profile, string language, Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// I risultati del recupero e le citazioni, nell'ordine in cui le restituisce il resolver del tier. Il testo
/// letterale resta in ogni citazione, perché è il contesto del modello. Regioni e offset li limita al tier Full
/// chi costruisce la forma per il client: la pipeline per il suo stream, <c>FinalizeAsync</c> per l'assistente.
/// </summary>
internal sealed record TieredPassages(IReadOnlyList<SearchResultDto> Results, IReadOnlyList<ChunkCitation> Citations)
{
    public static TieredPassages None { get; } = new([], []);

    public bool IsEmpty => Results.Count == 0;
}

internal sealed class TieredPassageSearch : ITieredPassageSearch
{
    private readonly AnswerRetrieval _retrieval;
    private readonly IVectorDocumentRepository _vectorDocuments;
    private readonly ICopyrightTierResolver _tierResolver;

    public TieredPassageSearch(AnswerRetrieval retrieval, IVectorDocumentRepository vectorDocuments, ICopyrightTierResolver tierResolver)
    {
        _retrieval = retrieval ?? throw new ArgumentNullException(nameof(retrieval));
        _vectorDocuments = vectorDocuments ?? throw new ArgumentNullException(nameof(vectorDocuments));
        _tierResolver = tierResolver ?? throw new ArgumentNullException(nameof(tierResolver));
    }

    public async Task<TieredPassages> SearchAsync(
        AnswerRetrievalRequest request, AgentProfileContent profile, string language, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // #4243: the request filtered by document and none of the named documents belongs to this
        // game. That finds nothing. It must stop here: further down an empty list means "no filter".
        if (request.PdfDocumentIds is { Count: 0 })
        {
            return TieredPassages.None;
        }

        // Passi 3 e 4: recupero e reranking con i numeri del profilo, condivisi con la domanda singola.
        var results = await _retrieval.RetrieveAsync(request, profile, language, cancellationToken).ConfigureAwait(false);
        if (results.Count == 0)
        {
            return TieredPassages.None;
        }

        var citations = await CiteAsync(request.GameId, results, userId, cancellationToken).ConfigureAwait(false);
        return new TieredPassages(results, citations);
    }

    /// <summary>
    /// Le citazioni dei risultati, con il tier risolto per <paramref name="userId"/>. La sorgente di una citazione
    /// è l'id del PDF, che il visore del frontend sa aprire; quello del documento vettoriale resta solo se la
    /// risoluzione manca (Issue #W).
    /// </summary>
    public async Task<IReadOnlyList<ChunkCitation>> CiteAsync(
        Guid gameId, IReadOnlyList<SearchResultDto> results, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(results);

        var vectorDocs = await _vectorDocuments
            .FindVectorDocumentsOfGameAfterAccessCheckAsync(gameId, cancellationToken).ConfigureAwait(false);
        var pdfIdByVectorId = vectorDocs.ToDictionary(v => v.Id, v => v.PdfDocumentId);

        // SP-C (#3407): the resolver keys on ChunkCitation.DocumentId = the resolved pdf id string (NOT
        // the vector id, NOT the "PDF:"-prefixed source), matching the session-agent chain.
        var raw = results.Select(r =>
        {
            var documentId = Guid.TryParse(r.VectorDocumentId, out var vectorId) && pdfIdByVectorId.TryGetValue(vectorId, out var pdfId)
                ? pdfId.ToString()
                : r.VectorDocumentId;
            return new ChunkCitation(
                DocumentId: documentId,
                PageNumber: r.PageNumber,
                RelevanceScore: (float)r.RelevanceScore,
                SnippetPreview: r.TextContent)
            {
                BoundingBoxesJson = r.BoundingBoxesJson,
                CharStart = r.CharStart,
                CharEnd = r.CharEnd,
            };
        }).ToList();

        return await _tierResolver.ResolveAsync(raw, userId, cancellationToken).ConfigureAwait(false);
    }
}
