using System.Data.Common;
using Api.SharedKernel.Application.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Api.Infrastructure.Persistence;

/// <summary>
/// <see cref="IAfterCommitWork"/> as a transaction interceptor on the scope's <see cref="MeepleAiDbContext"/> (#5093).
/// While a transaction is open the work waits; the commit runs it, a rollback or a failure drops it.
/// </summary>
/// <remarks>
/// <para>
/// An <c>[AtomicAudit]</c> command runs its handler inside the transaction <c>AuditLoggingBehavior</c> opens, and the
/// commit comes only after the audit row: a file the handler deleted stayed deleted when that commit failed and brought
/// the row back. Listening to the transaction itself, not to the behavior, keeps the rule true for any transaction.
/// </para>
/// <para>
/// The transaction state comes from these events, not from the DbContext, which builds this interceptor. A transaction
/// disposed without a commit or a rollback raises no event: the next one to start drops what the abandoned one queued.
/// When the execution strategy retries, the failed attempt's rollback drops its work and the retry queues it again.
/// </para>
/// </remarks>
internal sealed class AfterCommitWork : DbTransactionInterceptor, IAfterCommitWork
{
    private readonly ILogger<AfterCommitWork> _logger;
    private readonly List<Func<CancellationToken, Task>> _pending = [];
    private bool _inTransaction;

    public AfterCommitWork(ILogger<AfterCommitWork> logger)
    {
        _logger = logger;
    }

    public Task RunAfterCommitAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (!_inTransaction)
        {
            return work(cancellationToken);
        }

        _pending.Add(work);
        return Task.CompletedTask;
    }

    public override DbTransaction TransactionStarted(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        Begin();
        return result;
    }

    public override ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        Begin();
        return ValueTask.FromResult(result);
    }

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        RunCommittedAsync().GetAwaiter().GetResult();

    public override Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) =>
        RunCommittedAsync();

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => Drop();

    public override Task TransactionRolledBackAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Drop();
        return Task.CompletedTask;
    }

    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData) => Drop();

    public override Task TransactionFailedAsync(
        DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Drop();
        return Task.CompletedTask;
    }

    private void Begin()
    {
        // Work still queued belongs to a transaction disposed without commit or rollback: it never committed.
        _pending.Clear();
        _inTransaction = true;
    }

    private void Drop()
    {
        _pending.Clear();
        _inTransaction = false;
    }

    private async Task RunCommittedAsync()
    {
        var work = _pending.ToArray();
        Drop();

        foreach (var item in work)
        {
            try
            {
                // The commit is done: a client gone in the meantime does not cancel what belongs to it.
                await item(CancellationToken.None).ConfigureAwait(false);
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
