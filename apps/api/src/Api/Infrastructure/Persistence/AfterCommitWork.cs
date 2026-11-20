using System.Data.Common;
using Api.SharedKernel.Application.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Api.Infrastructure.Persistence;

/// <summary>
/// <see cref="IAfterCommitWork"/> as a transaction interceptor on the scope's <see cref="MeepleAiDbContext"/> (#5093).
/// Work asked for while a transaction is open waits for that transaction: its commit runs the work, in a new scope.
/// Nothing else does: a rollback, a failed commit or a disposal leaves it unrun, and the next transaction to start
/// clears it.
/// </summary>
/// <remarks>
/// <para>
/// An <c>[AtomicAudit]</c> command runs its handler inside the transaction <c>AuditLoggingBehavior</c> opens, and the
/// commit comes only after the audit row: a file the handler deleted stayed deleted when that commit failed and brought
/// the row back. Listening to the transaction itself, not to the behavior, keeps the rule true for any transaction.
/// </para>
/// <para>
/// Whether a transaction is open is read from the DbContext when the work is asked for, not inferred from events: a
/// transaction disposed without a commit or a rollback raises none, and must not hold back later work. The context is
/// resolved then, not injected, because it builds this interceptor. When the execution strategy retries, the failed
/// attempt never commits and the retry's transaction starts over, so its work runs once.
/// </para>
/// <para>
/// A commit that throws leaves the work unrun even if the database did commit: a file then stays behind, rather than
/// going away under a row that may still exist.
/// </para>
/// </remarks>
internal sealed class AfterCommitWork : DbTransactionInterceptor, IAfterCommitWork
{
    private readonly IServiceProvider _scope;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AfterCommitWork> _logger;
    private readonly List<Func<IServiceProvider, CancellationToken, Task>> _pending = [];
    private DbTransaction? _awaited;

    public AfterCommitWork(IServiceProvider scope, IServiceScopeFactory scopes, ILogger<AfterCommitWork> logger)
    {
        _scope = scope;
        _scopes = scopes;
        _logger = logger;
    }

    public Task RunAfterCommitAsync(
        Func<IServiceProvider, CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        var open = _scope.GetRequiredService<MeepleAiDbContext>().Database.CurrentTransaction?.GetDbTransaction();
        if (open is null)
        {
            return work(_scope, cancellationToken);
        }

        if (!ReferenceEquals(open, _awaited))
        {
            // Run now, the work would happen before a commit that may not come; queued, it would wait for an event
            // that never arrives.
            throw new InvalidOperationException(
                "A transaction is open on the DbContext, but AfterCommitWork did not see it start: it is not among "
                + "the context's interceptors.");
        }

        _pending.Add(work);
        return Task.CompletedTask;
    }

    public override DbTransaction TransactionStarted(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        Await(result);
        return result;
    }

    public override ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        Await(result);
        return ValueTask.FromResult(result);
    }

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        RunCommittedAsync().GetAwaiter().GetResult();

    public override Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) =>
        RunCommittedAsync();

    private void Await(DbTransaction transaction)
    {
        // Work still queued waited for a transaction that never committed: rolled back, failed or disposed.
        _pending.Clear();
        _awaited = transaction;
    }

    private async Task RunCommittedAsync()
    {
        var work = _pending.ToArray();
        _pending.Clear();

        foreach (var item in work)
        {
            try
            {
                var scope = _scopes.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    // The commit is done: a client gone in the meantime does not cancel what belongs to it.
                    await item(scope.ServiceProvider, CancellationToken.None).ConfigureAwait(false);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031
            {
                // Best-effort: the commit cannot be undone, and the remaining work still runs.
                _logger.LogError(ex, "Work after a commit failed; the committed change stands");
            }
        }
    }
}
