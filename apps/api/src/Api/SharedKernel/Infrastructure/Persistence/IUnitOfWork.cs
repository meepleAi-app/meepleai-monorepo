namespace Api.SharedKernel.Infrastructure.Persistence;

/// <summary>
/// Unit of Work pattern for managing transactions across repositories.
/// Ensures that all changes are committed or rolled back together.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Saves all changes made in this unit of work to the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of entities written to the database</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Issue #3636 — runs <paramref name="work"/> inside a transaction that is compatible with the
    /// connection-resiliency retry strategy.
    ///
    /// <para>
    /// <b>Use this instead of <see cref="BeginTransactionAsync"/>.</b> The DbContext is registered
    /// with <c>EnableRetryOnFailure</c> in every environment except <c>Testing</c>, and EF Core
    /// refuses a user-initiated transaction under a retrying strategy: opening one throws
    /// <see cref="InvalidOperationException"/> at runtime. That is not hypothetical — it returned
    /// HTTP 500 on the public waitlist endpoint on staging.
    /// </para>
    /// <para>
    /// ⚠️ <paramref name="work"/> MUST be idempotent: on a transient failure the strategy re-runs the
    /// whole delegate, transaction included. Keep side effects that cannot be replayed (sending mail,
    /// calling a third party) outside it.
    /// </para>
    /// <para>
    /// The implementation opens the transaction, runs the delegate, calls <c>SaveChanges</c> and
    /// commits. On exception it rolls back and rethrows, so callers keep catching what they caught
    /// before.
    /// </para>
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ExecuteInTransactionAsync{T}"/>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    /// <remarks>
    /// ⚠️ Issue #3636: incompatible with the retry strategy active outside <c>Testing</c> — throws
    /// <see cref="InvalidOperationException"/> at runtime. Kept for the paths not yet migrated;
    /// new code must use <see cref="ExecuteInTransactionAsync{T}"/>.
    /// </remarks>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
