using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Api.Tests.TestHelpers;

/// <summary>
/// A failure the retrying execution strategy replays, injected once at the next save or at the next commit: an
/// <see cref="NpgsqlException"/> around a <see cref="TimeoutException"/>, which Npgsql calls transient (#5145, #5162).
/// </summary>
/// <remarks>
/// Register both interceptors on a context whose host enables the strategy
/// (<c>IntegrationServiceCollectionBuilder.CreateBase</c>): under the <c>Testing</c> environment the delegate runs
/// once and a retry cannot be seen. Arm one, run the code, then assert <c>Tripped</c> before anything else — a fault
/// that never fired proves nothing.
/// </remarks>
internal sealed class TransientDatabaseFaults
{
    public SaveFault Save { get; } = new();

    public CommitFault Commit { get; } = new();

    private static NpgsqlException Transient(string where) =>
        new($"transient failure at the {where}, injected by the test", new TimeoutException());

    public sealed class SaveFault : SaveChangesInterceptor
    {
        public volatile bool Armed;
        public volatile bool Tripped;

        /// <summary>When set, only a save this predicate accepts trips the fault.</summary>
        public Func<DbContext, bool>? When { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && (When is null || When(eventData.Context!)))
            {
                Armed = false;
                Tripped = true;
                throw Transient("save");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    public sealed class CommitFault : DbTransactionInterceptor
    {
        public volatile bool Armed;
        public volatile bool Tripped;

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (Armed)
            {
                Armed = false;
                Tripped = true;
                throw Transient("commit");
            }

            return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
        }
    }
}
