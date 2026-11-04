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

    /// <summary>
    /// Dice se il link è valido sulla riga in questo momento: non revocato e non scaduto (#4592).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sola lettura: non conta una visita e non scrive nulla. È per chi usa il link ma non lo visita, come il
    /// commento a una chat condivisa. La visita no: usa <see cref="TryRecordAccessAsync"/>, che controlla e
    /// conta nello stesso statement.
    /// </para>
    /// <para>
    /// La condizione è la stessa di <see cref="TryRecordAccessAsync"/> e vive in un solo punto
    /// dell'implementazione, così le due non possono divergere. Non guarda la blacklist Redis: la riga è la
    /// verità anche quando la chiave si è persa.
    /// </para>
    /// <para>
    /// Il risultato è una fotografia. Fra la lettura e ciò che il chiamante scrive il proprietario può
    /// revocare il link: per un commento, una revoca nel mezzo vale come un commento arrivato un istante prima.
    /// </para>
    /// </remarks>
    /// <param name="id">Id del link, dal token già validato.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns><c>true</c> se il link esiste, non è revocato e non è scaduto; altrimenti <c>false</c>.</returns>
    Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default);
}
