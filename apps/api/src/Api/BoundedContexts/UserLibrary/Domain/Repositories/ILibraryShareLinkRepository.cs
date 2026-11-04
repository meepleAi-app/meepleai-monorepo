using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

/// <summary>
/// Repository interface for LibraryShareLink aggregate.
/// </summary>
internal interface ILibraryShareLinkRepository : IRepository<LibraryShareLink, Guid>
{
    /// <summary>
    /// Gets a share link by its share token.
    /// </summary>
    /// <param name="shareToken">The share token to look up</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The share link if found, null otherwise</returns>
    Task<LibraryShareLink?> GetByShareTokenAsync(string shareToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active share link for a user (non-revoked, non-expired).
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The active share link if exists, null otherwise</returns>
    Task<LibraryShareLink?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all share links for a user (including revoked/expired).
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of share links</returns>
    Task<IReadOnlyList<LibraryShareLink>> GetAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts share links created by user in the last 24 hours (for rate limiting).
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of share links created in last 24 hours</returns>
    Task<int> CountRecentByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Conta una visita pubblica al link, solo se il link è ancora valido nel momento in cui la scrive (#4558).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Un solo statement: incrementa <c>view_count</c> e aggiorna <c>last_accessed_at</c> dove il link non è
    /// revocato né scaduto. Non passa dall'aggregato e non tocca altre colonne, quindi una visita non può
    /// riscrivere ciò che il proprietario ha deciso nel frattempo: revoca, privacy, note, scadenza. Due visite
    /// concorrenti contano entrambe.
    /// </para>
    /// <para>
    /// Si scrive subito, fuori dall'unità di lavoro: non serve <c>SaveChangesAsync</c>. Le colonne del
    /// contatore sono di questo metodo: <c>UpdateAsync</c> non le scrive.
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
