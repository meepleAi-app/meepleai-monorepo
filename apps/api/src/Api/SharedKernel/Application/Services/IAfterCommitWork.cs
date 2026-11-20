namespace Api.SharedKernel.Application.Services;

/// <summary>
/// Work outside the database that must happen only if the transaction the caller is in commits (#5093): a stored file
/// deleted, a cover removed. With no transaction open it runs right away.
/// </summary>
/// <remarks>
/// After a commit the work runs in a scope of its own, whose services it receives: the scope of the caller still holds
/// the transaction being closed, and its DbContext cannot be used. Publishing a domain event writes its audit row
/// (<c>DomainEventAuditHandler</c>), so it needs that fresh scope too. The work is best-effort once the commit has
/// happened: a failure is logged and does not undo the commit.
/// </remarks>
public interface IAfterCommitWork
{
    /// <summary>
    /// Runs <paramref name="work"/> now, with the caller's services, when no transaction is open; otherwise after it
    /// commits, with the services of a new scope. A rollback, or a commit that fails, drops it.
    /// </summary>
    Task RunAfterCommitAsync(Func<IServiceProvider, CancellationToken, Task> work, CancellationToken cancellationToken);
}
