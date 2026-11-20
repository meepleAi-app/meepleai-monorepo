namespace Api.SharedKernel.Application.Services;

/// <summary>
/// Work outside the database that must happen only if the transaction the caller is in commits (#5093): a stored file
/// deleted, a cover removed. With no transaction open it runs right away.
/// </summary>
/// <remarks>
/// The work is best-effort once the commit has happened: a failure is logged and does not undo the commit. It runs
/// while the transaction is still being closed, so it must not use the scope's DbContext: storage, caches and
/// notifications only.
/// </remarks>
public interface IAfterCommitWork
{
    /// <summary>
    /// Runs <paramref name="work"/> now when no transaction is open, otherwise after it commits. A rollback drops it.
    /// </summary>
    Task RunAfterCommitAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);
}
