using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Domain.Repositories;

/// <summary>
/// Repository interface for ShareLink aggregate.
/// </summary>
internal interface IShareLinkRepository : IRepository<ShareLink, Guid>
{
    /// <summary>
    /// Finds all share links for a specific thread.
    /// </summary>
    Task<IReadOnlyList<ShareLink>> FindByThreadIdAsync(Guid threadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all share links created by a specific user.
    /// </summary>
    Task<IReadOnlyList<ShareLink>> FindByCreatorIdAsync(Guid creatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a share link by thread ID and creator ID (for ownership verification).
    /// </summary>
    Task<ShareLink?> FindByThreadIdAndCreatorIdAsync(Guid threadId, Guid creatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all active (not revoked, not expired) share links for a thread.
    /// </summary>
    Task<IReadOnlyList<ShareLink>> FindActiveByThreadIdAsync(Guid threadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Conta una visita pubblica al link, solo se il link è ancora valido nel momento in cui la scrive (#4573).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Un solo statement: incrementa <c>access_count</c> e aggiorna <c>last_accessed_at</c> dove il link non è
    /// revocato né scaduto. Non passa dall'aggregato e non tocca altre colonne, quindi una visita non può
    /// riscrivere ciò che il proprietario ha deciso nel frattempo: revoca, scadenza, etichetta. Due visite
    /// concorrenti contano entrambe.
    /// </para>
    /// <para>
    /// Si scrive subito, fuori da <c>SaveChangesAsync</c>. Le colonne del contatore sono di questo metodo:
    /// <c>UpdateAsync</c> non le scrive.
    /// </para>
    /// </remarks>
    /// <param name="id">Id del link letto dalla visita.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// <c>true</c> se la visita è stata contata; <c>false</c> se nel frattempo il link è stato revocato, è
    /// scaduto o non esiste più. Per la visita <c>false</c> vuol dire 404.
    /// </returns>
    Task<bool> TryRecordAccessAsync(Guid id, CancellationToken cancellationToken = default);
}
