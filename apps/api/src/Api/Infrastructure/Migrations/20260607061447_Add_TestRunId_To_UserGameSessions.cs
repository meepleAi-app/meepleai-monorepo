using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Materializes the TestRunId column on the 3 entities NOT already covered by
    /// `20260606074943_AddSourceEventIdToCounterTables_CF2` (which had silently picked
    /// up 5 of the 8 drifted columns during its "drift recovery" pass):
    /// - UserLibraryEntries (PR #1951 / Issue #1929 Macro 3a, sess.42)
    /// - SharedGames (PR #1951 / Issue #1929 Macro 3a, sess.42)
    /// - UserGameSessions/game_sessions (PR #1954 / Issue #1929 Macro 4, sess.43 — this PR)
    ///
    /// **2026-06-08 fix (Issue #2013, smoke failure):** the original Up() called
    /// `AddColumn` on all 8 tables, but the prior CF2 migration had already added
    /// `test_run_id` to 5 of them (users, game_night_events, game_night_invitations,
    /// game_night_rsvps, game_night_sessions). On a fresh DB the CF2 migration runs
    /// first (older timestamp) and the consolidation here then fails on the first
    /// duplicate AddColumn with "column already exists". This crashed the API at
    /// startup and turned the nightly E2E smoke red.
    ///
    /// Background (pre-fix): in PRs #1936/#1951/#1954 the TestRunId property + EF
    /// configuration were added to the entity model, but the column was materialized
    /// only via `EnsureCreatedAsync` inside the `SharedTestcontainersFixture`
    /// integration test fixture. PR-CF2 quietly captured the resulting snapshot
    /// drift; the consolidation here was authored without knowing the CF2 file had
    /// already taken 5 of the 8 columns. The fix scopes this migration down to the
    /// remaining 3 tables.
    ///
    /// Verification: <code>grep -n 'test_run_id' apps/api/src/Api/Infrastructure/Migrations/*.cs</code>
    /// (excluding *.Designer.cs + *Snapshot.cs) returns each `test_run_id` column
    /// in exactly ONE migration file.
    /// </summary>
    /// <example>
    /// Future contributors adding TestRunId to a new entity for E2E seeding (DEC-B-8
    /// pattern) MUST NOT amend this migration. The correct pattern is:
    ///   1. Add <c>public string? TestRunId { get; set; }</c> to the new entity class.
    ///   2. Add EF configuration: <c>HasMaxLength(64)</c> + partial Postgres index
    ///      <c>WHERE "test_run_id" IS NOT NULL</c>.
    ///   3. Run <c>dotnet ef migrations add Add_TestRunId_To_&lt;NewEntity&gt;</c> to
    ///      generate a dedicated migration with a single targeted AddColumn call.
    ///   4. Update <c>CleanupTestEntitiesCommandHandler</c> with the new ExecuteDeleteAsync
    ///      step in correct FK child→parent order.
    /// </example>
    /// <remarks>
    /// Refs Issues #1928 (Task B) + #1929 (Task C Macro 3a + Macro 4) + Umbrella #1895.
    /// Follow-up #1955 added this documentation post-merge.
    /// </remarks>
    public partial class Add_TestRunId_To_UserGameSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE (Issue #2013, 2026-06-08): the 5 AddColumn calls for
            // users / game_night_events / game_night_invitations / game_night_rsvps /
            // game_night_sessions were removed because CF2 (timestamp 20260606074943)
            // already adds those columns earlier in the migration sequence. Keeping
            // them here caused "column already exists" on every fresh-DB migrate.
            migrationBuilder.AddColumn<string>(
                name: "test_run_id",
                table: "user_library_entries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "test_run_id",
                table: "shared_games",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "test_run_id",
                table: "game_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_sessions_test_run_id",
                table: "game_sessions",
                column: "test_run_id",
                filter: "\"test_run_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Symmetry with Up(): only drop the 3 columns this migration created.
            // CF2 owns the Down() for the other 5 tables.
            migrationBuilder.DropIndex(
                name: "ix_game_sessions_test_run_id",
                table: "game_sessions");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "game_sessions");
        }
    }
}
