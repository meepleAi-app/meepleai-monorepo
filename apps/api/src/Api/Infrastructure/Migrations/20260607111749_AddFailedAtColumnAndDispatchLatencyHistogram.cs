using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Issue #1535 T6 follow-up: addresses code-review finding F9 (GetFailedEventOutboxRowsQuery
    /// claimed "most-recent failures first" but the EnqueuedAt-based sort buried recent failures
    /// behind older retry timeouts). Adds the <c>failed_at</c> column (set on MarkFailed,
    /// cleared on RearmFromFailed) and re-creates the partial index
    /// <c>ix_domain_event_outbox_failed_recent</c> on <c>failed_at DESC</c> so the query plan
    /// uses the index directly.
    ///
    /// <para>The migration name also mentions the dispatch-latency histogram which is a
    /// separate code-only deliverable (no schema change). Kept in the migration name as a
    /// pointer to the parallel commit batch.</para>
    /// </remarks>
    public partial class AddFailedAtColumnAndDispatchLatencyHistogram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_domain_event_outbox_failed_recent",
                table: "domain_event_outbox");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "failed_at",
                table: "domain_event_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_domain_event_outbox_failed_recent",
                table: "domain_event_outbox",
                column: "failed_at",
                descending: new bool[0],
                filter: "status = 2::smallint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_domain_event_outbox_failed_recent",
                table: "domain_event_outbox");

            migrationBuilder.DropColumn(
                name: "failed_at",
                table: "domain_event_outbox");

            migrationBuilder.CreateIndex(
                name: "ix_domain_event_outbox_failed_recent",
                table: "domain_event_outbox",
                column: "enqueued_at",
                descending: new bool[0],
                filter: "status = 2::smallint");
        }
    }
}
