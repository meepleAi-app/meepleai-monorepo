using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Consolidates the TestRunId schema drift accumulated across 3 prior PRs:
    /// - PR #1936 (Issue #1928 Task B, sess.39-40): DEC-B-8 explicit columns on
    ///   Users + GameNightEvents + GameNightSessions + GameNightRsvps + GameNightInvitations.
    /// - PR #1951 (Issue #1929 Macro 3a, sess.42): DEC-C-8 on UserLibraryEntries + SharedGames.
    /// - PR #1954 (Issue #1929 Macro 4, sess.43): UserGameSessions (this PR — adds property).
    ///
    /// In all 3 PRs the TestRunId property + EF configuration were added to the entity
    /// model, but the column was materialized only via `EnsureCreatedAsync` inside the
    /// `SharedTestcontainersFixture` integration test fixture. No EF Core migration was
    /// generated, leaving `MeepleAiDbContextModelSnapshot` out-of-sync with the entity
    /// classes. This migration adds the column physically to all 8 tables in a single
    /// `Up()` to reconcile the snapshot with the model.
    ///
    /// Acceptance criterion (verifiable): on `main-dev` HEAD prior to this PR,
    /// <code>grep -l "test_run_id" apps/api/src/Api/Infrastructure/Migrations/*.cs</code>
    /// (excluding *.Designer.cs + *Snapshot.cs) returns zero matches. After this PR,
    /// only this file matches — confirming this is the first migration to materialize
    /// the column and is NOT a duplicate of any prior schema change.
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
            migrationBuilder.AddColumn<string>(
                name: "test_run_id",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

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

            migrationBuilder.AddColumn<string>(
                name: "test_run_id",
                table: "game_night_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "test_run_id",
                table: "game_night_rsvps",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "test_run_id",
                table: "game_night_invitations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "test_run_id",
                table: "game_night_events",
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
            migrationBuilder.DropIndex(
                name: "ix_game_sessions_test_run_id",
                table: "game_sessions");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "game_sessions");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "game_night_sessions");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "game_night_rsvps");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "game_night_invitations");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "game_night_events");
        }
    }
}
