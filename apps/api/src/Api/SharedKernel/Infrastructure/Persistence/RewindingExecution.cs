using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.SharedKernel.Infrastructure.Persistence;

/// <summary>
/// The execution strategy of a <see cref="MeepleAiDbContext"/>, with every attempt after the first starting from the
/// change tracker and the collected domain events as the caller handed them in (#5162).
/// </summary>
/// <remarks>
/// <para>
/// <c>EnableRetryOnFailure</c> is on outside <c>Testing</c>, and a code path that opens a transaction by hand must run
/// it inside <c>CreateExecutionStrategy().ExecuteAsync</c> (#3636). The strategy runs the delegate again after a
/// transient failure, and the tracker it finds is the one the failed attempt left: after a failed save, the
/// attempt's rows still <c>Added</c> and written again next to the retry's; after a failed commit, its changes already
/// <c>Unchanged</c>, so a change the retry repeats is not written. A backup code marked used in a failed attempt
/// stayed usable (<c>TotpService</c>): the retry's identity resolution handed back the tracked instance, already
/// <c>IsUsed = true</c>, and only <c>UsedAt</c> reached the database.
/// </para>
/// <para>
/// This is what <see cref="EfCoreUnitOfWork"/> does for its own loop (#5145), for the code paths that cannot go
/// through the unit of work because they need an isolation level, a transaction of their own around a mediator call,
/// or a result of their own. A new direct use of <c>CreateExecutionStrategy()</c> is refused by
/// <c>ExecutionStrategyUsesTheRewindArchitectureTests</c>.
/// </para>
/// <para>
/// Only the database work belongs in the delegate. Anything that cannot run twice — a single-use OAuth state, a call
/// to a provider, a message — goes before it: the rewind puts back the tracker, not the world.
/// </para>
/// </remarks>
internal static class RewindingExecution
{
    /// <summary>Runs <paramref name="operation"/> under the context's execution strategy, rewinding before every retry.</summary>
    public static async Task<TResult> ExecuteRewindingAsync<TResult>(
        this MeepleAiDbContext context,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operation);

        var strategy = context.Database.CreateExecutionStrategy();
        var rewind = ChangeTrackerRewind.CaptureIfRetrying(strategy, context, context.DomainEventCollector);
        var attempt = 0;

        return await strategy.ExecuteAsync(async ct =>
        {
            if (attempt++ > 0)
                rewind?.Restore();

            return await operation(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ExecuteRewindingAsync{TResult}"/>
    public static Task ExecuteRewindingAsync(
        this MeepleAiDbContext context,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return context.ExecuteRewindingAsync<bool>(async ct =>
        {
            await operation(ct).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }
}
