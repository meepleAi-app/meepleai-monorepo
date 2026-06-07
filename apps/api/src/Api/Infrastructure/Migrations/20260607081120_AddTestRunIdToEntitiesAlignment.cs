using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Issue #1535 T6 — temporary in-branch alignment migration.
    ///
    /// <para>The seven entities below carry an explicit <c>test_run_id</c> column
    /// (issue #1928 DEC-B-8) on their CLR projection, but the corresponding
    /// <c>ADD COLUMN</c> statements live in PRs that are still pending merge to
    /// <c>main-dev</c> (#1946 CF-2, #1947 CF-3, #1949 iso-1, #1950 iso-2). Without
    /// this migration the integration test rig's <c>Database.MigrateAsync()</c>
    /// step succeeds, but the FIRST EF query against <c>users</c> (or any other
    /// listed table) raises <c>Npgsql.PostgresException 42703: column u.test_run_id
    /// does not exist</c> — blocking every HTTP integration test on this branch.</para>
    ///
    /// <para><b>Cleanup contract</b>: when any of #1946/#1947/#1949/#1950 lands on
    /// <c>main-dev</c> first, the merge-up of this branch will produce duplicate
    /// <c>ADD COLUMN</c> migrations. Resolve by DELETING this migration (and its
    /// designer) and letting the upstream PR own the column creation. The model
    /// snapshot will reconcile on the next <c>dotnet ef migrations add</c>.</para>
    /// </remarks>
    public partial class AddTestRunIdToEntitiesAlignment : Migration
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
