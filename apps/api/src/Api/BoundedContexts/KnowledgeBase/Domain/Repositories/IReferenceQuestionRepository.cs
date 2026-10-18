using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// #4470 (ADR-095 fetta 7b): lettura delle domande di riferimento. Le scrive solo il seeder, dal set.
/// </summary>
internal interface IReferenceQuestionRepository
{
    /// <summary>
    /// Le domande che passano il filtro, in ordine di codice. Ogni filtro assente lascia passare tutto.
    /// </summary>
    Task<IReadOnlyList<ReferenceQuestion>> ListAsync(
        Guid? sharedGameId, bool? inGate, CancellationToken cancellationToken = default);

    /// <summary>Quante domande ha ogni gioco, senza filtri: un gioco senza domande non c'è.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountByGameAsync(CancellationToken cancellationToken = default);
}
