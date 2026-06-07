using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Issue #1535 T6 code review (F6): wire Postgres-native <c>xmin</c> as the optimistic
    /// concurrency token on <c>domain_event_outbox</c>. The column already exists as a
    /// hidden system column on every Postgres table — no <c>ADD COLUMN</c> is needed; the
    /// EntityConfiguration declares the EF property as <c>ValueGeneratedOnAddOrUpdate</c>
    /// + <c>IsConcurrencyToken</c> so EF Core's UPDATE statements include <c>WHERE xmin = @p</c>
    /// and raise <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
    /// on a lost-update race (admin retry overlapping a processor commit, future multi-
    /// instance work-stealing, etc.).
    ///
    /// <para>The EF model snapshot diff also surfaces the partial-index filter change
    /// (<c>status = 0</c> → <c>status = 0::smallint</c>) — that is already applied via raw
    /// SQL in the prior <c>20260607081120_AddTestRunIdToEntitiesAlignment</c> migration.
    /// This migration is therefore <b>schema-no-op</b> at runtime: it carries the model
    /// snapshot reconciliation only.</para>
    /// </remarks>
    public partial class AlignDomainEventOutboxConcurrencyTokenAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // xmin is a hidden Postgres system column on every table — nothing to do at
            // the schema level. EF's UPDATE SQL will include the concurrency-token
            // WHERE-clause once the model snapshot reflects the property.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Symmetric no-op — removing the concurrency token requires only a model
            // snapshot regeneration, not a SQL change.
        }
    }
}
