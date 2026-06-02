using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Issue #1675: added <c>RowVersion</c> property to <c>KbQualityBudgetCounter</c> for
    /// optimistic concurrency control on parallel monthly-counter increments.
    /// <para>No DDL emitted: Npgsql maps <c>[Timestamp] byte[]?</c> with <c>.IsRowVersion()</c>
    /// to the Postgres system column <c>xmin</c>, which exists natively on every row.
    /// The migration is retained to document the model-version bump and keep the snapshot
    /// in sync with downstream rebases.</para>
    /// </remarks>
    public partial class AddRowVersionToKbQualityBudgetCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            // Intentionally empty — Postgres xmin is a system column, no schema change required.
            // The argument is touched only to satisfy SonarAnalyzer S1186 (no-empty-method).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            // Intentionally empty — see Up().
        }
    }
}
