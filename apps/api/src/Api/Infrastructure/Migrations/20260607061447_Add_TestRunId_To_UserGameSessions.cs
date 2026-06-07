using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
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
