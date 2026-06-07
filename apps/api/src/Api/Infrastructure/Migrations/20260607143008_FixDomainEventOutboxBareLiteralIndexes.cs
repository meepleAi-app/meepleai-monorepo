using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Issue #1535 follow-up + Issue #1967: rebuild the surviving bare-integer partial
    /// index <c>ix_domain_event_outbox_pending</c> with an explicit <c>::smallint</c>
    /// filter literal so the Postgres planner reliably matches the predicate against the
    /// Npgsql-parameterised query (which binds <c>@p0</c> as <c>smallint</c>). Without
    /// the cast the planner may degrade to seq-scan on the hot poll path.
    ///
    /// <para>Also creates <c>ix_domain_event_outbox_sent_dispatched_at</c> needed by
    /// <c>GetEventOutboxStatsQuery.SentLast24h</c> (#1535 T6 review F15).</para>
    ///
    /// <para><b>Background</b>: these two operations originally lived inside
    /// <c>20260607081120_AddTestRunIdToEntitiesAlignment.cs</c> shipped by #1535. That
    /// migration also added <c>test_run_id</c> columns to 7 entities, but the same
    /// columns were independently added by the parallel <c>20260607061447_Add_TestRunId_To_UserGameSessions</c>
    /// migration shipped by #1954 (Macro 4) shortly before #1535 merged. When
    /// <c>MigrateAsync</c> ran the two migrations in order, the second failed with
    /// "column already exists". The cleanup contract in #1967 mandated deleting the
    /// alignment migration and preserving the index operations — this migration is
    /// that successor.</para>
    ///
    /// <para>The <c>ix_domain_event_outbox_failed_recent</c> partial index is NOT
    /// touched here — <c>20260607111749_AddFailedAtColumnAndDispatchLatencyHistogram</c>
    /// already DROP+CREATE'd it on <c>failed_at DESC</c> with the <c>::smallint</c>
    /// filter, so it is already in the correct state.</para>
    /// </remarks>
    public partial class FixDomainEventOutboxBareLiteralIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pending: rebuild with smallint-typed filter literal.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_domain_event_outbox_pending;");
            migrationBuilder.Sql(
                "CREATE INDEX ix_domain_event_outbox_pending " +
                "ON domain_event_outbox (next_attempt_at, enqueued_at) " +
                "WHERE status = 0::smallint;");

            // Sent24h dashboard: create the partial index that was never materialised on
            // main-dev (the deleted alignment migration was supposed to create it).
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_domain_event_outbox_sent_dispatched_at " +
                "ON domain_event_outbox (dispatched_at DESC) " +
                "WHERE status = 1::smallint;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_domain_event_outbox_sent_dispatched_at;");
            // Revert pending index to the bare-integer literal that originally shipped
            // in 20260607042524_AddDomainEventOutboxTable.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_domain_event_outbox_pending;");
            migrationBuilder.Sql(
                "CREATE INDEX ix_domain_event_outbox_pending " +
                "ON domain_event_outbox (next_attempt_at, enqueued_at) " +
                "WHERE status = 0;");
        }
    }
}
