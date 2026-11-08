using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository di <see cref="AssistantProposal"/> (spec 2026-10-11 §5.2). Pattern UoW di ADR-056: i metodi
/// che scrivono preparano soltanto le modifiche; il chiamante invoca
/// <see cref="Api.SharedKernel.Infrastructure.Persistence.IUnitOfWork.SaveChangesAsync"/>.
/// </summary>
public interface IAssistantProposalRepository
{
    /// <summary>La proposta, tracciata: le transizioni fatte dall'aggregato si salvano con la unit of work.</summary>
    Task<AssistantProposal?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// La proposta com'è adesso nel database, non tracciata: per rispondere con lo stato attuale dopo aver
    /// perso una corsa sulla concorrenza, quando l'entità tracciata è ormai vecchia.
    /// </summary>
    Task<AssistantProposal?> GetReadOnlyAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Le proposte ancora aperte di una sessione (<c>Pending</c> e <c>Applying</c>), tracciate.</summary>
    Task<IReadOnlyList<AssistantProposal>> ListOpenBySessionAsync(Guid liveSessionId, CancellationToken cancellationToken = default);

    /// <summary>Tutte le proposte nate in un thread, in sola lettura: servono alla storia del turno.</summary>
    Task<IReadOnlyList<AssistantProposal>> ListByThreadAsync(Guid chatThreadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Porta a <c>Expired</c> le proposte ancora <c>Pending</c> di una sessione: è l'inizio di un altro
    /// turno dell'assistente (spec §5.2). Un solo <c>UPDATE</c>, eseguito subito e fuori dalla unit of
    /// work: una conferma concorrente non fa fallire il turno, trova la proposta non più <c>Pending</c>.
    /// </summary>
    /// <returns>Quante proposte sono scadute.</returns>
    Task<int> ExpirePendingBySessionAsync(Guid liveSessionId, DateTime now, CancellationToken cancellationToken = default);

    Task AddAsync(AssistantProposal proposal, CancellationToken cancellationToken = default);
}
